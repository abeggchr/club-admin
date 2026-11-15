using backend.Accounting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonEntity = backend.Person.Person;

namespace backend.Invoicing;

[ApiController]
[Route("api")]
public class InvoicingController(AppDbContext db) : ControllerBase
{
    [HttpGet("text-templates")]
    public async Task<IActionResult> GetTextTemplates() =>
        Ok(await db.TextTemplates.OrderBy(t => t.Subject).ThenBy(t => t.Id).ToListAsync());

    [HttpPost("text-templates")]
    public async Task<IActionResult> CreateTextTemplate(TextTemplateInput input)
    {
        if (TextTemplateRules.Validate(input.Subject, input.Text, input.Greetings) is { } error)
        {
            return BadRequest(new { error });
        }
        var template = ApplyInput(new TextTemplate(), input);
        db.TextTemplates.Add(template);
        await db.SaveChangesAsync();
        return Created($"/api/text-templates/{template.Id}", template);
    }

    [HttpPut("text-templates/{id:int}")]
    public async Task<IActionResult> UpdateTextTemplate(int id, TextTemplateInput input)
    {
        if (TextTemplateRules.Validate(input.Subject, input.Text, input.Greetings) is { } error)
        {
            return BadRequest(new { error });
        }
        var template = await db.TextTemplates.FindAsync(id);
        if (template is null)
        {
            return NotFound();
        }
        ApplyInput(template, input);
        await db.SaveChangesAsync();
        return Ok(template);
    }

    [HttpDelete("text-templates/{id:int}")]
    public async Task<IActionResult> DeleteTextTemplate(int id)
    {
        var template = await db.TextTemplates.FindAsync(id);
        if (template is null)
        {
            return NotFound();
        }
        db.TextTemplates.Remove(template);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices()
    {
        var invoices = await db.Invoices.WithDetails().OrderByDescending(i => i.Id).ToListAsync();
        var people = await PeopleByIdAsync(invoices.Select(i => i.PersonId));
        return Ok(
            invoices.Select(i => InvoiceMapping.ToInvoiceListItem(i, people[i.PersonId])).ToList()
        );
    }

    [HttpGet("invoices/{id:int}")]
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

    [HttpPost("invoices")]
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

    // How many Personen the Jahresbeiträge run considers and how many Rechnungen it would create.
    [HttpGet("invoices/summary")]
    public async Task<IActionResult> GetInvoiceSummary(CancellationToken ct)
    {
        var invoices = await db.Invoices.WithDetails().ToListAsync(ct);
        return Ok(InvoiceRules.Summarize(invoices));
    }

    [HttpGet("invoices/annual-fees/preview")]
    public async Task<IActionResult> PreviewAnnualFees(CancellationToken ct)
    {
        var settings = await db.Settings.FindAsync([Settings.Settings.SingletonId], ct);
        var persons = await db.Persons.AsNoTracking().ToListAsync(ct);
        var discounts = await db
            .MembershipDiscounts.AsNoTracking()
            .ToDictionaryAsync(d => d.PersonId, ct);
        var plan = MembershipFeeRules.Plan(persons, settings!.StartDate, discounts);
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

    [HttpPost("invoices/annual-fees")]
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

    // Saves (adds or replaces) the one Ermässigung of a Person.
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

    [HttpGet("invoices/{id:int}/pdf")]
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

    // Copies an already validated Textvorlage input onto the entity, trimmed.
    private static TextTemplate ApplyInput(TextTemplate template, TextTemplateInput input)
    {
        template.Subject = input.Subject!.Trim();
        template.Text = input.Text!.Trim();
        template.Greetings = input.Greetings!.Trim();
        return template;
    }
}

public record MembershipDiscountInput(int? PersonId, int? Percent, string? Text);

public record TextTemplateInput(string? Subject, string? Text, string? Greetings);

public record InvoiceLineInput(string? Text, decimal Amount, int AccountNumber);

public record AnnualFeesInput(int? TemplateId);

public record InvoiceInput(
    int? PersonId,
    int? TemplateId,
    List<InvoiceLineInput>? Lines,
    bool OpenAmount = false
);
