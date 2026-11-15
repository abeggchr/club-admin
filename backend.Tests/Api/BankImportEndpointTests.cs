using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using backend.Tests.Invoicing;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Api;

/// <summary>The wire format of POST /api/transactions/import: which JSON each outcome of the import produces.</summary>
public class BankImportEndpointTests : IClassFixture<LocalPostgresFixture>
{
    private const string ClubIban = "CH9300762011623852957";
    private readonly LocalPostgresFixture _postgres;

    public BankImportEndpointTests(LocalPostgresFixture postgres)
    {
        _postgres = postgres;
        _postgres.SkipUnlessAvailable();
    }

    private async Task<HttpResponseMessage> Post(AppFactory factory, string xml)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "anna@example.ch");
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(xml)), "file", "stmt.xml");
        return await client.PostAsync("/api/transactions/import", form);
    }

    private async Task SetSettings(bool present)
    {
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_postgres.ConnectionString).Options
        );
        await db.Transactions.ExecuteDeleteAsync();
        await db.Settings.ExecuteDeleteAsync();
        if (present)
        {
            db.Settings.Add(
                new backend.Settings.Settings
                {
                    StartDate = new DateOnly(2026, 1, 1),
                    Iban = ClubIban,
                    Name = "SC Wettingen",
                    Address = "Sportweg 12",
                    PostalCode = "5430",
                    City = "Wettingen",
                }
            );
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Missing_Settings_give_400_with_a_single_error_message()
    {
        await SetSettings(present: false);
        await using var factory = new AppFactory(_postgres);

        var response = await Post(factory, Statement(ClubIban, "TX-1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.String, body.GetProperty("error").ValueKind);
    }

    [Fact]
    public async Task A_file_for_another_Iban_gives_400_with_the_errors_list()
    {
        await SetSettings(present: true);
        await using var factory = new AppFactory(_postgres);

        var response = await Post(factory, Statement("CH9300762011623852951", "TX-1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.GetProperty("errors").ValueKind);
    }

    [Fact]
    public async Task A_valid_file_gives_200_with_the_four_counts()
    {
        await SetSettings(present: true);
        await using var factory = new AppFactory(_postgres);

        var response = await Post(factory, Statement(ClubIban, "TX-OK"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("imported").GetInt32());
        Assert.Equal(0, body.GetProperty("skippedDuplicates").GetInt32());
        Assert.Equal(0, body.GetProperty("bookedInvoices").GetArrayLength());
        Assert.Equal(1, body.GetProperty("unmatched").GetInt32());
    }

    private static string Statement(string iban, string txRef) =>
        backend.Tests.Accounting.CamtTestHelpers.CamtFile(
            "stmt",
            iban,
            backend.Tests.Accounting.CamtTestHelpers.TxDtlsNtry("25.00", "CRDT", "2026-02-10", txRef)
        );
}
