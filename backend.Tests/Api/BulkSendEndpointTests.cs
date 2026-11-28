using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using backend.Invoicing;
using backend.Tests.Invoicing;
using Microsoft.EntityFrameworkCore;
using PersonEntity = backend.Person.Person;

namespace backend.Tests.Api;

/// <summary>Sending all unsent Invoices at once over HTTP, with fakes in place of the SMTP server and ePost.</summary>
public class BulkSendEndpointTests : IClassFixture<LocalPostgresFixture>
{
    private readonly LocalPostgresFixture _postgres;

    public BulkSendEndpointTests(LocalPostgresFixture postgres)
    {
        _postgres = postgres;
        _postgres.SkipUnlessAvailable();
    }

    private AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_postgres.ConnectionString).Options);

    private sealed record Seeded(int Email, int EmailAndPost, int PostOnly, int Nobody, int AlreadySent);

    /// <summary>Settings with SMTP and ePost, and five Invoices: email only, email and post, post only, no way to deliver, and one already Sent.</summary>
    private async Task<(Seeded Ids, AppFactory Factory)> Seed()
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
            SmtpHost = "smtp.example.ch",
            SmtpPort = 587,
            SmtpSecurity = backend.Settings.SmtpSecurity.StartTls,
            EPostApiKey = "key-123",
        };
        db.Settings.Add(settings);

        async Task<int> Invoice(string last, string? email, bool address, InvoiceStatus? status = null)
        {
            var person = new PersonEntity
            {
                FirstName = "Anna",
                LastName = last,
                Email = email,
                Street = address ? "Bahnhofstrasse" : null,
                Number = "7",
                PostalCode = address ? "5400" : null,
                City = address ? "Baden" : null,
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

        var ids = new Seeded(
            Email: await Invoice("Email", "a@example.ch", address: false),
            EmailAndPost: await Invoice("Beides", "b@example.ch", address: true),
            PostOnly: await Invoice("Post", null, address: true),
            Nobody: await Invoice("Niemand", null, address: false),
            AlreadySent: await Invoice("Versendet", "c@example.ch", address: true, status: InvoiceStatus.Sent)
        );
        var factory = new AppFactory(_postgres);
        foreach (var id in new[] { ids.Email, ids.EmailAndPost, ids.PostOnly, ids.Nobody, ids.AlreadySent })
            await factory.InvoicePdfs.PutAsync(id, [1, 2, 3]);
        return (ids, factory);
    }

    private static HttpClient SignedIn(AppFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "anna@example.ch");
        return client;
    }

    [Fact]
    public async Task The_preview_counts_Created_Invoices_by_the_channel_each_would_take()
    {
        var (_, factory) = await Seed();
        await using var _f = factory;
        using var client = SignedIn(factory);

        var preview = await client.GetFromJsonAsync<JsonElement>("/api/invoices/bulk-send/preview");

        Assert.Equal(2, preview.GetProperty("email").GetInt32());
        Assert.Equal(1, preview.GetProperty("post").GetInt32());
        Assert.Equal(1, preview.GetProperty("notSendable").GetInt32());
    }

    private static Task<HttpResponseMessage> BulkSend(HttpClient client, string? subject = "Rechnung", string? text = "Guten Tag") =>
        client.PostAsJsonAsync("/api/invoices/bulk-send", new { subject, text });

    private static async Task<string> StatusRemark(HttpClient client, int id)
    {
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/invoices/{id}");
        var last = detail.GetProperty("statusHistory").EnumerateArray().Last();
        return $"{last.GetProperty("status").GetString()} {last.GetProperty("remark").GetString()}";
    }

    [Fact]
    public async Task Bulk_send_picks_email_first_then_post_and_records_Sent_with_the_matching_Remark()
    {
        var (ids, factory) = await Seed();
        await using var _f = factory;
        using var client = SignedIn(factory);

        var response = await BulkSend(client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            ["a@example.ch", "b@example.ch"],
            factory.Emails.Invoices.Select(m => m.Email.To).Order()
        );
        Assert.All(factory.Emails.Invoices, m => Assert.Equal("Rechnung", m.Email.Subject));
        Assert.Equal(1, factory.EPost.Calls);
        Assert.Equal("Sent per Email", await StatusRemark(client, ids.Email));
        Assert.Equal("Sent per Email", await StatusRemark(client, ids.EmailAndPost));
        Assert.Equal($"Sent per Post ({factory.EPost.DeliveryId})", await StatusRemark(client, ids.PostOnly));
        var summary = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, summary.GetProperty("sent").GetArrayLength());
        Assert.Equal(0, summary.GetProperty("failed").GetArrayLength());
    }

    [Fact]
    public async Task Bulk_send_skips_Invoices_nobody_can_receive_and_reports_the_reason()
    {
        var (ids, factory) = await Seed();
        await using var _f = factory;
        using var client = SignedIn(factory);

        var summary = await (await BulkSend(client)).Content.ReadFromJsonAsync<JsonElement>();

        var skipped = Assert.Single(summary.GetProperty("skipped").EnumerateArray());
        Assert.Equal(InvoiceRules.FormatNumber(ids.Nobody), skipped.GetProperty("number").GetString());
        Assert.Equal("Niemand Anna", skipped.GetProperty("person").GetString());
        Assert.Equal(
            "Die Person hat keine E-Mail-Adresse. Die Person hat keine vollständige Schweizer Adresse (Strasse, PLZ und Ort).",
            skipped.GetProperty("reason").GetString()
        );
        Assert.Equal("Created", (await client.GetFromJsonAsync<JsonElement>($"/api/invoices/{ids.Nobody}")).GetProperty("status").GetString());
        Assert.Equal("Sent", (await client.GetFromJsonAsync<JsonElement>($"/api/invoices/{ids.AlreadySent}")).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Subject_and_text_are_required_when_an_Invoice_goes_by_email()
    {
        var (_, factory) = await Seed();
        await using var _f = factory;
        using var client = SignedIn(factory);

        var response = await BulkSend(client, subject: " ", text: "Text");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Der Betreff ist erforderlich.", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
        Assert.Empty(factory.Emails.Invoices);
        Assert.Equal(0, factory.EPost.Calls);
    }
}
