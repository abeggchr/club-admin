using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using backend.Accounting;
using backend.Tests.Invoicing;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Api;

/// <summary>Receipts (Belege) of a Transaction over HTTP: upload, list, open, remove and their effect on the Transaction.</summary>
public class ReceiptEndpointTests : IClassFixture<LocalPostgresFixture>
{
    private static readonly byte[] Pdf = "%PDF-1.4 receipt"u8.ToArray();
    private readonly LocalPostgresFixture _postgres;

    public ReceiptEndpointTests(LocalPostgresFixture postgres)
    {
        _postgres = postgres;
        _postgres.SkipUnlessAvailable();
    }

    private AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_postgres.ConnectionString).Options);

    /// <summary>Clears the database and stores the Settings plus one Transaction; returns its Id.</summary>
    private async Task<int> SeedTransaction(string? bankReference = null, bool withEntry = true)
    {
        await using var db = NewDb();
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
        var transaction = new Transaction
        {
            Date = new DateOnly(2026, 2, 10),
            Amount = -50m,
            Text = "Material",
            BankReference = bankReference,
        };
        if (withEntry)
        {
            transaction.Entries.Add(new Entry { Text = "Material", AccountNumber = 6010, Amount = -50m });
        }
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();
        return transaction.Id;
    }

    private static HttpClient SignedIn(AppFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "anna@example.ch");
        return client;
    }

    private static Task<HttpResponseMessage> Upload(HttpClient client, int transactionId, byte[] content)
    {
        var form = new MultipartFormDataContent { { new ByteArrayContent(content), "file", "scan.pdf" } };
        return client.PostAsync($"/api/transactions/{transactionId}/receipts", form);
    }

    private static async Task<JsonElement[]> ReceiptsOf(HttpClient client, int transactionId)
    {
        var list = await client.GetFromJsonAsync<JsonElement>("/api/transactions");
        var transaction = list.EnumerateArray().Single(t => t.GetProperty("id").GetInt32() == transactionId);
        return transaction.GetProperty("receipts").EnumerateArray().ToArray();
    }

    [Fact]
    public async Task An_uploaded_PDF_shows_up_as_a_Receipt_of_the_Transaction()
    {
        var id = await SeedTransaction();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);

        var response = await Upload(client, id, Pdf);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var receipts = await ReceiptsOf(client, id);
        var receipt = Assert.Single(receipts);
        Assert.Equal(created.GetProperty("id").GetInt32(), receipt.GetProperty("id").GetInt32());
        Assert.True(receipt.TryGetProperty("uploadedAt", out _));
    }

    [Fact]
    public async Task A_Receipt_is_streamed_inline_with_the_uploaded_bytes()
    {
        var id = await SeedTransaction();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);
        var created = await (await Upload(client, id, Pdf)).Content.ReadFromJsonAsync<JsonElement>();
        var receiptId = created.GetProperty("id").GetInt32();

        var response = await client.GetAsync($"/api/transactions/receipts/{receiptId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            $"inline; filename=\"Beleg-{receiptId}.pdf\"",
            response.Content.Headers.ContentDisposition?.ToString()
        );
        Assert.Equal(Pdf, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task An_unknown_Receipt_cannot_be_opened()
    {
        await SeedTransaction();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);

        var response = await client.GetAsync("/api/transactions/receipts/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Several_uploads_keep_their_upload_order()
    {
        var id = await SeedTransaction();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);
        var first = await (await Upload(client, id, Pdf)).Content.ReadFromJsonAsync<JsonElement>();
        var second = await (await Upload(client, id, Pdf)).Content.ReadFromJsonAsync<JsonElement>();

        var receipts = await ReceiptsOf(client, id);

        Assert.Equal(
            [first.GetProperty("id").GetInt32(), second.GetProperty("id").GetInt32()],
            receipts.Select(r => r.GetProperty("id").GetInt32())
        );
    }

    [Theory]
    [InlineData("TX-1", true)] // Imported
    [InlineData(null, false)] // Unbooked
    [InlineData(null, true)] // manual
    public async Task A_Receipt_can_be_attached_to_any_kind_of_Transaction(string? bankReference, bool withEntry)
    {
        var id = await SeedTransaction(bankReference, withEntry);
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);

        var response = await Upload(client, id, Pdf);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_Transaction_gives_404()
    {
        await SeedTransaction();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);

        var response = await Upload(client, 999999, Pdf);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_file_that_is_not_a_PDF_is_rejected_with_a_German_error_and_stores_nothing()
    {
        var id = await SeedTransaction();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);

        var response = await Upload(client, id, "GIF89a"u8.ToArray());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("PDF", body.GetProperty("error").GetString());
        Assert.Empty(await ReceiptsOf(client, id));
        Assert.Empty(factory.Receipts.Keys);
    }

    [Fact]
    public async Task An_empty_file_is_rejected()
    {
        var id = await SeedTransaction();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);

        var response = await Upload(client, id, []);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_file_over_10_MB_is_rejected()
    {
        var id = await SeedTransaction();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);
        var big = new byte[10 * 1024 * 1024 + 1];
        Pdf.CopyTo(big, 0);

        var response = await Upload(client, id, big);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_21st_Receipt_of_a_Transaction_is_rejected()
    {
        var id = await SeedTransaction();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await Upload(client, id, Pdf)).StatusCode);
        }

        var response = await Upload(client, id, Pdf);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(20, (await ReceiptsOf(client, id)).Length);
    }

    [Fact]
    public async Task Removing_a_Receipt_deletes_the_row_and_the_object()
    {
        var id = await SeedTransaction();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);
        var created = await (await Upload(client, id, Pdf)).Content.ReadFromJsonAsync<JsonElement>();
        var receiptId = created.GetProperty("id").GetInt32();

        var response = await client.DeleteAsync($"/api/transactions/receipts/{receiptId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await ReceiptsOf(client, id));
        Assert.Empty(factory.Receipts.Keys);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/transactions/receipts/{receiptId}")).StatusCode
        );
    }

    [Fact]
    public async Task Removing_an_unknown_Receipt_gives_404()
    {
        await SeedTransaction();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);

        var response = await client.DeleteAsync("/api/transactions/receipts/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_Transaction_deletes_its_Receipts_and_their_objects()
    {
        var id = await SeedTransaction(withEntry: false);
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);
        await Upload(client, id, Pdf);
        await Upload(client, id, Pdf);

        var response = await client.DeleteAsync($"/api/transactions/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(factory.Receipts.Keys);
        await using var db = NewDb();
        Assert.Empty(await db.Receipts.ToListAsync());
    }

    [Fact]
    public async Task Editing_the_Entries_of_a_Transaction_leaves_its_Receipts_alone()
    {
        var id = await SeedTransaction(bankReference: "TX-1");
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);
        await Upload(client, id, Pdf);
        var entryId = (await client.GetFromJsonAsync<JsonElement>("/api/transactions"))
            .EnumerateArray().Single().GetProperty("entryIds")[0].GetInt32();

        var response = await client.PutAsJsonAsync(
            $"/api/transactions/{id}",
            new
            {
                date = "2026-02-10",
                amount = -50m,
                text = "Material",
                personId = (int?)null,
                entries = new[] { new { id = (int?)entryId, text = "Neu", accountNumber = 6010, amount = -50m } },
            }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(await ReceiptsOf(client, id));
        Assert.Single(factory.Receipts.Keys);
    }

    [Fact]
    public async Task The_endpoints_require_a_sign_in()
    {
        var id = await SeedTransaction();
        await using var factory = new AppFactory(_postgres);
        using var anonymous = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        Assert.NotEqual(HttpStatusCode.OK, (await Upload(anonymous, id, Pdf)).StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await anonymous.GetAsync("/api/transactions/receipts/1")).StatusCode);
        Assert.NotEqual(HttpStatusCode.NoContent, (await anonymous.DeleteAsync("/api/transactions/receipts/1")).StatusCode);
        Assert.Empty(factory.Receipts.Keys);
    }

    [Fact]
    public async Task A_re_import_of_the_bank_statement_leaves_Receipts_alone()
    {
        await SeedTransaction();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);
        var xml = backend.Tests.Accounting.CamtTestHelpers.CamtFile(
            "stmt",
            "CH9300762011623852957",
            backend.Tests.Accounting.CamtTestHelpers.TxDtlsNtry("25.00", "CRDT", "2026-02-11", "TX-RECEIPT")
        );
        async Task Import()
        {
            var form = new MultipartFormDataContent
            {
                { new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(xml)), "file", "stmt.xml" },
            };
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/transactions/import", form)).StatusCode);
        }
        await Import();
        var imported = (await client.GetFromJsonAsync<JsonElement>("/api/transactions"))
            .EnumerateArray().Single(t => t.GetProperty("bankReference").GetString() == "TX-RECEIPT");
        var id = imported.GetProperty("id").GetInt32();
        await Upload(client, id, Pdf);

        await Import();

        Assert.Single(await ReceiptsOf(client, id));
        Assert.Single(factory.Receipts.Keys);
    }
}
