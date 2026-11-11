using backend.Invoicing;
using backend.Person;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PersonEntity = backend.Person.Person;

namespace backend.Tests.Invoicing;

/// <summary>
/// Needs a real database (local PostgreSQL per README); when no local database is reachable the tests
/// fail with a clear message.
/// </summary>
public class InvoiceCreatorTests : IClassFixture<LocalPostgresFixture>
{
    private static readonly QrBillCreditor Club = new(
        "CH9300762011623852957",
        "SC Wettingen",
        "Sportweg 12",
        "5430",
        "Wettingen"
    );

    public InvoiceCreatorTests(LocalPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private readonly LocalPostgresFixture _fixture;

    private async Task<AppDbContext> Database()
    {
        if (_fixture.ConnectionString == "")
            throw new InvalidOperationException(
                "No reachable PostgreSQL for the database-dependent creation tests. Expected the developer "
                + "Postgres from the README at localhost:5432 (postgres/secret) or the host's Postgres at "
                + "host.docker.internal:5432 (sandbox)."
            );
        // The fixture already ran MigrateAsync on the fresh database, so tests read exactly the schema
        // the production migrations create.
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_fixture.ConnectionString).Options);
    }

    private static PersonEntity CreatePerson(AppDbContext db)
    {
        var person = new PersonEntity { FirstName = "Hans", LastName = "Muster", Street = "Dorfstrasse", Number = "1", PostalCode = "8000", City = "Zürich", Country = "Schweiz" };
        db.Persons.Add(person);
        db.SaveChanges();
        return person;
    }

    [Fact]
    public async Task Creating_a_Rechnung_stores_the_QR_bill_Reference_in_the_database()
    {
        await using var db = await Database();
        var person = CreatePerson(db);
        var creator = new InvoiceCreator(db, new InMemoryInvoiceStorage(), NullLogger<InvoiceCreator>.Instance);

        var result = await creator.CreateAsync(
            person,
            new TextTemplate { Subject = "Jahresbeitrag 2026", Text = "Bitte zahlen Sie.", Greetings = "Freundliche Grüsse" },
            [new InvoiceLineDraft("Jahresbeitrag", 100m, 3400)],
            Club
        );

        Assert.Null(result.Error);
        var invoice = result.Invoice!;

        // Verified in the database, not just on the PDF: a fresh context reads only what was committed.
        await using var check = await Database();
        var stored = await check.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoice.Id);
        // Worked example from QrBillSlipTests: "0042" -> "RF340042" (RF + 34 + Rechnungsnummer 0042).
        Assert.Equal($"RF34{invoice.Id:D4}", stored.Reference);
    }

    [Fact]
    public async Task Existing_Rechnungen_are_backfilled_with_the_empty_Reference()
    {
        await using var db = await Database();
        // Simulates a Rechnung created before the Reference column existed: no reference set, so the
        // backfilled '' (the migration's defaultValue) is what a fresh database would hold.
        var person = CreatePerson(db);
        var old = new Invoice
        {
            PersonId = person.Id,
            Lines = [new InvoiceLine { Text = "Jahresbeitrag", Amount = 100m, AccountNumber = 3400, Position = 0 }],
            StatusChanges = [new InvoiceStatusChange { Status = InvoiceStatus.Created, Timestamp = DateTime.UtcNow }],
        };
        db.Invoices.Add(old);
        await db.SaveChangesAsync();
        var id = old.Id;

        await using var check = await Database();
        var stored = await check.Invoices.AsNoTracking().SingleAsync(i => i.Id == id);
        // The backfill value '' can never match a payment by exact Gläubigerreferenz.
        Assert.Equal("", stored.Reference);
    }
}
