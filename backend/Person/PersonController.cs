using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Person;

public record PersonUpdate(
    string FirstName,
    string LastName,
    MembershipType Membership,
    DateOnly? DateOfBirth,
    DateOnly? JoinedOn,
    DateOnly? LeftOn,
    string? Email,
    bool IsMainPayer,
    string? MobilePhone,
    string? LandlinePhone,
    string? Street,
    string? Number,
    string? PostalCode,
    string? City,
    string? Country,
    string? Notes,
    [property: JsonRequired] int? PayerId,
    bool ReceivesClubNews
);

[ApiController]
[Route("api/person")]
public class PersonController(AppDbContext db, TimeProvider time) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPersons() =>
        Ok(await db.Persons.OrderBy(p => p.Id).ToListAsync());

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var persons = await db.Persons.AsNoTracking().ToListAsync(ct);
        var summary = PersonSummaryRules.Summarize(
            persons,
            backend.Invoicing.InvoiceTime.Today(time.GetUtcNow().UtcDateTime)
        );
        return Ok(
            new
            {
                total = summary.Total,
                byMembership = summary.ByMembership.Select(m => new
                {
                    membership = m.Membership.ToString().ToLowerInvariant(),
                    m.Count,
                }),
            }
        );
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync(int id, PersonUpdate update)
    {
        if (
            string.IsNullOrWhiteSpace(update.FirstName)
            || string.IsNullOrWhiteSpace(update.LastName)
        )
        {
            return BadRequest(new { error = "Vorname und Nachname sind erforderlich." });
        }

        var person = await db.Persons.FindAsync(id);
        if (person is null)
        {
            return NotFound();
        }

        var links = await PayerLinksAsync();
        if (PayerRules.ValidateSet(id, update.PayerId, update.IsMainPayer, links) is { } payerError)
        {
            return BadRequest(new { error = payerError });
        }

        person.FirstName = update.FirstName.Trim();
        person.LastName = update.LastName.Trim();
        person.Membership = update.Membership;
        person.DateOfBirth = update.DateOfBirth;
        person.JoinedOn = update.JoinedOn;
        person.LeftOn = update.LeftOn;
        person.Email = update.Email;
        person.IsMainPayer = update.IsMainPayer;
        person.MobilePhone = update.MobilePhone;
        person.LandlinePhone = update.LandlinePhone;
        person.Street = update.Street;
        person.Number = update.Number;
        person.PostalCode = update.PostalCode;
        person.City = update.City;
        person.Country = update.Country;
        person.Notes = update.Notes;
        person.PayerId = update.PayerId;
        person.ReceivesClubNews = update.ReceivesClubNews;

        await db.SaveChangesAsync();
        return Ok(person);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id)
    {
        var person = await db.Persons.FindAsync(id);
        if (person is null)
        {
            return NotFound();
        }

        if (PayerRules.ValidateDelete(id, await PayerLinksAsync()) is { } error)
        {
            return BadRequest(new { error });
        }

        db.Persons.Remove(person);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("import")]
    public async Task<IActionResult> ImportPersons(IFormFile file)
    {
        List<Person> persons;
        try
        {
            using var reader = new StreamReader(file.OpenReadStream());
            persons = PersonCsvImporter.Parse(reader);
        }
        catch (FormatException e)
        {
            return BadRequest(new { error = e.Message });
        }

        db.Persons.AddRange(persons);
        await db.SaveChangesAsync();
        return Ok(new { imported = persons.Count });
    }

    [HttpGet("mailing-list")]
    public async Task<IActionResult> GetMailingList(CancellationToken ct)
    {
        var settings = await db.Settings.FindAsync([Settings.Settings.SingletonId], ct);
        var persons = await db.Persons.AsNoTracking().ToListAsync(ct);
        // Recipients: every active Person plus every Person flagged "Newsletter".
        var recipients = ClubNewsRecipientRules.Recipients(persons, settings!.StartDate).ToList();
        var csv = MailingListCsv.Build(MailingListRules.BuildRows(recipients));
        // BOM (EF BB BF) so Excel opens the UTF-8 file with the correct encoding; the CSV
        // string is BOM-free, like the buildPersonCsv string of the Persons export.
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var content = System.Text.Encoding.UTF8.GetBytes(csv);
        var bytes = new byte[bom.Length + content.Length];
        bom.CopyTo(bytes, 0);
        content.CopyTo(bytes, bom.Length);
        return File(bytes, "text/csv; charset=utf-8", "versandliste.csv");
    }

    [HttpGet("jubilees")]
    public async Task<IActionResult> GetJubilees()
    {
        var settings = await db.Settings.FindAsync(Settings.Settings.SingletonId);
        if (settings is null)
        {
            return NotFound();
        }

        var persons = await db.Persons.ToListAsync();
        var jubilees = JubileeRules.Compute(
            persons,
            settings.StartDate,
            backend.Invoicing.InvoiceTime.Today(time.GetUtcNow().UtcDateTime)
        );
        return Ok(
            new
            {
                clubYearStartYear = jubilees.ClubYearStartYear,
                anniversaries = jubilees.Anniversaries.Select(a => new
                {
                    personId = a.PersonId,
                    years = a.Years,
                    firstName = a.FirstName,
                    lastName = a.LastName,
                    birthdate = a.Birthdate,
                }),
                jubilees = jubilees.JubileesRows.Select(j => new
                {
                    personId = j.PersonId,
                    years = j.Years,
                    firstName = j.FirstName,
                    lastName = j.LastName,
                    entryDate = j.EntryDate,
                }),
            }
        );
    }

    private async Task<List<PayerLink>> PayerLinksAsync() =>
        await db.Persons.AsNoTracking().Select(p => new PayerLink(p.Id, p.PayerId, p.IsMainPayer)).ToListAsync();
}
