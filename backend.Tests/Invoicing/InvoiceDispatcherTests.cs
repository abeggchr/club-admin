using backend.Invoicing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using PersonEntity = backend.Person.Person;
using SettingsEntity = backend.Settings.Settings;

namespace backend.Tests.Invoicing;

/// <summary>
/// Sending one Invoice and the bulk send against a real database (local PostgreSQL per README; CI provides a
/// Postgres service), with in-memory senders and PDF storage: what is saved, when, and what is reported.
/// </summary>
public class InvoiceDispatcherTests : IClassFixture<LocalPostgresFixture>
{
    private static readonly DateTime Now = new(2026, 11, 25, 13, 3, 0, DateTimeKind.Utc);
    private static readonly byte[] Pdf = [1, 2, 3];
    private static readonly InvoiceMessage Message = new("Rechnung 0042", "Guten Tag");

    private readonly LocalPostgresFixture _fixture;
    private readonly InMemoryInvoiceStorage _storage = new();
    private readonly TestClock _clock = new(Now);

    public InvoiceDispatcherTests(LocalPostgresFixture fixture) => _fixture = fixture;

    private sealed class TestClock(DateTime now) : TimeProvider
    {
        public DateTime Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    private sealed class RecordingSender(DeliveryBackend backend = DeliveryBackend.SmtpEmail) : IInvoiceSender
    {
        public record Call(PersonEntity Person, InvoiceMessage Message, int Number, byte[] Pdf);

        public DeliveryBackend Backend => backend;
        public string? FailWith { get; set; }

        /// <summary>Runs on every call after it is recorded, so a test can throw, cancel or move the clock midway.</summary>
        public Func<Task>? During { get; set; }

        public List<Call> Calls { get; } = [];

        public async Task<InvoiceSendResult> SendAsync(
            SettingsEntity settings,
            PersonEntity person,
            int number,
            InvoiceMessage message,
            byte[] pdf,
            CancellationToken ct
        )
        {
            Calls.Add(new Call(person, message, number, pdf));
            if (During is not null)
                await During();
            return FailWith is not null
                ? new InvoiceSendResult(null, FailWith)
                : new InvoiceSendResult("per Email", null);
        }
    }

    private AppDbContext Database()
    {
        _fixture.SkipUnlessAvailable();
        return new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_fixture.ConnectionString).Options
        );
    }

    private InvoiceDispatcher Dispatcher(AppDbContext db, params IInvoiceSender[] senders) =>
        new(db, senders, _storage, _clock, NullLogger<InvoiceDispatcher>.Instance);

    /// <summary>Empties the tables the send reads and writes and stores the club Settings; the tests of this class share one database.</summary>
    private static async Task Club(AppDbContext db, bool smtp = true, bool epost = false, bool fromAddress = true)
    {
        await db.Invoices.ExecuteDeleteAsync();
        await db.Transactions.ExecuteDeleteAsync();
        await db.Persons.ExecuteDeleteAsync();
        await db.Settings.ExecuteDeleteAsync();
        db.Settings.Add(
            new SettingsEntity
            {
                StartDate = new DateOnly(2026, 1, 1),
                Iban = "CH9300762011623852957",
                Name = "SC Wettingen",
                Address = "Sportweg 12",
                PostalCode = "5430",
                City = "Wettingen",
                FromAddress = fromAddress ? "kasse@example.ch" : null,
                SmtpHost = smtp ? "smtp.example.ch" : null,
                SmtpPort = smtp ? 587 : null,
                EPostApiKey = epost ? "key-123" : null,
            }
        );
        await db.SaveChangesAsync();
    }

    /// <summary>A Created Invoice (plus <paramref name="status"/> if given) for a new Person, with its PDF stored unless told otherwise.</summary>
    private async Task<int> SeedInvoice(
        AppDbContext db,
        string lastName = "Muster",
        string? email = "anna@example.ch",
        bool address = true,
        InvoiceStatus? status = null,
        bool storePdf = true
    )
    {
        var person = new PersonEntity
        {
            FirstName = "Anna",
            LastName = lastName,
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
            StatusChanges = [new InvoiceStatusChange { Status = InvoiceStatus.Created, Timestamp = Now.AddDays(-2) }],
        };
        if (status is { } s)
            invoice.StatusChanges.Add(new InvoiceStatusChange { Status = s, Timestamp = Now.AddDays(-1) });
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        if (storePdf)
            await _storage.PutAsync(invoice.Id, Pdf);
        return invoice.Id;
    }

    /// <summary>The saved status changes of an Invoice, read through a fresh context so only committed rows count.</summary>
    private async Task<List<InvoiceStatusChange>> SavedStatusChanges(int id)
    {
        await using var check = Database();
        var invoice = await check.Invoices.Include(i => i.StatusChanges).SingleAsync(i => i.Id == id);
        return invoice.StatusChanges.OrderBy(s => s.Timestamp).ThenBy(s => s.Id).ToList();
    }

    private async Task<InvoiceStatus> SavedStatus(int id) => (await SavedStatusChanges(id)).Last().Status;

    // Sending one Invoice

    [Fact]
    public async Task Sending_an_unknown_Invoice_is_not_found()
    {
        await using var db = Database();
        await Club(db);
        var sender = new RecordingSender();

        var outcome = await Dispatcher(db, sender).SendAsync(999999, DeliveryChannel.Email, Message, CancellationToken.None);

        Assert.IsType<InvoiceSendOutcome.NotFound>(outcome);
        Assert.Empty(sender.Calls);
    }

    [Fact]
    public async Task A_successful_send_hands_the_stored_PDF_to_the_sender_and_saves_Sent_with_its_Remark_and_the_given_time()
    {
        await using var db = Database();
        await Club(db);
        var id = await SeedInvoice(db);
        var sender = new RecordingSender();

        var outcome = await Dispatcher(db, sender).SendAsync(id, DeliveryChannel.Email, Message, CancellationToken.None);

        var sent = Assert.IsType<InvoiceSendOutcome.Sent>(outcome);
        Assert.Equal("Sent", sent.Invoice.Status);
        var call = Assert.Single(sender.Calls);
        Assert.Equal(id, call.Number);
        Assert.Equal(Pdf, call.Pdf);
        Assert.Equal(Message, call.Message);
        var saved = (await SavedStatusChanges(id)).Last();
        Assert.Equal(InvoiceStatus.Sent, saved.Status);
        Assert.Equal(Now, saved.Timestamp);
        Assert.Equal("per Email", saved.Remark);
    }

    [Fact]
    public async Task Sending_again_adds_another_Sent_row()
    {
        await using var db = Database();
        await Club(db);
        var id = await SeedInvoice(db, status: InvoiceStatus.Sent);

        var outcome = await Dispatcher(db, new RecordingSender()).SendAsync(id, DeliveryChannel.Email, Message, CancellationToken.None);

        Assert.IsType<InvoiceSendOutcome.Sent>(outcome);
        Assert.Equal(2, (await SavedStatusChanges(id)).Count(s => s.Status == InvoiceStatus.Sent));
    }

    [Theory]
    [InlineData(InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.Cancelled)]
    public async Task A_Paid_or_Cancelled_Invoice_is_not_sent(InvoiceStatus status)
    {
        await using var db = Database();
        await Club(db);
        var id = await SeedInvoice(db, status: status);
        var sender = new RecordingSender();

        var outcome = await Dispatcher(db, sender).SendAsync(id, DeliveryChannel.Email, Message, CancellationToken.None);

        Assert.Equal(new InvoiceSendOutcome.Refused("Nur offene Rechnungen können versendet werden."), outcome);
        Assert.Empty(sender.Calls);
        Assert.Equal(2, (await SavedStatusChanges(id)).Count);
    }

    [Fact]
    public async Task A_missing_subject_is_refused_before_anything_is_sent()
    {
        await using var db = Database();
        await Club(db);
        var id = await SeedInvoice(db);
        var sender = new RecordingSender();

        var outcome = await Dispatcher(db, sender).SendAsync(id, DeliveryChannel.Email, new InvoiceMessage("", "Text"), CancellationToken.None);

        Assert.Equal(new InvoiceSendOutcome.Refused("Der Betreff ist erforderlich."), outcome);
        Assert.Empty(sender.Calls);
    }

    [Fact]
    public async Task An_unconfigured_channel_is_refused()
    {
        await using var db = Database();
        await Club(db, smtp: false);
        var id = await SeedInvoice(db);
        var sender = new RecordingSender();

        var outcome = await Dispatcher(db, sender).SendAsync(id, DeliveryChannel.Email, Message, CancellationToken.None);

        Assert.Equal(
            new InvoiceSendOutcome.Refused("E-Mail ist nicht eingerichtet: Es braucht eine Absenderadresse und entweder SMTP oder ePost."),
            outcome
        );
        Assert.Empty(sender.Calls);
        Assert.Single(await SavedStatusChanges(id));
    }

    [Fact]
    public async Task A_Recipient_the_channel_cannot_deliver_to_is_refused()
    {
        await using var db = Database();
        await Club(db);
        var id = await SeedInvoice(db, email: null);
        var sender = new RecordingSender();

        var outcome = await Dispatcher(db, sender).SendAsync(id, DeliveryChannel.Email, Message, CancellationToken.None);

        Assert.Equal(new InvoiceSendOutcome.Refused("Die Person hat keine E-Mail-Adresse."), outcome);
        Assert.Empty(sender.Calls);
    }

    [Fact]
    public async Task A_sender_failure_is_returned_and_recorded_nowhere()
    {
        await using var db = Database();
        await Club(db);
        var id = await SeedInvoice(db);
        var sender = new RecordingSender { FailWith = "Die E-Mail konnte nicht gesendet werden: Verbindung abgelehnt" };
        var dispatcher = Dispatcher(db, sender);

        var outcome = await dispatcher.SendAsync(id, DeliveryChannel.Email, Message, CancellationToken.None);

        Assert.Equal(new InvoiceSendOutcome.Refused(sender.FailWith), outcome);
        Assert.Single(await SavedStatusChanges(id));
        // Nothing pending either: a later save in the same request must not record the failed send.
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task A_missing_stored_PDF_is_reported_and_records_nothing()
    {
        await using var db = Database();
        await Club(db);
        var id = await SeedInvoice(db, storePdf: false);
        var sender = new RecordingSender();

        var outcome = await Dispatcher(db, sender).SendAsync(id, DeliveryChannel.Email, Message, CancellationToken.None);

        Assert.Equal(new InvoiceSendOutcome.Refused("Das PDF der Rechnung wurde nicht gefunden."), outcome);
        Assert.Empty(sender.Calls);
        Assert.Single(await SavedStatusChanges(id));
    }

    private async Task<(RecordingSender Smtp, RecordingSender EPostEmail, RecordingSender Letter, InvoiceSendOutcome Outcome)> SendToAll(
        bool smtp,
        DeliveryChannel channel,
        InvoiceMessage message
    )
    {
        await using var db = Database();
        await Club(db, smtp: smtp, epost: true);
        var id = await SeedInvoice(db);
        var smtpSender = new RecordingSender(DeliveryBackend.SmtpEmail);
        var ePostEmail = new RecordingSender(DeliveryBackend.EPostEmail);
        var letter = new RecordingSender(DeliveryBackend.EPostLetter);
        var outcome = await Dispatcher(db, smtpSender, ePostEmail, letter).SendAsync(id, channel, message, CancellationToken.None);
        return (smtpSender, ePostEmail, letter, outcome);
    }

    [Fact]
    public async Task A_post_letter_goes_to_the_letter_sender_and_needs_no_subject_or_text()
    {
        var (smtp, ePostEmail, letter, outcome) = await SendToAll(true, DeliveryChannel.Post, new InvoiceMessage(null, null));

        Assert.IsType<InvoiceSendOutcome.Sent>(outcome);
        Assert.Empty(smtp.Calls);
        Assert.Empty(ePostEmail.Calls);
        Assert.Single(letter.Calls);
    }

    [Fact]
    public async Task With_SMTP_and_an_ePost_key_email_goes_to_the_SMTP_sender()
    {
        var (smtp, ePostEmail, _, outcome) = await SendToAll(true, DeliveryChannel.Email, Message);

        Assert.IsType<InvoiceSendOutcome.Sent>(outcome);
        Assert.Single(smtp.Calls);
        Assert.Empty(ePostEmail.Calls);
    }

    [Fact]
    public async Task Without_SMTP_email_goes_to_the_ePost_email_sender()
    {
        var (smtp, ePostEmail, _, outcome) = await SendToAll(false, DeliveryChannel.Email, Message);

        Assert.IsType<InvoiceSendOutcome.Sent>(outcome);
        Assert.Empty(smtp.Calls);
        Assert.Single(ePostEmail.Calls);
    }

    // The bulk send

    [Fact]
    public async Task The_preview_counts_unsent_Invoices_by_the_channel_each_would_take()
    {
        await using var db = Database();
        await Club(db, epost: true);
        await SeedInvoice(db, "Email", address: false);
        await SeedInvoice(db, "Beides");
        await SeedInvoice(db, "Post", email: null);
        await SeedInvoice(db, "Niemand", email: null, address: false);
        await SeedInvoice(db, "Versendet", status: InvoiceStatus.Sent);

        var preview = await Dispatcher(db).PreviewBulkSendAsync(CancellationToken.None);

        Assert.Equal(new BulkSendPreview(Email: 2, Post: 1, NotSendable: 1), preview);
    }

    [Fact]
    public async Task A_bulk_send_with_an_email_refuses_a_missing_subject_and_sends_nothing()
    {
        await using var db = Database();
        await Club(db);
        await SeedInvoice(db);
        var sender = new RecordingSender();

        var result = await Dispatcher(db, sender).BulkSendAsync(new InvoiceMessage(" ", "Text"), CancellationToken.None);

        Assert.Equal(new BulkSendResult.Refused("Der Betreff ist erforderlich."), result);
        Assert.Empty(sender.Calls);
    }

    [Fact]
    public async Task A_bulk_send_by_post_only_needs_no_subject_or_text()
    {
        await using var db = Database();
        // Without the club address there is no email at all, so every Invoice goes by post.
        await Club(db, smtp: false, epost: true, fromAddress: false);
        var id = await SeedInvoice(db);
        var letter = new RecordingSender(DeliveryBackend.EPostLetter);

        var result = await Dispatcher(db, letter).BulkSendAsync(new InvoiceMessage(null, null), CancellationToken.None);

        var done = Assert.IsType<BulkSendResult.Done>(result);
        Assert.Single(done.Summary.Sent);
        Assert.Single(letter.Calls);
        Assert.Equal(InvoiceStatus.Sent, await SavedStatus(id));
    }

    [Fact]
    public async Task An_invoice_without_a_usable_channel_is_skipped_with_its_reason()
    {
        await using var db = Database();
        await Club(db, epost: true);
        var sendable = await SeedInvoice(db, "Email");
        var nobody = await SeedInvoice(db, "Niemand", email: null, address: false);
        var sender = new RecordingSender();

        var result = await Dispatcher(db, sender).BulkSendAsync(Message, CancellationToken.None);

        var summary = Assert.IsType<BulkSendResult.Done>(result).Summary;
        Assert.Equal(
            new BulkSendOutcome(
                InvoiceRules.FormatNumber(nobody),
                "Niemand Anna",
                "Die Person hat keine E-Mail-Adresse. Die Person hat keine vollständige Schweizer Adresse (Strasse, PLZ und Ort)."
            ),
            Assert.Single(summary.Skipped)
        );
        Assert.Equal(new BulkSendOutcome(InvoiceRules.FormatNumber(sendable), "Email Anna", null), Assert.Single(summary.Sent));
        Assert.Equal(InvoiceStatus.Created, await SavedStatus(nobody));
    }

    [Fact]
    public async Task A_failure_on_one_invoice_never_stops_the_rest()
    {
        await using var db = Database();
        await Club(db);
        var ids = new[] { await SeedInvoice(db, "Eins"), await SeedInvoice(db, "Zwei"), await SeedInvoice(db, "Drei") };
        var sender = new RecordingSender();
        sender.During = () =>
        {
            sender.FailWith = sender.Calls.Count == 2 ? "Die E-Mail konnte nicht gesendet werden: Postfach voll" : null;
            return sender.Calls.Count == 1 ? throw new InvalidOperationException("boom") : Task.CompletedTask;
        };

        var result = await Dispatcher(db, sender).BulkSendAsync(Message, CancellationToken.None);

        var summary = Assert.IsType<BulkSendResult.Done>(result).Summary;
        Assert.Equal(3, sender.Calls.Count);
        Assert.Equal(
            [
                InvoiceRules.FormatNumber(sender.Calls[0].Number) + " Die Rechnung konnte nicht versendet werden.",
                InvoiceRules.FormatNumber(sender.Calls[1].Number) + " Die E-Mail konnte nicht gesendet werden: Postfach voll",
            ],
            summary.Failed.Select(f => $"{f.Number} {f.Reason}")
        );
        Assert.Equal([InvoiceRules.FormatNumber(sender.Calls[2].Number)], summary.Sent.Select(s => s.Number));
        Assert.Equal(InvoiceStatus.Created, await SavedStatus(sender.Calls[0].Number));
        Assert.Equal(InvoiceStatus.Created, await SavedStatus(sender.Calls[1].Number));
        Assert.Equal(InvoiceStatus.Sent, await SavedStatus(sender.Calls[2].Number));
        Assert.Equal(ids.Order(), sender.Calls.Select(c => c.Number).Order());
    }

    [Fact]
    public async Task A_timeout_that_is_not_a_cancellation_fails_only_that_invoice()
    {
        await using var db = Database();
        await Club(db);
        await SeedInvoice(db, "Eins");
        await SeedInvoice(db, "Zwei");
        var sender = new RecordingSender();
        sender.During = () =>
            sender.Calls.Count == 1 ? throw new TaskCanceledException("The request timed out.") : Task.CompletedTask;

        var result = await Dispatcher(db, sender).BulkSendAsync(Message, CancellationToken.None);

        var summary = Assert.IsType<BulkSendResult.Done>(result).Summary;
        Assert.Equal("Die Rechnung konnte nicht versendet werden.", Assert.Single(summary.Failed).Reason);
        Assert.Single(summary.Sent);
    }

    [Fact]
    public async Task Invoices_sent_before_a_failure_stay_saved()
    {
        await using var db = Database();
        await Club(db);
        await SeedInvoice(db, "Eins");
        await SeedInvoice(db, "Zwei");
        var sender = new RecordingSender();
        InvoiceStatus? firstWhenSecondWasSent = null;
        sender.During = async () =>
        {
            if (sender.Calls.Count != 2)
                return;
            // What another request sees while the second Invoice is being sent.
            firstWhenSecondWasSent = await SavedStatus(sender.Calls[0].Number);
            throw new InvalidOperationException("boom");
        };

        await Dispatcher(db, sender).BulkSendAsync(Message, CancellationToken.None);

        Assert.Equal(InvoiceStatus.Sent, firstWhenSecondWasSent);
        Assert.Equal(InvoiceStatus.Sent, await SavedStatus(sender.Calls[0].Number));
        Assert.Equal(InvoiceStatus.Created, await SavedStatus(sender.Calls[1].Number));
    }

    [Fact]
    public async Task Cancellation_stops_the_bulk_send()
    {
        await using var db = Database();
        await Club(db);
        await SeedInvoice(db, "Eins");
        await SeedInvoice(db, "Zwei");
        await SeedInvoice(db, "Drei");
        using var cancel = new CancellationTokenSource();
        var sender = new RecordingSender();
        sender.During = () =>
        {
            if (sender.Calls.Count != 2)
                return Task.CompletedTask;
            cancel.Cancel();
            throw new OperationCanceledException(cancel.Token);
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Dispatcher(db, sender).BulkSendAsync(Message, cancel.Token)
        );

        Assert.Equal(2, sender.Calls.Count);
        Assert.Equal(InvoiceStatus.Sent, await SavedStatus(sender.Calls[0].Number));
        Assert.Equal(InvoiceStatus.Created, await SavedStatus(sender.Calls[1].Number));
    }

    [Fact]
    public async Task Each_Invoice_in_a_bulk_send_is_stamped_with_the_time_of_its_own_send()
    {
        await using var db = Database();
        await Club(db);
        await SeedInvoice(db, "Eins");
        await SeedInvoice(db, "Zwei");
        var sender = new RecordingSender();
        sender.During = () =>
        {
            _clock.Now = Now.AddMinutes(sender.Calls.Count);
            return Task.CompletedTask;
        };

        await Dispatcher(db, sender).BulkSendAsync(Message, CancellationToken.None);

        Assert.Equal(Now.AddMinutes(1), (await SavedStatusChanges(sender.Calls[0].Number)).Last().Timestamp);
        Assert.Equal(Now.AddMinutes(2), (await SavedStatusChanges(sender.Calls[1].Number)).Last().Timestamp);
    }
}
