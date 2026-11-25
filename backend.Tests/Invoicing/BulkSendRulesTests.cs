using backend.Invoicing;
using PersonEntity = backend.Person.Person;
using SettingsEntity = backend.Settings.Settings;

namespace backend.Tests.Invoicing;

public class BulkSendRulesTests
{
    private sealed class FakeSender(DeliveryChannel channel, bool configured, Func<PersonEntity, bool> canDeliver) : IInvoiceSender
    {
        public DeliveryChannel Channel => channel;
        public string NotConfiguredError => "nicht eingerichtet";
        public string CannotDeliverError => $"{channel} nicht möglich.";
        public bool IsConfigured(SettingsEntity settings) => configured;
        public bool CanDeliverTo(PersonEntity recipient) => canDeliver(recipient);
        public Task<InvoiceSendResult> SendAsync(SettingsEntity s, PersonEntity r, int n, InvoiceMessage m, byte[] pdf, CancellationToken ct) =>
            Task.FromResult(new InvoiceSendResult("ok", null));
    }

    private static IInvoiceSender Email(bool configured = true) =>
        new FakeSender(DeliveryChannel.Email, configured, p => p.Email is not null);

    private static IInvoiceSender Post(bool configured = true) =>
        new FakeSender(DeliveryChannel.Post, configured, p => p.Street is not null);

    private static PersonEntity Person(string? email = null, string? street = null) =>
        new() { FirstName = "Anna", LastName = "Muster", Email = email, Street = street };

    private static Invoice Invoice(int id, InvoiceStatus status = InvoiceStatus.Created) =>
        new() { Id = id, StatusChanges = [new InvoiceStatusChange { Status = status, Timestamp = DateTime.UtcNow }] };

    private static List<BulkSendItem> Plan(
        IEnumerable<(Invoice, PersonEntity)> invoices,
        params IInvoiceSender[] senders
    ) => BulkSendRules.Plan(invoices, new SettingsEntity(), senders);

    [Fact]
    public void Email_is_preferred_over_post_when_the_Recipient_can_receive_both()
    {
        var plan = Plan([(Invoice(1), Person("a@example.ch", "Bahnhofstrasse"))], Post(), Email());

        Assert.Equal(DeliveryChannel.Email, Assert.Single(plan).Channel);
    }

    [Fact]
    public void Post_is_used_when_the_Recipient_has_no_email_address()
    {
        var plan = Plan([(Invoice(1), Person(street: "Bahnhofstrasse"))], Email(), Post());

        Assert.Equal(DeliveryChannel.Post, Assert.Single(plan).Channel);
    }

    [Fact]
    public void An_unconfigured_channel_is_never_picked()
    {
        var plan = Plan([(Invoice(1), Person("a@example.ch", "Bahnhofstrasse"))], Email(configured: false), Post());

        Assert.Equal(DeliveryChannel.Post, Assert.Single(plan).Channel);
    }

    [Fact]
    public void An_Invoice_nobody_can_receive_is_skipped_with_every_configured_channels_reason()
    {
        var item = Assert.Single(Plan([(Invoice(1), Person())], Email(), Post()));

        Assert.Null(item.Channel);
        Assert.Equal("Email nicht möglich. Post nicht möglich.", item.SkipReason);
    }

    [Fact]
    public void An_Invoice_is_skipped_when_no_channel_is_configured()
    {
        var item = Assert.Single(Plan([(Invoice(1), Person("a@example.ch"))], Email(configured: false), Post(configured: false)));

        Assert.Equal("Kein Versandweg ist eingerichtet.", item.SkipReason);
    }

    [Theory]
    [InlineData(InvoiceStatus.Sent)]
    [InlineData(InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.Cancelled)]
    public void Only_Created_Invoices_are_planned(InvoiceStatus status) =>
        Assert.Empty(Plan([(Invoice(1, status), Person("a@example.ch"))], Email()));

    [Fact]
    public void The_preview_counts_each_channel_and_the_skipped_Invoices()
    {
        var plan = Plan(
            [
                (Invoice(1), Person("a@example.ch")),
                (Invoice(2), Person("b@example.ch")),
                (Invoice(3), Person(street: "Bahnhofstrasse")),
                (Invoice(4), Person()),
            ],
            Email(),
            Post()
        );

        Assert.Equal(new BulkSendPreview(2, 1, 1), BulkSendRules.Preview(plan));
    }

    [Fact]
    public void Subject_and_text_are_required_when_an_Invoice_goes_by_email()
    {
        var plan = Plan([(Invoice(1), Person("a@example.ch"))], Email());

        Assert.Equal("Der Betreff ist erforderlich.", BulkSendRules.ValidateMessage(plan, " ", "Text"));
        Assert.Equal("Der Text ist erforderlich.", BulkSendRules.ValidateMessage(plan, "Betreff", null));
        Assert.Null(BulkSendRules.ValidateMessage(plan, "Betreff", "Text"));
    }

    [Fact]
    public void Subject_and_text_are_not_required_when_no_Invoice_goes_by_email()
    {
        var plan = Plan([(Invoice(1), Person(street: "Bahnhofstrasse")), (Invoice(2), Person())], Email(), Post());

        Assert.Null(BulkSendRules.ValidateMessage(plan, null, null));
    }
}
