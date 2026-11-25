using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using backend.Tests.Invoicing;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Api;

/// <summary>The optional "ePost" API key of the Settings over HTTP: write-only, an empty field keeps the stored value.</summary>
public class EPostSettingsEndpointTests : IClassFixture<LocalPostgresFixture>
{
    private readonly LocalPostgresFixture _postgres;

    public EPostSettingsEndpointTests(LocalPostgresFixture postgres)
    {
        _postgres = postgres;
        _postgres.SkipUnlessAvailable();
    }

    private async Task ClearSettings()
    {
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_postgres.ConnectionString).Options
        );
        await db.Transactions.ExecuteDeleteAsync();
        await db.Settings.ExecuteDeleteAsync();
    }

    private static HttpClient SignedIn(AppFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "anna@example.ch");
        return client;
    }

    private static Dictionary<string, object?> SettingsBody(string? ePostApiKey) =>
        new()
        {
            ["openingBalance"] = 0,
            ["startDate"] = "2026-01-01",
            ["iban"] = "CH9300762011623852957",
            ["name"] = "SC Wettingen",
            ["address"] = "Sportweg 12",
            ["postalCode"] = "5430",
            ["city"] = "Wettingen",
            ["ePostApiKey"] = ePostApiKey,
        };

    [Fact]
    public async Task Without_an_API_key_ePost_is_not_configured()
    {
        await ClearSettings();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);

        await client.PutAsJsonAsync("/api/settings", SettingsBody(null));

        var settings = await client.GetFromJsonAsync<JsonElement>("/api/settings");
        Assert.False(settings.GetProperty("ePostConfigured").GetBoolean());
    }

    [Fact]
    public async Task The_API_key_is_never_returned_only_whether_it_is_set()
    {
        await ClearSettings();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);

        var put = await client.PutAsJsonAsync("/api/settings", SettingsBody("geheimer-key"));
        var get = await client.GetAsync("/api/settings");

        foreach (var text in new[] { await put.Content.ReadAsStringAsync(), await get.Content.ReadAsStringAsync() })
        {
            Assert.DoesNotContain("geheimer-key", text);
            Assert.Contains("\"ePostConfigured\":true", text);
        }
    }

    [Fact]
    public async Task An_empty_API_key_on_save_keeps_the_stored_one()
    {
        await ClearSettings();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);
        await client.PutAsJsonAsync("/api/settings", SettingsBody("geheimer-key"));

        var response = await client.PutAsJsonAsync("/api/settings", SettingsBody(""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var settings = await client.GetFromJsonAsync<JsonElement>("/api/settings");
        Assert.True(settings.GetProperty("ePostConfigured").GetBoolean());
    }
}
