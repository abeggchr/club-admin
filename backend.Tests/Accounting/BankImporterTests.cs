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

    /// <summary>Empties the tables the import reads and writes; the tests of this class share one database.</summary>
    private static async Task Reset(AppDbContext db)
    {
        await db.Invoices.ExecuteDeleteAsync();
        await db.Transactions.ExecuteDeleteAsync();
        await db.Persons.ExecuteDeleteAsync();
        await db.Settings.ExecuteDeleteAsync();
    }

    /// <summary>Settings, a Person and one open Invoice of 100.00 with the reference RF340042.</summary>
    private static async Task<int> SeedOpenInvoice(AppDbContext db)
    {
        await Reset(db);
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

        var outcome = await Import(db, Payment("100.00", "TX-PAID"));

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

    private static string Payment(string amount, string txRef) =>
        CamtFile(
            "stmt",
            ClubIban,
            TxDtlsNtryWithReference(amount, "CRDT", "2026-02-10", txRef, "RF340042")
        );

    private static Task<BankImportOutcome> Import(AppDbContext db, string file) =>
        new BankImporter(db, Chart).ImportAsync(System.Text.Encoding.UTF8.GetBytes(file), "stmt.xml");

    [Fact]
    public async Task Without_saved_Settings_nothing_is_imported()
    {
        await using var db = Database();
        await Reset(db);

        var outcome = await Import(db, Payment("100.00", "TX-1"));

        Assert.IsType<BankImportOutcome.SettingsMissing>(outcome);
        await using var check = Database();
        Assert.Empty(await check.Transactions.ToListAsync());
    }

    [Fact]
    public async Task A_rejected_file_stores_nothing()
    {
        await using var db = Database();
        await SeedOpenInvoice(db);
        var otherIban = CamtFile(
            "stmt",
            "CH9300762011623852951",
            TxDtlsNtryWithReference("100.00", "CRDT", "2026-02-10", "TX-1", "RF340042")
        );

        var outcome = await Import(db, otherIban);

        Assert.IsType<BankImportOutcome.Rejected>(outcome);
        await using var check = Database();
        Assert.Empty(await check.Transactions.ToListAsync());
        var invoice = await check.Invoices.Include(i => i.StatusChanges).SingleAsync();
        Assert.DoesNotContain(invoice.StatusChanges, s => s.Status == InvoiceStatus.Paid);
    }

    [Fact]
    public async Task Importing_the_same_file_again_skips_it_and_keeps_the_Rechnung_Paid_once()
    {
        await using var db = Database();
        var invoiceId = await SeedOpenInvoice(db);
        var file = Payment("100.00", "TX-PAID");
        await Import(db, file);

        await using var again = Database();
        var outcome = Assert.IsType<BankImportOutcome.Imported>(await Import(again, file));

        Assert.Equal(0, outcome.ImportedCount);
        Assert.Equal(1, outcome.SkippedDuplicates);
        await using var check = Database();
        Assert.Single(await check.Transactions.ToListAsync());
        var invoice = await check.Invoices.Include(i => i.StatusChanges).SingleAsync(i => i.Id == invoiceId);
        Assert.Single(invoice.StatusChanges, s => s.Status == InvoiceStatus.Paid);
    }

    [Fact]
    public async Task An_overpayment_books_the_excess_to_Spenden_and_marks_the_Rechnung_paid()
    {
        await using var db = Database();
        var invoiceId = await SeedOpenInvoice(db);

        var outcome = Assert.IsType<BankImportOutcome.Imported>(
            await Import(db, Payment("150.00", "TX-OVER"))
        );

        Assert.Equal([invoiceId], outcome.BookedInvoices);
        await using var check = Database();
        var movement = await check.Transactions.Include(t => t.Entries).SingleAsync();
        Assert.Equal(
            [(3400, 100m), (3600, 50m)],
            movement.Entries.OrderBy(e => e.AccountNumber).Select(e => (e.AccountNumber, e.Amount))
        );
        var invoice = await check.Invoices.Include(i => i.StatusChanges).SingleAsync();
        Assert.Single(invoice.StatusChanges, s => s.Status == InvoiceStatus.Paid);
        Assert.Equal(movement.Id, invoice.PaidBy);
    }
}
