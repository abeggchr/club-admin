using PersonEntity = backend.Person.Person;
using SettingsEntity = backend.Settings.Settings;

namespace backend.Invoicing;

/// <summary>
/// The single entry point for sending an Invoice: checks the status, picks the Delivery Channel's
/// strategy, loads the PDF and appends the Sent status change when the sender succeeded.
/// </summary>
public class InvoiceDispatcher(IEnumerable<IInvoiceSender> senders, IInvoiceStorage storage)
{
    /// <summary>Returns a German error, or null after adding the Sent status change to <paramref name="invoice"/> (the caller saves).</summary>
    public async Task<string?> SendAsync(
        Invoice invoice,
        PersonEntity recipient,
        SettingsEntity settings,
        DeliveryChannel channel,
        InvoiceMessage message,
        DateTime now,
        CancellationToken ct
    )
    {
        if (
            InvoiceRules.ValidateSend(
                InvoiceRules.LatestStatus(invoice),
                message.Subject,
                message.Text
            ) is { } invalid
        )
        {
            return invalid;
        }
        var sender = senders.FirstOrDefault(s => s.Channel == channel);
        if (sender is null || !sender.IsConfigured(settings))
        {
            return "E-Mail ist nicht eingerichtet.";
        }
        if (!sender.CanDeliverTo(recipient))
        {
            return "Die Person hat keine E-Mail-Adresse.";
        }
        var pdf = await storage.GetAsync(invoice.Id, ct);
        if (pdf is null)
        {
            return "Das PDF der Rechnung wurde nicht gefunden.";
        }

        var result = await sender.SendAsync(settings, recipient, invoice.Id, message, pdf, ct);
        if (result.Error is not null)
        {
            return result.Error;
        }
        invoice.StatusChanges.Add(
            new InvoiceStatusChange
            {
                Status = InvoiceStatus.Sent,
                Timestamp = now,
                Remark = result.Remark,
            }
        );
        return null;
    }
}
