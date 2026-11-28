using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using backend.Accounting;
using backend.Invoicing;
using PersonEntity = backend.Person.Person;
using backend.Tests.Invoicing;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Api;

/// <summary>Saving a Transaction as paying an open Invoice sets the Invoice's PaidBy and Paid status.</summary>
public class TransactionInvoiceLinkTests : IClassFixture<LocalPostgresFixture>
{
    private readonly LocalPostgresFixture _postgres;

    public TransactionInvoiceLinkTests(LocalPostgresFixture postgres)
    {
        _postgres = postgres;
        _postgres.SkipUnlessAvailable();
    }

    private AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_postgres.ConnectionString).Options);

    /// <summary>Clears the database, stores the Settings and one open Invoice of 100.00; returns the Invoice id.</summary>
    private async Task<int> SeedInvoice()
    {
        await using var db = NewDb();
        await db.Invoices.ExecuteDeleteAsync();
        await db.Transactions.ExecuteDeleteAsync();
        await db.Settings.ExecuteDeleteAsync();
        db.Settings.Add(
            new backend.Settings.Settings
            {
                StartDate = new DateOnly(2026, 1, 1),
                Iban = "CH9300762011623852957",
                Name = "SC Wettingen",
                Address = "Sportweg 12",
                PostalCode = "5430",
                City = "Wettingen",
            }
        );
        var person = new PersonEntity { FirstName = "Anna", LastName = "Muster" };
        db.Persons.Add(person);
        await db.SaveChangesAsync();
        var invoice = new Invoice
        {
            PersonId = person.Id,
            Reference = "RF000001",
            Lines = [new InvoiceLine { Text = "Jahresbeitrag", Amount = 100m, AccountNumber = 3400 }],
            StatusChanges = [new InvoiceStatusChange { Status = InvoiceStatus.Created, Timestamp = DateTime.UtcNow }],
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice.Id;
    }

    private static HttpClient SignedIn(AppFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "anna@example.ch");
        return client;
    }

    private static object Body(int? invoiceId) =>
        new
        {
            date = "2026-02-10",
            amount = 100m,
            text = (string?)null,
            personId = (int?)null,
            invoiceId,
            entries = new[] { new { id = (int?)null, text = "Jahresbeitrag", accountNumber = 3400, amount = 100m } },
        };

    [Fact]
    public async Task Saving_a_Transaction_with_an_open_Invoice_marks_it_paid_by_that_Transaction()
    {
        var invoiceId = await SeedInvoice();
        await using var factory = new AppFactory(_postgres);
        var client = SignedIn(factory);

        var response = await client.PostAsJsonAsync("/api/transactions", Body(invoiceId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var transaction = await response.Content.ReadFromJsonAsync<JsonElement>();
        var transactionId = transaction.GetProperty("id").GetInt32();
        Assert.Equal(invoiceId, transaction.GetProperty("invoiceId").GetInt32());
        await using var db = NewDb();
        var invoice = await db.Invoices.Include(i => i.StatusChanges).SingleAsync(i => i.Id == invoiceId);
        Assert.Equal(transactionId, invoice.PaidBy);
        Assert.Equal(InvoiceStatus.Paid, InvoiceStatusRules.Current(invoice));
    }

    [Fact]
    public async Task An_Invoice_that_is_already_paid_cannot_be_linked_again()
    {
        var invoiceId = await SeedInvoice();
        await using var factory = new AppFactory(_postgres);
        var client = SignedIn(factory);
        (await client.PostAsJsonAsync("/api/transactions", Body(invoiceId))).EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync("/api/transactions", Body(invoiceId));

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task An_unknown_Invoice_is_rejected()
    {
        await SeedInvoice();
        await using var factory = new AppFactory(_postgres);

        var response = await SignedIn(factory).PostAsJsonAsync("/api/transactions", Body(999999));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
