using backend.Accounting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonEntity = backend.Person.Person;

namespace backend.Invoicing;

[ApiController]
[Route("api/invoices")]
public class InvoicingController(AppDbContext db, ILogger<InvoicingController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetInvoices()
    {
        var invoices = await db.Invoices.WithDetails().OrderByDescending(i => i.Id).ToListAsync();
        var people = await PeopleByIdAsync(invoices.Select(i => i.PersonId));
        return Ok(
            invoices.Select(i => InvoiceMapping.ToInvoiceListItem(i, people[i.PersonId])).ToList()
        );
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetInvoice(int id)
    {
        var invoice = await db.Invoices.WithDetails().FirstOrDefaultAsync(i => i.Id == id);
        if (invoice is null)
        {
            return NotFound();
        }
        var people = await PeopleByIdAsync([invoice.PersonId]);
        return Ok(InvoiceMapping.ToInvoiceDetail(invoice, people[invoice.PersonId]));
    }

    // Sends the Invoice PDF by the chosen Delivery Channel; records a Sent status change only when the channel accepted it.
    [HttpPost("{id:int}/send")]
    public async Task<IActionResult> SendInvoice(
        int id,
        InvoiceSendRequest request,
        [FromServices] InvoiceDispatcher dispatcher,
        CancellationToken ct
    )
    {
        var invoice = await db
            .Invoices.Include(i => i.Lines)
            .Include(i => i.StatusChanges)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null)
        {
            return NotFound();
        }
        var person = (await PeopleByIdAsync([invoice.PersonId]))[invoice.PersonId];
        var settings = await db.Settings.FindAsync([Settings.Settings.SingletonId], ct) ?? new();
        var error = await dispatcher.SendAsync(
            invoice,
            person,
            settings,
            request.Channel,
            request.Message,
            DateTime.UtcNow,
            ct
        );
        if (error is not null)
        {
            return BadRequest(new { error });
        }
        await db.SaveChangesAsync(ct);
        return Ok(InvoiceMapping.ToInvoiceDetail(invoice, person));
    }

    // How many unsent Invoices a bulk send would deliver by email, by post, or skip.
    [HttpGet("bulk-send/preview")]
    public async Task<IActionResult> PreviewBulkSend(
        [FromServices] IEnumerable<IInvoiceSender> senders,
        CancellationToken ct
    )
    {
        var plan = await PlanBulkSendAsync(senders, ct);
        return Ok(BulkSendRules.Preview(plan));
    }

    // Sends every unsent Invoice by the channel BulkSendRules picks, one at a time; a failure never stops the rest.
    [HttpPost("bulk-send")]
    public async Task<IActionResult> BulkSend(
        InvoiceSendRequest request,
        [FromServices] IEnumerable<IInvoiceSender> senders,
        [FromServices] InvoiceDispatcher dispatcher,
        CancellationToken ct
    )
    {
        var plan = await PlanBulkSendAsync(senders, ct);
        if (BulkSendRules.ValidateMessage(plan, request.Subject, request.Text) is { } invalid)
        {
            return BadRequest(new { error = invalid });
        }
        var settings = await db.Settings.FindAsync([Settings.Settings.SingletonId], ct) ?? new();
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
            var error = await SendOneAsync(dispatcher, item, settings, channel, request.Message, ct);
            (error is null ? sent : failed).Add(outcome(error));
        }
        return Ok(new BulkSendSummary(sent, failed, skipped));
    }

    // Saves each Invoice on its own so a letter already handed to ePost stays recorded when a later one fails.
    private async Task<string?> SendOneAsync(
        InvoiceDispatcher dispatcher,
        BulkSendItem item,
        Settings.Settings settings,
        DeliveryChannel channel,
        InvoiceMessage message,
        CancellationToken ct
    )
    {
        try
        {
            var error = await dispatcher.SendAsync(item.Invoice, item.Recipient, settings, channel, message, DateTime.UtcNow, ct);
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

    private async Task<List<BulkSendItem>> PlanBulkSendAsync(
        IEnumerable<IInvoiceSender> senders,
        CancellationToken ct
    )
    {
        var settings = await db.Settings.FindAsync([Settings.Settings.SingletonId], ct) ?? new();
        var invoices = await db.Invoices.Include(i => i.StatusChanges).ToListAsync(ct);
        var people = await PeopleByIdAsync(invoices.Select(i => i.PersonId));
        return BulkSendRules.Plan(invoices.Select(i => (i, people[i.PersonId])), settings, senders);
    }

    // Cancels (storniert) an open Invoice (Created or Sent): records a Cancelled status change.
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> CancelInvoice(int id, CancellationToken ct)
    {
        var invoice = await db
            .Invoices.Include(i => i.Lines)
            .Include(i => i.StatusChanges)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null)
        {
            return NotFound();
        }
        if (InvoiceStatusRules.Cancel(invoice, DateTime.UtcNow) is { } error)
        {
            return BadRequest(new { error });
        }
        await db.SaveChangesAsync(ct);
        var person = (await PeopleByIdAsync([invoice.PersonId]))[invoice.PersonId];
        return Ok(InvoiceMapping.ToInvoiceDetail(invoice, person));
    }

    [HttpPost]
    public async Task<IActionResult> CreateInvoice(
        InvoiceInput input,
        [FromServices] InvoiceCreator creator,
        [FromServices] ChartOfAccounts chart,
        CancellationToken ct
    )
    {
        var lines = (input.Lines ?? [])
            .Select(l => new InvoiceLineDraft(l.Text, l.Amount, l.AccountNumber))
            .ToList();
        if (InvoiceRules.Validate(input.PersonId, input.TemplateId, lines, chart) is { } error)
        {
            return BadRequest(new { error });
        }
        var person = await db.Persons.FindAsync([input.PersonId!.Value], ct);
        if (person is null)
        {
            return BadRequest(new { error = "Die Person existiert nicht." });
        }
        var template = await db.TextTemplates.FindAsync([input.TemplateId!.Value], ct);
        if (template is null)
        {
            return BadRequest(new { error = "Die Textvorlage existiert nicht." });
        }

        var settings = await db.Settings.FindAsync([Settings.Settings.SingletonId], ct);
        var (creditor, creditorError) = QrBillCreditor.Resolve(settings);
        if (creditor is null)
        {
            return BadRequest(new { error = creditorError });
        }

        var result = await creator.CreateAsync(
            person,
            template,
            lines,
            creditor,
            input.OpenAmount,
            ct
        );
        if (result.Invoice is not { } invoice)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = result.Error }
            );
        }
        return Created(
            $"/api/invoices/{invoice.Id}",
            InvoiceMapping.ToInvoiceDetail(invoice, person)
        );
    }

    // How many Persons the Annual Fees run considers and how many Invoices it would create.
    [HttpGet("summary")]
    public async Task<IActionResult> GetInvoiceSummary(CancellationToken ct)
    {
        var invoices = await db.Invoices.WithDetails().ToListAsync(ct);
        return Ok(InvoiceRules.Summarize(invoices));
    }

    [HttpGet("annual-fees/preview")]
    public async Task<IActionResult> PreviewAnnualFees(CancellationToken ct)
    {
        var settings = await db.Settings.FindAsync([Settings.Settings.SingletonId], ct);
        if (settings is null)
        {
            return BadRequest(new { error = "Bitte zuerst die Einstellungen erfassen" });
        }
        var persons = await db.Persons.AsNoTracking().ToListAsync(ct);
        var discounts = await db
            .MembershipDiscounts.AsNoTracking()
            .ToDictionaryAsync(d => d.PersonId, ct);
        var plan = MembershipFeeRules.Plan(persons, settings.StartDate, discounts);
        return Ok(
            new
            {
                consideredPersons = persons.Count(p =>
                    MembershipFeeRules.IsActive(p, settings.StartDate)
                ),
                plannedInvoices = plan.Invoices.Count,
            }
        );
    }

    [HttpPost("annual-fees")]
    public async Task<IActionResult> CreateAnnualFees(
        AnnualFeesInput input,
        [FromServices] InvoiceCreator creator,
        CancellationToken ct
    )
    {
        if (input.TemplateId is null)
        {
            return BadRequest(new { error = "Die Textvorlage ist erforderlich." });
        }
        var template = await db.TextTemplates.FindAsync([input.TemplateId.Value], ct);
        if (template is null)
        {
            return BadRequest(new { error = "Die Textvorlage existiert nicht." });
        }
        var settings = await db.Settings.FindAsync([Settings.Settings.SingletonId], ct);
        var (creditor, creditorError) = QrBillCreditor.Resolve(settings);
        if (creditor is null)
        {
            return BadRequest(new { error = creditorError });
        }

        var persons = await db.Persons.AsNoTracking().ToListAsync(ct);
        var discounts = await db
            .MembershipDiscounts.AsNoTracking()
            .ToDictionaryAsync(d => d.PersonId, ct);
        var plan = MembershipFeeRules.Plan(persons, settings!.StartDate, discounts);
        var summary = await BulkInvoiceRunner.RunAsync(
            plan,
            (invoice, token) =>
                creator.CreateAsync(
                    invoice.Recipient,
                    template,
                    invoice.Lines,
                    creditor,
                    false,
                    token
                ),
            ct
        );
        return Ok(summary);
    }

    [HttpGet("membership-discounts")]
    public async Task<IActionResult> GetMembershipDiscounts() =>
        Ok(await db.MembershipDiscounts.AsNoTracking().OrderBy(d => d.PersonId).ToListAsync());

    // Saves (adds or replaces) the one Discount of a Person.
    [HttpPut("membership-discounts")]
    public async Task<IActionResult> SaveMembershipDiscount(
        MembershipDiscountInput input,
        CancellationToken ct
    )
    {
        if (
            MembershipDiscountRules.Validate(input.PersonId, input.Percent, input.Text) is { } error
        )
        {
            return BadRequest(new { error });
        }
        if (!await db.Persons.AnyAsync(p => p.Id == input.PersonId, ct))
        {
            return BadRequest(new { error = "Die Person existiert nicht." });
        }
        var discount = await db.MembershipDiscounts.FindAsync([input.PersonId!.Value], ct);
        if (discount is null)
        {
            discount = new MembershipDiscount { PersonId = input.PersonId.Value };
            db.MembershipDiscounts.Add(discount);
        }
        discount.Percent = input.Percent!.Value;
        discount.Text = input.Text!.Trim();
        await db.SaveChangesAsync(ct);
        return Ok(discount);
    }

    [HttpDelete("membership-discounts/{personId:int}")]
    public async Task<IActionResult> DeleteMembershipDiscount(int personId, CancellationToken ct)
    {
        var discount = await db.MembershipDiscounts.FindAsync([personId], ct);
        if (discount is null)
        {
            return NotFound();
        }
        db.MembershipDiscounts.Remove(discount);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> GetInvoicePdf(
        int id,
        [FromServices] IInvoiceStorage storage,
        CancellationToken ct
    )
    {
        if (!await db.Invoices.AnyAsync(i => i.Id == id, ct))
        {
            return NotFound();
        }
        var pdf = await storage.GetAsync(id, ct);
        if (pdf is null)
        {
            return NotFound();
        }
        // Set directly: File with a filename would emit "attachment".
        Response.Headers.ContentDisposition =
            $"inline; filename=\"Rechnung-{InvoiceRules.FormatNumber(id)}.pdf\"";
        return File(pdf, "application/pdf");
    }

    private async Task<Dictionary<int, PersonEntity>> PeopleByIdAsync(IEnumerable<int> ids)
    {
        var wanted = ids.Distinct().ToList();
        return await db
            .Persons.AsNoTracking()
            .Where(p => wanted.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);
    }
}

/// <summary>One Invoice in the bulk send summary; the reason is null for a sent Invoice.</summary>
public record BulkSendOutcome(string Number, string Person, string? Reason);

/// <summary>The result of a bulk send: which Invoices were sent, failed or skipped.</summary>
public record BulkSendSummary(
    IReadOnlyList<BulkSendOutcome> Sent,
    IReadOnlyList<BulkSendOutcome> Failed,
    IReadOnlyList<BulkSendOutcome> Skipped
);

public record MembershipDiscountInput(int? PersonId, int? Percent, string? Text);

public record InvoiceLineInput(string? Text, decimal Amount, int AccountNumber);

public record AnnualFeesInput(int? TemplateId);

public record InvoiceInput(
    int? PersonId,
    int? TemplateId,
    List<InvoiceLineInput>? Lines,
    bool OpenAmount = false
);
