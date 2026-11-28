using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using backend.Invoicing;
using backend.Tests.Invoicing;
using Microsoft.EntityFrameworkCore;
using PersonEntity = backend.Person.Person;

namespace backend.Tests.Api;

/// <summary>Sending an Invoice by email over HTTP, with a recording fake in place of the SMTP server.</summary>
public class InvoiceSendEndpointTests : IClassFixture<LocalPostgresFixture>
{
    private readonly LocalPostgresFixture _postgres;

    public InvoiceSendEndpointTests(LocalPostgresFixture postgres)
    {
        _postgres = postgres;
        _postgres.SkipUnlessAvailable();
    }

    private AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_postgres.ConnectionString).Options);

    /// <summary>Clears the database and seeds Settings (with SMTP unless told otherwise) and one Created Invoice; returns its id.</summary>
    private async Task<int> Seed(
        bool smtp = true,
        InvoiceStatus? status = null,
        bool epost = false
    )
    {
        await using var db = NewDb();
        await db.Invoices.ExecuteDeleteAsync();
        await db.Transactions.ExecuteDeleteAsync();
        await db.Settings.ExecuteDeleteAsync();
        var settings = new backend.Settings.Settings
        {
            StartDate = new DateOnly(2026, 1, 1),
            Iban = "CH9300762011623852957",
            Name = "SC Wettingen",
            Address = "Sportweg 12",
            PostalCode = "5430",
            City = "Wettingen",
            FromAddress = "kasse@example.ch",
        };
        if (smtp)
        {
            settings.SmtpHost = "smtp.example.ch";
            settings.SmtpPort = 587;
            settings.SmtpSecurity = backend.Settings.SmtpSecurity.StartTls;
        }
        if (epost)
            settings.EPostApiKey = "key-123";
        db.Settings.Add(settings);
        var person = new PersonEntity
        {
            FirstName = "Anna",
            LastName = "Muster",
            Email = "anna@example.ch",
            Street = "Bahnhofstrasse",
            Number = "7",
            PostalCode = "5400",
            City = "Baden",
        };
        db.Persons.Add(person);
        await db.SaveChangesAsync();
        var invoice = new Invoice
        {
            PersonId = person.Id,
            Reference = "RF000001",
            Lines = [new InvoiceLine { Text = "Jahresbeitrag", Amount = 100m, AccountNumber = 3400 }],
            StatusChanges = [new InvoiceStatusChange { Status = InvoiceStatus.Created, Timestamp = DateTime.UtcNow.AddDays(-1) }],
        };
        if (status is { } s)
            invoice.StatusChanges.Add(new InvoiceStatusChange { Status = s, Timestamp = DateTime.UtcNow.AddHours(-1) });
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

    private static async Task<AppFactory> FactoryWithPdf(LocalPostgresFixture postgres, int id)
    {
        var factory = new AppFactory(postgres);
        await factory.InvoicePdfs.PutAsync(id, [1, 2, 3]);
        return factory;
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, int id, string? subject = "Rechnung", string? text = "Guten Tag") =>
        client.PostAsJsonAsync($"/api/invoices/{id}/send", new { subject, text });

    private static Task<HttpResponseMessage> SendByPost(HttpClient client, int id) =>
        client.PostAsJsonAsync($"/api/invoices/{id}/send", new { channel = "Post" });

    private static async Task<string> Error(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString()!;

    [Fact]
    public async Task Sending_emails_the_PDF_and_records_Sent_with_the_Remark_per_Email()
    {
        var id = await Seed();
        await using var factory = await FactoryWithPdf(_postgres, id);
        using var client = SignedIn(factory);

        var response = await Send(client, id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var mail = Assert.Single(factory.Emails.Invoices);
        Assert.Equal("anna@example.ch", mail.Email.To);
        Assert.Equal("Rechnung", mail.Email.Subject);
        Assert.Equal("Guten Tag", mail.Email.Text);
        Assert.Equal($"rechnung-{InvoiceRules.FormatNumber(id)}.pdf", mail.Email.AttachmentName);
        Assert.Equal(new byte[] { 1, 2, 3 }, mail.Email.Attachment);
        Assert.Equal("kasse@example.ch", mail.Smtp.FromAddress);
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/invoices/{id}");
        Assert.Equal("Sent", detail.GetProperty("status").GetString());
        var last = detail.GetProperty("statusHistory").EnumerateArray().Last();
        Assert.Equal("Sent", last.GetProperty("status").GetString());
        Assert.Equal("per Email", last.GetProperty("remark").GetString());
    }

    [Theory]
    [InlineData(null, "Text", "Der Betreff ist erforderlich.")]
    [InlineData("Betreff", " ", "Der Text ist erforderlich.")]
    public async Task Subject_and_text_are_required(string? subject, string? text, string error)
    {
        var id = await Seed();
        await using var factory = await FactoryWithPdf(_postgres, id);
        using var client = SignedIn(factory);

        var response = await Send(client, id, subject, text);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(error, await Error(response));
        Assert.Empty(factory.Emails.Invoices);
    }

    [Fact]
    public async Task Sending_an_unknown_Rechnung_is_not_found()
    {
        await Seed();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);

        Assert.Equal(HttpStatusCode.NotFound, (await Send(client, 999999)).StatusCode);
    }

    [Fact]
    public async Task A_Sent_Rechnung_can_be_cancelled()
    {
        var id = await Seed(status: InvoiceStatus.Sent);
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);

        var response = await client.PostAsJsonAsync($"/api/invoices/{id}/cancel", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Cancelled", detail.GetProperty("status").GetString());
    }

    [Fact]
    public async Task The_detail_shows_the_email_address_of_the_Recipient()
    {
        var id = await Seed();
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/invoices/{id}");

        Assert.Equal("anna@example.ch", detail.GetProperty("person").GetProperty("email").GetString());
    }

    [Fact]
    public async Task The_detail_lists_the_Delivery_Channels_with_the_reason_each_one_is_blocked()
    {
        var id = await Seed(epost: false);
        await using var factory = new AppFactory(_postgres);
        using var client = SignedIn(factory);

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/invoices/{id}");

        var options = detail.GetProperty("channelOptions").EnumerateArray().ToList();
        Assert.Equal(2, options.Count);
        Assert.Equal("Email", options[0].GetProperty("channel").GetString());
        Assert.Equal(JsonValueKind.Null, options[0].GetProperty("blockedReason").ValueKind);
        Assert.Equal("Post", options[1].GetProperty("channel").GetString());
        Assert.Equal("ePost ist nicht eingerichtet.", options[1].GetProperty("blockedReason").GetString());
    }

    [Fact]
    public async Task Sending_by_post_hands_the_PDF_to_ePost_and_records_Sent_with_the_delivery_id()
    {
        var id = await Seed(epost: true);
        await using var factory = await FactoryWithPdf(_postgres, id);
        using var client = SignedIn(factory);

        var response = await SendByPost(client, id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, factory.EPost.Calls);
        Assert.Empty(factory.Emails.Invoices);
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/invoices/{id}");
        var last = detail.GetProperty("statusHistory").EnumerateArray().Last();
        Assert.Equal("Sent", last.GetProperty("status").GetString());
        Assert.Equal($"per Post ({factory.EPost.DeliveryId})", last.GetProperty("remark").GetString());
    }

    [Fact]
    public async Task Without_an_SMTP_group_the_email_goes_through_ePost_with_the_club_as_Reply_To()
    {
        var id = await Seed(smtp: false, epost: true);
        await using var factory = await FactoryWithPdf(_postgres, id);
        using var client = SignedIn(factory);

        var response = await Send(client, id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(factory.Emails.Invoices);
        var delivery = Assert.Single(factory.EPost.Deliveries);
        Assert.Equal("EMAIL", (string?)delivery["deliveryChannelPreferences"]![0]);
        Assert.Equal("anna@example.ch", (string?)delivery["recipients"]![0]!["unhashedCredentials"]!["email"]);
        var email = delivery["emailAdditionalInfo"]!;
        Assert.Equal("Rechnung", (string?)email["emailSubject"]);
        Assert.Equal("Guten Tag", (string?)email["emailText"]);
        Assert.Equal("kasse@example.ch", (string?)email["replyTo"]![0]!["email"]);
        Assert.Equal("SC Wettingen", (string?)email["replyTo"]![0]!["name"]);
        Assert.Null(email["sender"]);
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/invoices/{id}");
        var last = detail.GetProperty("statusHistory").EnumerateArray().Last();
        Assert.Equal("Sent", last.GetProperty("status").GetString());
        Assert.Equal($"per Email (ePost, {factory.EPost.DeliveryId})", last.GetProperty("remark").GetString());
    }

    [Fact]
    public async Task A_failing_SMTP_server_is_not_retried_through_ePost()
    {
        var id = await Seed(smtp: true, epost: true);
        await using var factory = await FactoryWithPdf(_postgres, id);
        factory.Emails.FailWith = "Connection timed out";
        using var client = SignedIn(factory);

        var response = await Send(client, id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Die E-Mail konnte nicht gesendet werden: Connection timed out", await Error(response));
        Assert.Equal(0, factory.EPost.Calls);
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/invoices/{id}");
        Assert.Equal("Created", detail.GetProperty("status").GetString());
    }
}
