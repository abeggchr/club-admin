using Microsoft.EntityFrameworkCore;
using PersonEntity = backend.Person.Person;
using SettingsEntity = backend.Settings.Settings;

namespace backend.Invoicing;

/// <summary>The outcome of <see cref="InvoiceDispatcher.SendAsync"/>.</summary>
public abstract record InvoiceSendOutcome
{
    /// <summary>No Invoice has this id.</summary>
    public sealed record NotFound : InvoiceSendOutcome;

    /// <summary>The Invoice was not sent and nothing was recorded.</summary>
    public sealed record Refused(string Error) : InvoiceSendOutcome;

    /// <summary>The channel accepted the Invoice and the Sent status change is saved.</summary>
    public sealed record Sent(InvoiceDetailDto Invoice) : InvoiceSendOutcome;
}

/// <summary>The outcome of <see cref="InvoiceDispatcher.BulkSendAsync"/>.</summary>
public abstract record BulkSendResult
{
    /// <summary>The message was refused before any Invoice was sent.</summary>
    public sealed record Refused(string Error) : BulkSendResult;

    /// <summary>Every planned Invoice was tried.</summary>
    public sealed record Done(BulkSendSummary Summary) : BulkSendResult;
}

/// <summary>One Invoice in the bulk send summary; the reason is null for a sent Invoice.</summary>
public record BulkSendOutcome(string Number, string Person, string? Reason);

/// <summary>The result of a bulk send: which Invoices were sent, failed or skipped.</summary>
public record BulkSendSummary(
    IReadOnlyList<BulkSendOutcome> Sent,
    IReadOnlyList<BulkSendOutcome> Failed,
    IReadOnlyList<BulkSendOutcome> Skipped
);

/// <summary>
/// The single entry point for sending Invoices, one or all unsent ones at once. For each Invoice it checks the
/// status, asks <see cref="DeliveryChannelRules"/> who delivers the chosen Delivery Channel, loads the PDF, hands it
/// to that backend's sender and saves the Sent status change, stamped with the time of that send, once the sender
/// succeeded.
/// </summary>
public class InvoiceDispatcher(
    AppDbContext db,
    IEnumerable<IInvoiceSender> senders,
    IInvoiceStorage storage,
    TimeProvider time,
    ILogger<InvoiceDispatcher> logger
)
{
    public async Task<InvoiceSendOutcome> SendAsync(
        int invoiceId,
        DeliveryChannel channel,
        InvoiceMessage message,
        CancellationToken ct
    )
    {
        var invoice = await db
            .Invoices.Include(i => i.Lines)
            .Include(i => i.StatusChanges)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (invoice is null)
        {
            return new InvoiceSendOutcome.NotFound();
        }
        var recipient = await db.Persons.AsNoTracking().SingleAsync(p => p.Id == invoice.PersonId, ct);
        var settings = await LoadSettingsAsync(ct);
        if (await SendOneAsync(invoice, recipient, settings, channel, message, ct) is { } error)
        {
            return new InvoiceSendOutcome.Refused(error);
        }
        await db.SaveChangesAsync(ct);
        return new InvoiceSendOutcome.Sent(InvoiceMapping.ToInvoiceDetail(invoice, recipient, settings));
    }

    /// <summary>How many unsent Invoices a bulk send would deliver by email, by post, or skip.</summary>
    public async Task<BulkSendPreview> PreviewBulkSendAsync(CancellationToken ct)
    {
        var plan = await PlanBulkSendAsync(await LoadSettingsAsync(ct), ct);
        return BulkSendRules.Preview(plan);
    }

    /// <summary>Sends every unsent Invoice by the channel <see cref="BulkSendRules"/> picks, one at a time; a failure never stops the rest.</summary>
    public async Task<BulkSendResult> BulkSendAsync(InvoiceMessage message, CancellationToken ct)
    {
        var settings = await LoadSettingsAsync(ct);
        var plan = await PlanBulkSendAsync(settings, ct);
        if (BulkSendRules.ValidateMessage(plan, message.Subject, message.Text) is { } invalid)
        {
            return new BulkSendResult.Refused(invalid);
        }
        var sent = new List<BulkSendOutcome>();
        var failed = new List<BulkSendOutcome>();
        var skipped = new List<BulkSendOutcome>();
        foreach (var item in plan)
        {
            var outcome = (string? reason) =>
                new BulkSendOutcome(
                    InvoiceRules.FormatNumber(item.Invoice.Id),
                    InvoiceMapping.DisplayName(item.Recipient),
                    reason
                );
            if (item.Channel is not { } channel)
            {
                skipped.Add(outcome(item.SkipReason));
                continue;
            }
            var error = await SendAndSaveAsync(item, settings, channel, message, ct);
            (error is null ? sent : failed).Add(outcome(error));
        }
        return new BulkSendResult.Done(new BulkSendSummary(sent, failed, skipped));
    }

    // Saves each Invoice on its own so a delivery ePost already billed stays recorded when a later one fails.
    private async Task<string?> SendAndSaveAsync(
        BulkSendItem item,
        SettingsEntity settings,
        DeliveryChannel channel,
        InvoiceMessage message,
        CancellationToken ct
    )
    {
        try
        {
            var error = await SendOneAsync(item.Invoice, item.Recipient, settings, channel, message, ct);
            if (error is null)
            {
                await db.SaveChangesAsync(ct);
            }
            return error;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Sending Invoice {InvoiceId} failed", item.Invoice.Id);
            return "Die Rechnung konnte nicht versendet werden.";
        }
    }

    /// <summary>Returns a German error, or null after adding the Sent status change to <paramref name="invoice"/> (the caller saves).</summary>
    private async Task<string?> SendOneAsync(
        Invoice invoice,
        PersonEntity recipient,
        SettingsEntity settings,
        DeliveryChannel channel,
        InvoiceMessage message,
        CancellationToken ct
    )
    {
        var invalid =
            InvoiceStatusRules.ValidateSend(invoice)
            ?? (channel == DeliveryChannel.Email ? InvoiceRules.ValidateMessage(message.Subject, message.Text) : null);
        if (invalid is not null)
        {
            return invalid;
        }
        var option = DeliveryChannelRules.Option(settings, recipient, channel);
        if (option.BlockedReason is { } blocked)
        {
            return blocked;
        }
        var pdf = await storage.GetAsync(invoice.Id, ct);
        if (pdf is null)
        {
            return "Das PDF der Rechnung wurde nicht gefunden.";
        }

        var sender = senders.Single(s => s.Backend == option.Backend);
        var result = await sender.SendAsync(settings, recipient, invoice.Id, message, pdf, ct);
        if (result.Error is not null)
        {
            return result.Error;
        }
        return InvoiceStatusRules.Send(invoice, result.Remark, time.GetUtcNow().UtcDateTime);
    }

    private async Task<SettingsEntity> LoadSettingsAsync(CancellationToken ct) =>
        await db.Settings.FindAsync([SettingsEntity.SingletonId], ct) ?? new();

    private async Task<List<BulkSendItem>> PlanBulkSendAsync(SettingsEntity settings, CancellationToken ct)
    {
        var invoices = await db.Invoices.Include(i => i.StatusChanges).ToListAsync(ct);
        var wanted = invoices.Select(i => i.PersonId).Distinct().ToList();
        var people = await db
            .Persons.AsNoTracking()
            .Where(p => wanted.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);
        return BulkSendRules.Plan(invoices.Select(i => (i, people[i.PersonId])), settings);
    }
}
