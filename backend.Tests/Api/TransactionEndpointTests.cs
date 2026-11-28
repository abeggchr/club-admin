using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using backend.Accounting;
using backend.Invoicing;
using backend.Tests.Invoicing;
using Microsoft.EntityFrameworkCore;
using PersonEntity = backend.Person.Person;

namespace backend.Tests.Api;

/// <summary>
/// Creating, editing and deleting a Transaction over HTTP: the status codes and JSON. What is refused and stored is
/// tested at <see cref="TransactionEditor"/> (TransactionEditorTests).
/// </summary>
public class TransactionEndpointTests : IClassFixture<LocalPostgresFixture>
{
    private readonly LocalPostgresFixture _postgres;

    public TransactionEndpointTests(LocalPostgresFixture postgres)
    {
        _postgres = postgres;
        _postgres.SkipUnlessAvailable();
    }

    private AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_postgres.ConnectionString).Options);

    /// <summary>Clears the database and stores the Settings, one open Invoice and one Transaction; returns their ids.</summary>
    private async Task<(int InvoiceId, int TransactionId)> Seed()
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
        var transaction = new Transaction
        {
            Date = new DateOnly(2026, 2, 10),
            Amount = 100m,
            Entries = [new Entry { Text = "Beitrag", AccountNumber = 3400, Amount = 100m }],
        };
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();
        return (invoice.Id, transaction.Id);
    }

    private static HttpClient SignedIn(AppFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "anna@example.ch");
        return client;
    }

    private static object Body(int? invoiceId = null, int? personId = null, string? text = null) =>
        new
        {
            date = "2026-02-10",
            amount = 100m,
            text,
            personId,
            invoiceId,
            entries = new[] { new { id = (int?)null, text = "Jahresbeitrag", accountNumber = 3400, amount = 100m } },
        };

    private static async Task AssertBadRequest(HttpResponseMessage response, string error)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(error, body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Creating_a_Transaction_answers_201_with_its_location_and_the_Transaction()
    {
        var (invoiceId, _) = await Seed();
        await using var factory = new AppFactory(_postgres);

        var response = await SignedIn(factory).PostAsJsonAsync("/api/transactions", Body(invoiceId, text: " Beitrag "));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetInt32();
        Assert.Equal($"/api/transactions/{id}", response.Headers.Location?.ToString());
        Assert.Equal("2026-02-10", body.GetProperty("date").GetString());
        Assert.Equal(100m, body.GetProperty("amount").GetDecimal());
        Assert.Equal("Beitrag", body.GetProperty("text").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("personId").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("bankReference").ValueKind);
        Assert.Equal(200m, body.GetProperty("balance").GetDecimal());
        Assert.Equal(1, body.GetProperty("entryIds").GetArrayLength());
        Assert.Equal(0, body.GetProperty("receipts").GetArrayLength());
        Assert.Equal(invoiceId, body.GetProperty("invoiceId").GetInt32());
    }

    [Fact]
    public async Task A_refused_Transaction_answers_400_with_the_German_error()
    {
        await Seed();
        await using var factory = new AppFactory(_postgres);

        var response = await SignedIn(factory).PostAsJsonAsync("/api/transactions", Body(invoiceId: 999999));

        await AssertBadRequest(response, "Die Rechnung existiert nicht.");
    }

    [Fact]
    public async Task Editing_a_Transaction_answers_200_with_the_Transaction()
    {
        var (_, transactionId) = await Seed();
        await using var factory = new AppFactory(_postgres);

        var response = await SignedIn(factory).PutAsJsonAsync($"/api/transactions/{transactionId}", Body(text: "Neu"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(transactionId, body.GetProperty("id").GetInt32());
        Assert.Equal("Neu", body.GetProperty("text").GetString());
    }

    [Fact]
    public async Task A_refused_edit_answers_400_with_the_German_error()
    {
        var (_, transactionId) = await Seed();
        await using var factory = new AppFactory(_postgres);

        var response = await SignedIn(factory).PutAsJsonAsync($"/api/transactions/{transactionId}", Body(personId: 999999));

        await AssertBadRequest(response, "Die Person existiert nicht.");
    }

    [Fact]
    public async Task Editing_an_unknown_Transaction_answers_404()
    {
        await Seed();
        await using var factory = new AppFactory(_postgres);

        var response = await SignedIn(factory).PutAsJsonAsync("/api/transactions/999999", Body());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_Transaction_answers_204_and_an_unknown_one_404()
    {
        var (_, transactionId) = await Seed();
        await using var factory = new AppFactory(_postgres);
        var client = SignedIn(factory);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/transactions/{transactionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/transactions/{transactionId}")).StatusCode);
    }
}
