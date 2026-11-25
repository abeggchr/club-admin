using backend.Invoicing;
using PersonEntity = backend.Person.Person;
using SettingsEntity = backend.Settings.Settings;

namespace backend.Tests.Invoicing;

public class InvoiceDispatcherTests
{
    private static readonly DateTime Now = new(2026, 11, 25, 13, 3, 0, DateTimeKind.Utc);

    private sealed class RecordingSender : IInvoiceSender
    {
        public record Call(PersonEntity Person, InvoiceMessage Message, int Number, byte[] Pdf);

        public DeliveryChannel Channel => DeliveryChannel.Email;
        public bool Configured { get; set; } = true;
        public bool CanDeliver { get; set; } = true;
        public string? FailWith { get; set; }
        public List<Call> Calls { get; } = [];

        public bool IsConfigured(SettingsEntity settings) => Configured;

        public bool CanDeliverTo(PersonEntity person) => CanDeliver;

        public Task<InvoiceSendResult> SendAsync(
            SettingsEntity settings,
            PersonEntity person,
            int number,
            InvoiceMessage message,
            byte[] pdf,
            CancellationToken ct
        )
        {
            if (FailWith is not null)
                return Task.FromResult(new InvoiceSendResult(null, FailWith));
            Calls.Add(new Call(person, message, number, pdf));
            return Task.FromResult(new InvoiceSendResult("per Email", null));
        }
    }

    private static readonly byte[] Pdf = [1, 2, 3];
    private static readonly InvoiceMessage Message = new("Rechnung 0042", "Guten Tag");

    private static async Task<(InvoiceDispatcher, RecordingSender, Invoice)> Setup(
        InvoiceStatus status = InvoiceStatus.Created,
        bool storePdf = true
    )
    {
        var sender = new RecordingSender();
        var storage = new InMemoryInvoiceStorage();
        if (storePdf)
            await storage.PutAsync(42, Pdf);
        var invoice = new Invoice
        {
            Id = 42,
            StatusChanges =
            [
                new InvoiceStatusChange { Status = InvoiceStatus.Created, Timestamp = Now.AddDays(-2) },
            ],
        };
        if (status != InvoiceStatus.Created)
            invoice.StatusChanges.Add(
                new InvoiceStatusChange { Status = status, Timestamp = Now.AddDays(-1) }
            );
        return (new InvoiceDispatcher([sender], storage), sender, invoice);
    }

    private static Task<string?> Send(InvoiceDispatcher dispatcher, Invoice invoice, InvoiceMessage? message = null) =>
        dispatcher.SendAsync(
            invoice,
            new PersonEntity { FirstName = "Anna", LastName = "Muster", Email = "anna@example.ch" },
            new SettingsEntity(),
            DeliveryChannel.Email,
            message ?? Message,
            Now,
            CancellationToken.None
        );

    [Fact]
    public async Task A_successful_send_hands_the_stored_PDF_to_the_sender_and_records_Sent_with_its_Remark()
    {
        var (dispatcher, sender, invoice) = await Setup();

        var error = await Send(dispatcher, invoice);

        Assert.Null(error);
        var call = Assert.Single(sender.Calls);
        Assert.Equal(42, call.Number);
        Assert.Equal(Pdf, call.Pdf);
        Assert.Equal(Message, call.Message);
        var sent = invoice.StatusChanges.Last();
        Assert.Equal(InvoiceStatus.Sent, sent.Status);
        Assert.Equal(Now, sent.Timestamp);
        Assert.Equal("per Email", sent.Remark);
    }

    [Fact]
    public async Task Sending_again_adds_another_Sent_row()
    {
        var (dispatcher, _, invoice) = await Setup(InvoiceStatus.Sent);

        Assert.Null(await Send(dispatcher, invoice));

        Assert.Equal(2, invoice.StatusChanges.Count(s => s.Status == InvoiceStatus.Sent));
    }

    [Theory]
    [InlineData(InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.Cancelled)]
    public async Task A_Paid_or_Cancelled_Rechnung_is_not_sent(InvoiceStatus status)
    {
        var (dispatcher, sender, invoice) = await Setup(status);
        var rows = invoice.StatusChanges.Count;

        Assert.Equal("Nur offene Rechnungen können versendet werden.", await Send(dispatcher, invoice));

        Assert.Empty(sender.Calls);
        Assert.Equal(rows, invoice.StatusChanges.Count);
    }

    [Fact]
    public async Task A_missing_subject_is_refused_before_anything_is_sent()
    {
        var (dispatcher, sender, invoice) = await Setup();

        Assert.Equal("Der Betreff ist erforderlich.", await Send(dispatcher, invoice, new InvoiceMessage("", "Text")));

        Assert.Empty(sender.Calls);
    }

    [Fact]
    public async Task An_unconfigured_channel_is_refused()
    {
        var (dispatcher, sender, invoice) = await Setup();
        sender.Configured = false;

        Assert.Equal("E-Mail ist nicht eingerichtet.", await Send(dispatcher, invoice));

        Assert.Empty(sender.Calls);
        Assert.Single(invoice.StatusChanges);
    }

    [Fact]
    public async Task A_Recipient_the_channel_cannot_deliver_to_is_refused()
    {
        var (dispatcher, sender, invoice) = await Setup();
        sender.CanDeliver = false;

        Assert.Equal("Die Person hat keine E-Mail-Adresse.", await Send(dispatcher, invoice));

        Assert.Empty(sender.Calls);
        Assert.Single(invoice.StatusChanges);
    }

    [Fact]
    public async Task A_sender_failure_is_returned_and_records_nothing()
    {
        var (dispatcher, sender, invoice) = await Setup();
        sender.FailWith = "Die E-Mail konnte nicht gesendet werden: Verbindung abgelehnt";

        Assert.Equal(sender.FailWith, await Send(dispatcher, invoice));

        Assert.Single(invoice.StatusChanges);
    }

    [Fact]
    public async Task A_missing_stored_PDF_is_reported_and_records_nothing()
    {
        var (dispatcher, sender, invoice) = await Setup(storePdf: false);

        Assert.Equal("Das PDF der Rechnung wurde nicht gefunden.", await Send(dispatcher, invoice));

        Assert.Empty(sender.Calls);
        Assert.Single(invoice.StatusChanges);
    }
}
