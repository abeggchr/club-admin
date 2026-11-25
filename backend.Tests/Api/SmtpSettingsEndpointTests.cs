using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using backend.Tests.Invoicing;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Api;

/// <summary>The optional "E-Mail (SMTP)" group of the Settings over HTTP: all-or-nothing validation, write-only password, configured flag and the test email.</summary>
public class SmtpSettingsEndpointTests : IClassFixture<LocalPostgresFixture>
{
    private readonly LocalPostgresFixture _postgres;

    public SmtpSettingsEndpointTests(LocalPostgresFixture postgres)
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

    private static Dictionary<string, object?> FullSmtp(string? password = "geheim") =>
        new()
        {
            ["host"] = "smtp.example.ch",
            ["port"] = 587,
            ["security"] = "StartTls",
            ["username"] = "kasse",
            ["password"] = password,
        };

    private static Dictionary<string, object?> SettingsBody(object? smtp, string? fromAddress = "kasse@example.ch") =>
        new()
        {
            ["openingBalance"] = 0,
            ["startDate"] = "2026-01-01",
            ["iban"] = "CH9300762011623852957",
            ["name"] = "SC Wettingen",
            ["address"] = "Sportweg 12",
            ["postalCode"] = "5430",
            ["city"] = "Wettingen",
            ["fromAddress"] = fromAddress,
            ["smtp"] = smtp,
        };

    [Fact]
    public async Task Settings_without_an_SMTP_group_are_valid_and_the_email_channel_is_not_configured()
    {
        await ClearSettings();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);

        var response = await client.PutAsJsonAsync("/api/settings", SettingsBody(null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var settings = await client.GetFromJsonAsync<JsonElement>("/api/settings");
        Assert.False(settings.GetProperty("emailConfigured").GetBoolean());
        Assert.False(settings.GetProperty("smtp").GetProperty("passwordSet").GetBoolean());
    }

    [Fact]
    public async Task A_partly_filled_SMTP_group_is_rejected_with_a_German_message()
    {
        await ClearSettings();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);
        var smtp = FullSmtp();
        smtp["host"] = "";

        var response = await client.PutAsJsonAsync("/api/settings", SettingsBody(smtp));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Der SMTP-Server ist erforderlich.", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task A_complete_SMTP_group_is_saved_and_the_email_channel_is_configured()
    {
        await ClearSettings();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);

        var response = await client.PutAsJsonAsync("/api/settings", SettingsBody(FullSmtp()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var settings = await client.GetFromJsonAsync<JsonElement>("/api/settings");
        Assert.True(settings.GetProperty("emailConfigured").GetBoolean());
        var smtp = settings.GetProperty("smtp");
        Assert.Equal("smtp.example.ch", smtp.GetProperty("host").GetString());
        Assert.Equal(587, smtp.GetProperty("port").GetInt32());
        Assert.Equal("StartTls", smtp.GetProperty("security").GetString());
        Assert.Equal("kasse@example.ch", settings.GetProperty("fromAddress").GetString());
        Assert.Equal("Smtp", settings.GetProperty("emailBackend").GetString());
    }

    [Fact]
    public async Task The_password_is_never_returned_only_whether_it_is_set()
    {
        await ClearSettings();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);

        var put = await client.PutAsJsonAsync("/api/settings", SettingsBody(FullSmtp("geheim")));
        var get = await client.GetAsync("/api/settings");

        foreach (var text in new[] { await put.Content.ReadAsStringAsync(), await get.Content.ReadAsStringAsync() })
        {
            Assert.DoesNotContain("geheim", text);
            Assert.Contains("\"passwordSet\":true", text);
        }
    }

    [Fact]
    public async Task An_empty_password_on_save_keeps_the_stored_one()
    {
        await ClearSettings();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);
        await client.PutAsJsonAsync("/api/settings", SettingsBody(FullSmtp("geheim")));

        var response = await client.PutAsJsonAsync("/api/settings", SettingsBody(FullSmtp(password: "")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var settings = await client.GetFromJsonAsync<JsonElement>("/api/settings");
        Assert.True(settings.GetProperty("smtp").GetProperty("passwordSet").GetBoolean());
        var testResponse = await client.PostAsJsonAsync(
            "/api/settings/smtp/test",
            new { receiver = "x@example.ch", fromAddress = "kasse@example.ch", smtp = FullSmtp(password: "") }
        );
        Assert.Equal(HttpStatusCode.OK, testResponse.StatusCode);
        Assert.Equal("geheim", Assert.Single(factory.Emails.Sent).Smtp.Password);
    }

    [Fact]
    public async Task Emptying_the_whole_SMTP_group_clears_it_including_the_password()
    {
        await ClearSettings();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);
        await client.PutAsJsonAsync("/api/settings", SettingsBody(FullSmtp()));

        await client.PutAsJsonAsync("/api/settings", SettingsBody(null));

        var settings = await client.GetFromJsonAsync<JsonElement>("/api/settings");
        Assert.False(settings.GetProperty("emailConfigured").GetBoolean());
        Assert.False(settings.GetProperty("smtp").GetProperty("passwordSet").GetBoolean());
    }

    [Fact]
    public async Task The_test_email_is_sent_to_the_receiver_with_the_submitted_values()
    {
        await ClearSettings();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);

        var response = await client.PostAsJsonAsync(
            "/api/settings/smtp/test",
            new { receiver = "empfaenger@example.ch", fromAddress = "kasse@example.ch", smtp = FullSmtp("neu") }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sent = Assert.Single(factory.Emails.Sent);
        Assert.Equal("empfaenger@example.ch", sent.Receiver);
        Assert.Equal("smtp.example.ch", sent.Smtp.Host);
        Assert.Equal("kasse@example.ch", sent.Smtp.FromAddress);
        Assert.Equal("neu", sent.Smtp.Password);
    }

    [Fact]
    public async Task A_failing_SMTP_server_is_reported_with_its_error()
    {
        await ClearSettings();
        await using var factory = new AppFactory(_postgres);
        factory.Emails.FailWith = "Authentication failed";
        using var client = SignedIn(factory);

        var response = await client.PostAsJsonAsync(
            "/api/settings/smtp/test",
            new { receiver = "empfaenger@example.ch", fromAddress = "kasse@example.ch", smtp = FullSmtp() }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Authentication failed", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task The_test_email_needs_a_complete_SMTP_group_and_a_valid_receiver()
    {
        await ClearSettings();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);

        var incomplete = await client.PostAsJsonAsync(
            "/api/settings/smtp/test",
            new { receiver = "empfaenger@example.ch", smtp = new { host = "smtp.example.ch" } }
        );
        var badReceiver = await client.PostAsJsonAsync(
            "/api/settings/smtp/test",
            new { receiver = "kein-email", fromAddress = "kasse@example.ch", smtp = FullSmtp() }
        );

        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badReceiver.StatusCode);
        Assert.Empty(factory.Emails.Sent);
    }

    [Fact]
    public async Task An_SMTP_group_without_the_club_address_is_rejected()
    {
        await ClearSettings();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);

        var response = await client.PutAsJsonAsync("/api/settings", SettingsBody(FullSmtp(), fromAddress: ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Die Absenderadresse ist erforderlich.", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task An_ePost_key_with_the_club_address_makes_ePost_the_email_backend_and_the_key_is_not_returned()
    {
        await ClearSettings();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);
        var body = SettingsBody(null);
        body["ePostApiKey"] = "key-123";

        var put = await client.PutAsJsonAsync("/api/settings", body);

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var text = await (await client.GetAsync("/api/settings")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("key-123", text);
        var settings = JsonDocument.Parse(text).RootElement;
        Assert.True(settings.GetProperty("emailConfigured").GetBoolean());
        Assert.Equal("EPost", settings.GetProperty("emailBackend").GetString());
    }

    [Fact]
    public async Task Without_the_club_address_an_ePost_key_does_not_configure_email()
    {
        await ClearSettings();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);
        var body = SettingsBody(null, fromAddress: null);
        body["ePostApiKey"] = "key-123";

        await client.PutAsJsonAsync("/api/settings", body);

        var settings = await client.GetFromJsonAsync<JsonElement>("/api/settings");
        Assert.False(settings.GetProperty("emailConfigured").GetBoolean());
        Assert.Equal(JsonValueKind.Null, settings.GetProperty("emailBackend").ValueKind);
        Assert.True(settings.GetProperty("ePostConfigured").GetBoolean());
    }

    [Fact]
    public async Task The_club_address_survives_emptying_the_SMTP_group()
    {
        await ClearSettings();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);
        await client.PutAsJsonAsync("/api/settings", SettingsBody(FullSmtp()));

        await client.PutAsJsonAsync("/api/settings", SettingsBody(null));

        var settings = await client.GetFromJsonAsync<JsonElement>("/api/settings");
        Assert.Equal("kasse@example.ch", settings.GetProperty("fromAddress").GetString());
    }

    [Fact]
    public async Task The_test_email_never_goes_through_ePost()
    {
        await ClearSettings();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);

        var response = await client.PostAsJsonAsync(
            "/api/settings/smtp/test",
            new { receiver = "empfaenger@example.ch", fromAddress = "kasse@example.ch", smtp = (object?)null }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.EPost.Calls);
        Assert.Empty(factory.Emails.Sent);
    }
}
