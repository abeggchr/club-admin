using backend.Invoicing;
using PersonEntity = backend.Person.Person;
using SettingsEntity = backend.Settings.Settings;

namespace backend.Tests.Invoicing;

public class BulkSendRulesTests
{
    // Without a club address there is no email at all, even though the ePost key could deliver it.
    private static SettingsEntity Club(bool email = true, bool post = true) =>
        new()
        {
            FromAddress = email ? "kasse@example.ch" : null,
            SmtpHost = email ? "smtp.example.ch" : null,
            EPostApiKey = post ? "key-123" : null,
        };

    private static PersonEntity Person(string? email = null, bool address = false) =>
        new()
        {
            FirstName = "Anna",
            LastName = "Muster",
            Email = email,
            Street = address ? "Bahnhofstrasse" : null,
            PostalCode = address ? "5400" : null,
            City = address ? "Baden" : null,
        };

    private static Invoice Invoice(int id, InvoiceStatus status = InvoiceStatus.Created) =>
        new() { Id = id, StatusChanges = [new InvoiceStatusChange { Status = status, Timestamp = DateTime.UtcNow }] };

    private static List<BulkSendItem> Plan(IEnumerable<(Invoice, PersonEntity)> invoices, SettingsEntity? settings = null) =>
        BulkSendRules.Plan(invoices, settings ?? Club());

    [Fact]
    public void Email_is_preferred_over_post_when_the_Recipient_can_receive_both()
    {
        var plan = Plan([(Invoice(1), Person("a@example.ch", address: true))]);

        Assert.Equal(DeliveryChannel.Email, Assert.Single(plan).Channel);
    }

    [Fact]
    public void Post_is_used_when_the_Recipient_has_no_email_address()
    {
        var plan = Plan([(Invoice(1), Person(address: true))]);

        Assert.Equal(DeliveryChannel.Post, Assert.Single(plan).Channel);
    }

    [Fact]
    public void An_unconfigured_channel_is_never_picked()
    {
        var plan = Plan([(Invoice(1), Person("a@example.ch", address: true))], Club(email: false));

        Assert.Equal(DeliveryChannel.Post, Assert.Single(plan).Channel);
    }

    [Fact]
    public void An_Invoice_nobody_can_receive_is_skipped_with_every_configured_channels_reason()
    {
        var item = Assert.Single(Plan([(Invoice(1), Person())]));

        Assert.Null(item.Channel);
        Assert.Equal(
            "Die Person hat keine E-Mail-Adresse. Die Person hat keine vollständige Schweizer Adresse (Strasse, PLZ und Ort).",
            item.SkipReason
        );
    }

    [Fact]
    public void A_channel_that_is_not_configured_is_left_out_of_the_skip_reason()
    {
        var item = Assert.Single(Plan([(Invoice(1), Person("a@example.ch"))], Club(email: false)));

        Assert.Equal("Die Person hat keine vollständige Schweizer Adresse (Strasse, PLZ und Ort).", item.SkipReason);
    }

    [Fact]
    public void An_Invoice_is_skipped_when_no_channel_is_configured()
    {
        var item = Assert.Single(Plan([(Invoice(1), Person("a@example.ch"))], new SettingsEntity()));

        Assert.Equal("Kein Versandweg ist eingerichtet.", item.SkipReason);
    }

    [Theory]
    [InlineData(InvoiceStatus.Sent)]
    [InlineData(InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.Cancelled)]
    public void Only_Created_Invoices_are_planned(InvoiceStatus status) =>
        Assert.Empty(Plan([(Invoice(1, status), Person("a@example.ch"))]));

    [Fact]
    public void The_preview_counts_each_channel_and_the_skipped_Invoices()
    {
        var plan = Plan(
            [
                (Invoice(1), Person("a@example.ch")),
                (Invoice(2), Person("b@example.ch")),
                (Invoice(3), Person(address: true)),
                (Invoice(4), Person()),
            ]
        );

        Assert.Equal(new BulkSendPreview(2, 1, 1), BulkSendRules.Preview(plan));
    }

    [Fact]
    public void Subject_and_text_are_required_when_an_Invoice_goes_by_email()
    {
        var plan = Plan([(Invoice(1), Person("a@example.ch"))]);

        Assert.Equal("Der Betreff ist erforderlich.", BulkSendRules.ValidateMessage(plan, " ", "Text"));
        Assert.Equal("Der Text ist erforderlich.", BulkSendRules.ValidateMessage(plan, "Betreff", null));
        Assert.Null(BulkSendRules.ValidateMessage(plan, "Betreff", "Text"));
    }

    [Fact]
    public void Subject_and_text_are_not_required_when_no_Invoice_goes_by_email()
    {
        var plan = Plan([(Invoice(1), Person(address: true)), (Invoice(2), Person())]);

        Assert.Null(BulkSendRules.ValidateMessage(plan, null, null));
    }
}
