using System.Text.Json.Serialization;
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
    [property: JsonRequired] int? PayerId
);

public static class PersonEndpoints
{
    public static async Task<IResult> UpdateAsync(int id, PersonUpdate update, AppDbContext db)
    {
        if (
            string.IsNullOrWhiteSpace(update.FirstName)
            || string.IsNullOrWhiteSpace(update.LastName)
        )
        {
            return Results.BadRequest(new { error = "Vorname und Nachname sind erforderlich." });
        }

        var person = await db.Persons.FindAsync(id);
        if (person is null)
        {
            return Results.NotFound();
        }

        var links = await PayerLinksAsync(db);
        if (PayerRules.ValidateSet(id, update.PayerId, links) is { } payerError)
        {
            return Results.BadRequest(new { error = payerError });
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

        await db.SaveChangesAsync();
        return Results.Ok(person);
    }

    public static async Task<IResult> DeleteAsync(int id, AppDbContext db)
    {
        var person = await db.Persons.FindAsync(id);
        if (person is null)
        {
            return Results.NotFound();
        }

        if (PayerRules.ValidateDelete(id, await PayerLinksAsync(db)) is { } error)
        {
            return Results.BadRequest(new { error });
        }

        db.Persons.Remove(person);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<List<PayerLink>> PayerLinksAsync(AppDbContext db) =>
        await db.Persons.AsNoTracking().Select(p => new PayerLink(p.Id, p.PayerId)).ToListAsync();
}
