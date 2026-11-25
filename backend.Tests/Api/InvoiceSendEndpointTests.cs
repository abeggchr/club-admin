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
        string? email = "anna@example.ch",
        InvoiceStatus? status = null,
        bool epost = false,
        string? street = "Bahnhofstrasse",
        string? country = null
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
        };
        if (smtp)
        {
            settings.SmtpHost = "smtp.example.ch";
            settings.SmtpPort = 587;
            settings.SmtpSecurity = backend.Settings.SmtpSecurity.StartTls;
            settings.SmtpFromAddress = "kasse@example.ch";
        }
        if (epost)
            settings.EPostApiKey = "key-123";
        db.Settings.Add(settings);
        var person = new PersonEntity
        {
            FirstName = "Anna",
            LastName = "Muster",
            Email = email,
            Street = street,
            Number = "7",
            PostalCode = "5400",
            City = "Baden",
            Country = country,
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

    [Fact]
    public async Task Sending_again_adds_another_Sent_row()
    {
        var id = await Seed(status: InvoiceStatus.Sent);
        await using var factory = await FactoryWithPdf(_postgres, id);
        using var client = SignedIn(factory);

        Assert.Equal(HttpStatusCode.OK, (await Send(client, id)).StatusCode);

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/invoices/{id}");
        Assert.Equal(
            2,
            detail.GetProperty("statusHistory").EnumerateArray().Count(s => s.GetProperty("status").GetString() == "Sent")
        );
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

    [Theory]
    [InlineData(InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.Cancelled)]
    public async Task A_Paid_or_Cancelled_Rechnung_cannot_be_sent(InvoiceStatus status)
    {
        var id = await Seed(status: status);
        await using var factory = await FactoryWithPdf(_postgres, id);
        using var client = SignedIn(factory);

        var response = await Send(client, id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Nur offene Rechnungen können versendet werden.", await Error(response));
        Assert.Empty(factory.Emails.Invoices);
    }

    [Fact]
    public async Task A_Recipient_without_an_email_address_is_refused()
    {
        var id = await Seed(email: null);
        await using var factory = await FactoryWithPdf(_postgres, id);
        using var client = SignedIn(factory);

        var response = await Send(client, id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Die Person hat keine E-Mail-Adresse.", await Error(response));
    }

    [Fact]
    public async Task Sending_is_refused_while_email_is_not_configured()
    {
        var id = await Seed(smtp: false);
        await using var factory = await FactoryWithPdf(_postgres, id);
        using var client = SignedIn(factory);

        var response = await Send(client, id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("E-Mail ist nicht eingerichtet.", await Error(response));
        Assert.Empty(factory.Emails.Invoices);
    }

    [Fact]
    public async Task A_sender_failure_returns_a_German_error_and_records_nothing()
    {
        var id = await Seed();
        await using var factory = await FactoryWithPdf(_postgres, id);
        factory.Emails.FailWith = "Connection refused";
        using var client = SignedIn(factory);

        var response = await Send(client, id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Die E-Mail konnte nicht gesendet werden: Connection refused", await Error(response));
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/invoices/{id}");
        Assert.Equal("Created", detail.GetProperty("status").GetString());
        Assert.Equal(1, detail.GetProperty("statusHistory").GetArrayLength());
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

    [Theory]
    [InlineData("Schweiz")]
    [InlineData("ch")]
    public async Task Sending_by_post_accepts_Switzerland_in_any_spelling(string country)
    {
        var id = await Seed(epost: true, country: country);
        await using var factory = await FactoryWithPdf(_postgres, id);
        using var client = SignedIn(factory);

        Assert.Equal(HttpStatusCode.OK, (await SendByPost(client, id)).StatusCode);
    }

    [Fact]
    public async Task Post_is_refused_while_ePost_is_not_configured()
    {
        var id = await Seed(epost: false);
        await using var factory = await FactoryWithPdf(_postgres, id);
        using var client = SignedIn(factory);

        var response = await SendByPost(client, id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ePost ist nicht eingerichtet.", await Error(response));
        Assert.Equal(0, factory.EPost.Calls);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("Bahnhofstrasse", "Deutschland")]
    public async Task Post_is_refused_for_an_incomplete_or_foreign_address(string? street, string? country)
    {
        var id = await Seed(epost: true, street: street, country: country);
        await using var factory = await FactoryWithPdf(_postgres, id);
        using var client = SignedIn(factory);

        var response = await SendByPost(client, id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Die Person hat keine vollständige Schweizer Adresse (Strasse, PLZ und Ort).", await Error(response));
        Assert.Equal(0, factory.EPost.Calls);
    }

    [Fact]
    public async Task An_ePost_failure_returns_the_message_and_records_nothing()
    {
        var id = await Seed(epost: true);
        await using var factory = await FactoryWithPdf(_postgres, id);
        factory.EPost.Status = HttpStatusCode.TooManyRequests;
        factory.EPost.Body = """{"message":"Zu viele Anfragen"}""";
        using var client = SignedIn(factory);

        var response = await SendByPost(client, id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Der Brief konnte nicht versendet werden: Zu viele Anfragen", await Error(response));
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/invoices/{id}");
        Assert.Equal(1, detail.GetProperty("statusHistory").GetArrayLength());
    }

    [Fact]
    public async Task Email_stays_possible_when_ePost_is_configured_too()
    {
        var id = await Seed(epost: true);
        await using var factory = await FactoryWithPdf(_postgres, id);
        using var client = SignedIn(factory);

        Assert.Equal(HttpStatusCode.OK, (await Send(client, id)).StatusCode);
        Assert.Single(factory.Emails.Invoices);
        Assert.Equal(0, factory.EPost.Calls);
    }
}
