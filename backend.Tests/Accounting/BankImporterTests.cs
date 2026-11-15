using backend.Accounting;
using backend.Invoicing;
using backend.Tests.Invoicing;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static backend.Tests.Accounting.CamtTestHelpers;
using PersonEntity = backend.Person.Person;

namespace backend.Tests.Accounting;

/// <summary>
/// The bank import against a real database (local PostgreSQL per README; CI provides a Postgres
/// service): what is stored for a payment that covers an Invoice.
/// </summary>
public class BankImporterTests : IClassFixture<LocalPostgresFixture>
{
    private const string ClubIban = "CH9300762011623852957";

    private static readonly ChartOfAccounts Chart = ChartOfAccounts.Parse(
        """
        {
          "income": [
            { "number": 3400, "name": "Beiträge" },
            { "number": 3600, "name": "Spenden" }
          ],
          "expense": []
        }
        """
    );

    private readonly LocalPostgresFixture _fixture;

    public BankImporterTests(LocalPostgresFixture fixture) => _fixture = fixture;

    private AppDbContext Database()
    {
        _fixture.SkipUnlessAvailable();
        return new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_fixture.ConnectionString).Options
        );
    }

    /// <summary>Settings, a Person and one open Invoice of 100.00 with the reference RF340042.</summary>
    private static async Task<int> SeedOpenInvoice(AppDbContext db)
    {
        db.Settings.RemoveRange(db.Settings);
        db.Settings.Add(
            new backend.Settings.Settings
            {
                OpeningBalance = 0m,
                StartDate = new DateOnly(2026, 1, 1),
                Iban = ClubIban,
                Name = "SC Wettingen",
                Address = "Sportweg 12",
                PostalCode = "5430",
                City = "Wettingen",
            }
        );
        var person = new PersonEntity { FirstName = "Hans", LastName = "Muster" };
        db.Persons.Add(person);
        await db.SaveChangesAsync();
        var invoice = new Invoice
        {
            PersonId = person.Id,
            Reference = "RF340042",
            Lines = [new InvoiceLine { Text = "Jahresbeitrag", Amount = 100m, AccountNumber = 3400 }],
            StatusChanges = [new InvoiceStatusChange { Status = InvoiceStatus.Created, Timestamp = DateTime.UtcNow }],
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice.Id;
    }

    [Fact]
    public async Task A_payment_that_covers_the_Rechnung_stores_the_Transaction_marks_it_Paid_and_records_PaidBy()
    {
        await using var db = Database();
        var invoiceId = await SeedOpenInvoice(db);
        var file = CamtFile(
            "stmt",
            ClubIban,
            TxDtlsNtryWithReference("100.00", "CRDT", "2026-02-10", "TX-PAID", "RF340042")
        );

        var outcome = await new BankImporter(db, Chart).ImportAsync(
            System.Text.Encoding.UTF8.GetBytes(file),
            "stmt.xml"
        );

        var imported = Assert.IsType<BankImportOutcome.Imported>(outcome);
        Assert.Equal(1, imported.ImportedCount);
        Assert.Equal([invoiceId], imported.BookedInvoices);

        // A fresh context reads only what was committed.
        await using var check = Database();
        var movement = await check
            .Transactions.Include(t => t.Entries)
            .SingleAsync(t => t.BankReference == "TX-PAID");
        var entry = Assert.Single(movement.Entries);
        Assert.Equal(3400, entry.AccountNumber);
        var invoice = await check
            .Invoices.Include(i => i.StatusChanges)
            .SingleAsync(i => i.Id == invoiceId);
        Assert.Contains(invoice.StatusChanges, s => s.Status == InvoiceStatus.Paid);
        Assert.Equal(movement.Id, invoice.PaidBy);
    }
}
