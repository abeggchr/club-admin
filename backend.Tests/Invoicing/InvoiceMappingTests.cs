using backend.Invoicing;
using PersonEntity = backend.Person.Person;

namespace backend.Tests.Invoicing;

public class InvoiceMappingTests
{
    private static PersonEntity Person => new() { Id = 1, FirstName = "Hans", LastName = "Muster" };

    private static Invoice Invoice(string reference) =>
        new()
        {
            Id = 42,
            PersonId = 1,
            Reference = reference,
            Lines = [new InvoiceLine { Text = "Jahresbeitrag", Amount = 100m, AccountNumber = 3400, Position = 0 }],
            StatusChanges =
            [
                new InvoiceStatusChange { Status = InvoiceStatus.Created, Timestamp = new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc) },
            ],
        };

    [Fact]
    public void Detail_shows_the_Gläubigerreferenz()
    {
        var detail = InvoiceMapping.ToInvoiceDetail(Invoice("RF340042"), Person);

        Assert.Equal("RF340042", detail.Reference);
    }

    [Fact]
    public void Detail_shows_the_empty_backfilled_reference()
    {
        var detail = InvoiceMapping.ToInvoiceDetail(Invoice(""), Person);

        Assert.Equal("", detail.Reference);
    }

    [Fact]
    public void List_stays_unchanged_and_has_no_reference()
    {
        var item = InvoiceMapping.ToInvoiceListItem(Invoice("RF340042"), Person);

        Assert.Equal(42, item.Id);
        Assert.Equal("0042", item.Number);
        Assert.Equal("Created", item.Status);
        // The list view does not show the reference (spec #59: detail view only).
        Assert.False(
            typeof(InvoiceListItemDto).GetProperties().Any(p => p.Name == "Reference"),
            "InvoiceListItemDto must not gain a Reference property in this change."
        );
    }

    [Fact]
    public void The_migration_backfills_existing_Rechnungen_with_an_empty_Reference()
    {
        // Covers the backfill criterion without a database: the migration (not the model) is what the
        // running server executes, so assert the shipped migration file.
        var migrationFiles = Directory
            .EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "backend", "Migrations"))
            .Where(p => p.Contains("AddInvoiceReference") && !p.Contains("Designer") && p.EndsWith(".cs"));

        Assert.Single(migrationFiles);
        var migration = File.ReadAllText(migrationFiles.Single());

        // AddColumn on Invoices: varchar(35), not nullable, default '' so pre-existing Rechnungen never
        // match a payment.
        Assert.Contains("name: \"Reference\"", migration);
        Assert.Contains("table: \"Invoices\"", migration);
        Assert.Contains("type: \"character varying(35)\"", migration);
        Assert.Contains("nullable: false", migration);
        Assert.Contains("defaultValue: \"\"", migration);
    }
}
