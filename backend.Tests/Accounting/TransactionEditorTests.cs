using backend.Accounting;
using backend.Invoicing;
using backend.Tests.Invoicing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;
using PersonEntity = backend.Person.Person;

namespace backend.Tests.Accounting;

/// <summary>
/// Creating, editing and deleting a Transaction against a real database (local PostgreSQL per README; CI provides a
/// Postgres service), with in-memory Receipt storage: what is refused, what is stored, and the Invoice it pays.
/// </summary>
public class TransactionEditorTests : IClassFixture<LocalPostgresFixture>
{
    private static readonly ChartOfAccounts Chart = ChartOfAccounts.Parse(
        """
        { "income": [ { "number": 3400, "name": "Jahresbeiträge" }, { "number": 3600, "name": "Spenden" } ],
          "expense": [ { "number": 6010, "name": "Miete" } ] }
        """
    );

    private static readonly DateOnly Date = new(2026, 2, 10);

    private readonly LocalPostgresFixture _fixture;
    private readonly InMemoryReceiptStorage _receipts = new();

    public TransactionEditorTests(LocalPostgresFixture fixture) => _fixture = fixture;

    private AppDbContext Database(params IInterceptor[] interceptors)
    {
        _fixture.SkipUnlessAvailable();
        return new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(_fixture.ConnectionString)
                .AddInterceptors(interceptors)
                .Options
        );
    }

    private TransactionEditor Editor(AppDbContext db) => new(db, Chart, _receipts);

    /// <summary>Empties the tables the editor reads and writes and stores the Settings unless told otherwise; the tests of this class share one database.</summary>
    private static async Task Club(AppDbContext db, bool settings = true)
    {
        await db.Invoices.ExecuteDeleteAsync();
        await db.Transactions.ExecuteDeleteAsync();
        await db.AccountUsages.ExecuteDeleteAsync();
        await db.Persons.ExecuteDeleteAsync();
        await db.Settings.ExecuteDeleteAsync();
        if (settings)
        {
            db.Settings.Add(
                new backend.Settings.Settings
                {
                    StartDate = new DateOnly(2026, 1, 1),
                    Iban = "CH9300762011623852957",
                    Name = "SC Wettingen",
                    Address = "Sportweg 12",
                    PostalCode = "5430",
                    City = "Wettingen",
                }
            );
            await db.SaveChangesAsync();
        }
    }

    private static async Task<int> SeedPerson(AppDbContext db)
    {
        var person = new PersonEntity { FirstName = "Anna", LastName = "Muster" };
        db.Persons.Add(person);
        await db.SaveChangesAsync();
        return person.Id;
    }

    /// <summary>An Invoice of 100.00 for a new Person: Created, plus <paramref name="status"/> if given.</summary>
    private static async Task<int> SeedInvoice(AppDbContext db, InvoiceStatus? status = null)
    {
        var invoice = new Invoice
        {
            PersonId = await SeedPerson(db),
            Reference = "RF000001",
            Lines = [new InvoiceLine { Text = "Jahresbeitrag", Amount = 100m, AccountNumber = 3400 }],
            StatusChanges = [new InvoiceStatusChange { Status = InvoiceStatus.Created, Timestamp = DateTime.UtcNow.AddDays(-2) }],
        };
        if (status is { } s)
            invoice.StatusChanges.Add(new InvoiceStatusChange { Status = s, Timestamp = DateTime.UtcNow.AddDays(-1) });
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice.Id;
    }

    /// <summary>A stored Transaction of 100.00 with one Entry on 3400; imported when a BankReference is given.</summary>
    private static async Task<Transaction> SeedTransaction(AppDbContext db, string? bankReference = null, string? text = null)
    {
        var transaction = new Transaction
        {
            Date = Date,
            Amount = 100m,
            Text = text,
            BankReference = bankReference,
            Entries = [new Entry { Text = "Beitrag", AccountNumber = 3400, Amount = 100m }],
        };
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();
        return transaction;
    }

    private static TransactionEntryInput Line(decimal amount = 100m, int account = 3400, int? id = null, string text = "Beitrag") =>
        new(id, text, account, amount);

    private static TransactionInput Input(
        TransactionEntryInput[]? entries = null,
        int? personId = null,
        int? invoiceId = null,
        decimal amount = 100m,
        string? text = null,
        DateOnly? date = null
    ) => new(date ?? Date, amount, text, personId, (entries ?? [Line()]).ToList(), invoiceId);

    private static string RefusedWith(TransactionSaveOutcome outcome) =>
        Assert.IsType<TransactionSaveOutcome.Refused>(outcome).Error;

    private static TransactionDto Saved(TransactionSaveOutcome outcome) =>
        Assert.IsType<TransactionSaveOutcome.Saved>(outcome).Transaction;

    private async Task<Invoice> SavedInvoice(int id)
    {
        await using var check = Database();
        return await check.Invoices.Include(i => i.StatusChanges).SingleAsync(i => i.Id == id);
    }

    private async Task<Transaction?> SavedTransaction(int id)
    {
        await using var check = Database();
        return await check.Transactions.Include(t => t.Entries).SingleOrDefaultAsync(t => t.Id == id);
    }

    private async Task<int> TransactionCount()
    {
        await using var check = Database();
        return await check.Transactions.CountAsync();
    }

    // Refused input

    [Fact]
    public async Task A_new_Transaction_whose_Entries_carry_ids_is_refused()
    {
        await using var db = Database();
        await Club(db);
        var existing = await SeedTransaction(db);

        var outcome = await Editor(db).CreateAsync(Input([Line(id: existing.Entries[0].Id)]));

        Assert.Equal("Neue Buchungen dürfen keine Id haben.", RefusedWith(outcome));
        Assert.Equal(1, await TransactionCount());
    }

    [Fact]
    public async Task A_Transaction_is_refused_before_the_Settings_are_saved()
    {
        await using var db = Database();
        await Club(db, settings: false);

        var outcome = await Editor(db).CreateAsync(Input());

        Assert.Equal("Bitte zuerst den Anfangssaldo erfassen.", RefusedWith(outcome));
        Assert.Equal(0, await TransactionCount());
    }

    [Fact]
    public async Task Entries_that_do_not_sum_to_the_amount_are_refused()
    {
        await using var db = Database();
        await Club(db);

        var outcome = await Editor(db).CreateAsync(Input([Line(60m)]));

        Assert.Equal(
            "Die Summe der Buchungen (60.00) stimmt nicht mit dem Betrag der Bewegung (100.00) überein.",
            RefusedWith(outcome)
        );
    }

    [Fact]
    public async Task A_new_Transaction_for_an_unknown_Person_is_refused()
    {
        await using var db = Database();
        await Club(db);

        var outcome = await Editor(db).CreateAsync(Input(personId: 999999));

        Assert.Equal("Die Person existiert nicht.", RefusedWith(outcome));
        Assert.Equal(0, await TransactionCount());
    }

    [Fact]
    public async Task An_edit_to_an_unknown_Person_is_refused_and_changes_nothing()
    {
        await using var db = Database();
        await Club(db);
        var transaction = await SeedTransaction(db);

        var outcome = await Editor(db).UpdateAsync(transaction.Id, Input(personId: 999999, text: "Neu"));

        Assert.Equal("Die Person existiert nicht.", RefusedWith(outcome));
        Assert.Null((await SavedTransaction(transaction.Id))!.Text);
    }

    [Fact]
    public async Task Editing_an_unknown_Transaction_finds_nothing()
    {
        await using var db = Database();
        await Club(db);

        Assert.IsType<TransactionSaveOutcome.NotFound>(await Editor(db).UpdateAsync(999999, Input()));
    }

    [Fact]
    public async Task An_Entry_id_of_another_Transaction_is_refused()
    {
        await using var db = Database();
        await Club(db);
        var transaction = await SeedTransaction(db);
        var other = await SeedTransaction(db);

        var outcome = await Editor(db).UpdateAsync(transaction.Id, Input([Line(id: other.Entries[0].Id)]));

        Assert.Equal("Eine Buchung gehört nicht zu dieser Bewegung.", RefusedWith(outcome));
    }

    [Fact]
    public async Task The_same_Entry_twice_is_refused()
    {
        await using var db = Database();
        await Club(db);
        var transaction = await SeedTransaction(db);
        var entryId = transaction.Entries[0].Id;

        var outcome = await Editor(db).UpdateAsync(transaction.Id, Input([Line(50m, id: entryId), Line(50m, id: entryId)]));

        Assert.Equal("Eine Buchung kommt mehrfach vor.", RefusedWith(outcome));
    }

    [Fact]
    public async Task A_changed_bank_fact_of_an_Imported_Transaction_is_refused_before_its_Entries_are_checked()
    {
        await using var db = Database();
        await Club(db);
        var imported = await SeedTransaction(db, bankReference: "TX-1", text: "Zahlung");

        var outcome = await Editor(db).UpdateAsync(imported.Id, Input([Line(id: 999999)], text: "Anders"));

        Assert.Equal(
            "Der Text einer importierten Bewegung ist eine Bankangabe und kann nicht geändert werden.",
            RefusedWith(outcome)
        );
        Assert.Equal("Zahlung", (await SavedTransaction(imported.Id))!.Text);
    }

    // Saving

    [Fact]
    public async Task Creating_a_Transaction_stores_it_with_its_Entries_and_trimmed_text()
    {
        await using var db = Database();
        await Club(db);
        var personId = await SeedPerson(db);

        var dto = Saved(
            await Editor(db).CreateAsync(Input([Line(70m, text: " Beitrag "), Line(30m, 3600, text: "Spende")], personId, text: "  Anna  "))
        );

        Assert.Equal("Anna", dto.Text);
        Assert.Equal(personId, dto.PersonId);
        Assert.Equal(100m, dto.Balance);
        Assert.Null(dto.InvoiceId);
        var saved = await SavedTransaction(dto.Id);
        Assert.Equal(dto.EntryIds, saved!.Entries.Select(e => e.Id).Order());
        Assert.Equal(["Beitrag", "Spende"], saved.Entries.OrderBy(e => e.Id).Select(e => e.Text));
    }

    [Fact]
    public async Task A_blank_text_is_stored_as_none()
    {
        await using var db = Database();
        await Club(db);

        var dto = Saved(await Editor(db).CreateAsync(Input(text: "   ")));

        Assert.Null(dto.Text);
    }

    [Fact]
    public async Task Editing_a_Transaction_changes_kept_Entries_adds_new_ones_and_deletes_left_out_ones()
    {
        await using var db = Database();
        await Club(db);
        var transaction = await SeedTransaction(db);
        db.JournalEntries.Add(new Entry { TransactionId = transaction.Id, Text = "Weg", AccountNumber = 3600, Amount = 0.5m });
        await db.SaveChangesAsync();
        var kept = transaction.Entries[0].Id;
        db.ChangeTracker.Clear();

        var dto = Saved(
            await Editor(db).UpdateAsync(
                transaction.Id,
                Input([Line(-80m, 6010, id: kept, text: "Miete"), Line(-40m, 6010, text: "Neu")], amount: -120m, text: "Bearbeitet")
            )
        );

        Assert.Equal(-120m, dto.Amount);
        Assert.Equal("Bearbeitet", dto.Text);
        var saved = await SavedTransaction(transaction.Id);
        Assert.Equal(
            [(kept, "Miete", -80m), (dto.EntryIds[1], "Neu", -40m)],
            saved!.Entries.OrderBy(e => e.Id).Select(e => (e.Id, e.Text, e.Amount))
        );
    }

    [Fact]
    public async Task Only_the_Entries_of_an_Imported_Transaction_can_be_edited()
    {
        await using var db = Database();
        await Club(db);
        var imported = await SeedTransaction(db, bankReference: "TX-1", text: "Zahlung");
        var entryId = imported.Entries[0].Id;
        db.ChangeTracker.Clear();

        // The same bank facts, the text as sent with surrounding blanks, and new Entries.
        var dto = Saved(
            await Editor(db).UpdateAsync(imported.Id, Input([Line(60m, id: entryId), Line(40m, 3600, text: "Spende")], text: " Zahlung "))
        );

        Assert.Equal("Zahlung", dto.Text);
        Assert.Equal("TX-1", dto.BankReference);
        Assert.Equal(2, (await SavedTransaction(imported.Id))!.Entries.Count);
    }

    [Fact]
    public async Task Editing_the_Entries_of_a_Transaction_leaves_its_Receipts_alone()
    {
        await using var db = Database();
        await Club(db);
        var transaction = await SeedTransaction(db);
        var receipt = new Receipt { TransactionId = transaction.Id, UploadedAt = DateTime.UtcNow };
        db.Receipts.Add(receipt);
        await db.SaveChangesAsync();
        await _receipts.PutAsync(receipt.Id, [1]);
        db.ChangeTracker.Clear();

        var dto = Saved(await Editor(db).UpdateAsync(transaction.Id, Input([Line(id: transaction.Entries[0].Id, text: "Neu")])));

        Assert.Equal([receipt.Id], dto.Receipts.Select(r => r.Id));
        Assert.Single(_receipts.Keys);
    }

    // AccountUsage (ADR 0002)

    [Fact]
    public async Task An_Account_is_recorded_with_its_Category_when_an_Entry_first_uses_it_and_dropped_when_none_does()
    {
        await using var db = Database();
        await Club(db);

        var dto = Saved(await Editor(db).CreateAsync(Input([Line(-100m, 6010)], amount: -100m)));

        await using (var check = Database())
        {
            var usage = await check.AccountUsages.SingleAsync();
            Assert.Equal((6010, Category.Expense), (usage.AccountNumber, usage.Category));
        }

        Assert.IsType<TransactionSaveOutcome.Saved>(await Editor(db).UpdateAsync(dto.Id, Input([Line(-100m, 6010)], amount: -100m)));
        await using (var check = Database())
        {
            Assert.Single(await check.AccountUsages.ToListAsync());
        }

        Assert.IsType<TransactionDeleteOutcome.Deleted>(await Editor(db).DeleteAsync(dto.Id));
        await using (var check = Database())
        {
            Assert.Empty(await check.AccountUsages.ToListAsync());
        }
    }

    // The Invoice it pays

    [Theory]
    [InlineData(null)]
    [InlineData(InvoiceStatus.Sent)]
    public async Task Saving_a_Transaction_with_an_open_Invoice_marks_it_Paid_by_that_Transaction(InvoiceStatus? status)
    {
        await using var db = Database();
        await Club(db);
        var invoiceId = await SeedInvoice(db, status);

        var dto = Saved(await Editor(db).CreateAsync(Input(invoiceId: invoiceId)));

        Assert.Equal(invoiceId, dto.InvoiceId);
        var invoice = await SavedInvoice(invoiceId);
        Assert.Equal(dto.Id, invoice.PaidBy);
        Assert.Equal(InvoiceStatus.Paid, InvoiceStatusRules.Current(invoice));
        Assert.Equal(InvoiceTime.TimestampFor(Date), invoice.StatusChanges.Single(s => s.Status == InvoiceStatus.Paid).Timestamp);
    }

    [Fact]
    public async Task Editing_a_Transaction_can_pay_an_Invoice_and_saving_it_again_with_that_Invoice_changes_nothing()
    {
        await using var db = Database();
        await Club(db);
        var invoiceId = await SeedInvoice(db);
        var transaction = await SeedTransaction(db);
        db.ChangeTracker.Clear();

        Assert.Equal(invoiceId, Saved(await Editor(db).UpdateAsync(transaction.Id, Input(invoiceId: invoiceId))).InvoiceId);
        db.ChangeTracker.Clear();
        var again = Saved(await Editor(db).UpdateAsync(transaction.Id, Input(invoiceId: invoiceId, text: "Nochmals")));

        Assert.Equal(invoiceId, again.InvoiceId);
        var invoice = await SavedInvoice(invoiceId);
        Assert.Equal(transaction.Id, invoice.PaidBy);
        Assert.Single(invoice.StatusChanges, s => s.Status == InvoiceStatus.Paid);
    }

    [Fact]
    public async Task Saving_without_an_Invoice_keeps_the_Invoice_it_already_pays()
    {
        await using var db = Database();
        await Club(db);
        var invoiceId = await SeedInvoice(db);
        var dto = Saved(await Editor(db).CreateAsync(Input(invoiceId: invoiceId)));
        db.ChangeTracker.Clear();

        var again = Saved(await Editor(db).UpdateAsync(dto.Id, Input()));

        Assert.Equal(invoiceId, again.InvoiceId);
        Assert.Equal(dto.Id, (await SavedInvoice(invoiceId)).PaidBy);
    }

    [Fact]
    public async Task A_Transaction_paying_an_unknown_Invoice_is_refused()
    {
        await using var db = Database();
        await Club(db);

        var outcome = await Editor(db).CreateAsync(Input(invoiceId: 999999));

        Assert.Equal("Die Rechnung existiert nicht.", RefusedWith(outcome));
        Assert.Equal(0, await TransactionCount());
    }

    [Theory]
    [InlineData(InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.Cancelled)]
    public async Task A_Paid_or_Cancelled_Invoice_cannot_be_paid(InvoiceStatus status)
    {
        await using var db = Database();
        await Club(db);
        var invoiceId = await SeedInvoice(db, status);

        var outcome = await Editor(db).CreateAsync(Input(invoiceId: invoiceId));

        Assert.Equal("Die Rechnung ist nicht offen.", RefusedWith(outcome));
        Assert.Equal(0, await TransactionCount());
        Assert.Null((await SavedInvoice(invoiceId)).PaidBy);
    }

    [Fact]
    public async Task An_Invoice_paid_by_another_Transaction_cannot_be_paid_again()
    {
        await using var db = Database();
        await Club(db);
        var invoiceId = await SeedInvoice(db);
        var first = Saved(await Editor(db).CreateAsync(Input(invoiceId: invoiceId)));
        var second = await SeedTransaction(db);
        db.ChangeTracker.Clear();

        var outcome = await Editor(db).UpdateAsync(second.Id, Input(invoiceId: invoiceId));

        Assert.Equal("Die Rechnung ist nicht offen.", RefusedWith(outcome));
        Assert.Equal(first.Id, (await SavedInvoice(invoiceId)).PaidBy);
    }

    [Fact]
    public async Task A_Transaction_that_already_pays_an_Invoice_cannot_pay_another()
    {
        await using var db = Database();
        await Club(db);
        var firstInvoice = await SeedInvoice(db);
        var otherInvoice = await SeedInvoice(db);
        var dto = Saved(await Editor(db).CreateAsync(Input(invoiceId: firstInvoice)));
        db.ChangeTracker.Clear();

        var outcome = await Editor(db).UpdateAsync(dto.Id, Input(invoiceId: otherInvoice));

        Assert.Equal("Die Bewegung bezahlt bereits eine andere Rechnung.", RefusedWith(outcome));
        Assert.Null((await SavedInvoice(otherInvoice)).PaidBy);
    }

    [Fact]
    public async Task When_marking_the_Invoice_Paid_fails_the_Transaction_is_not_stored_either()
    {
        await using var setup = Database();
        await Club(setup);
        var invoiceId = await SeedInvoice(setup);
        await using var db = Database(new FailOnPaidStatusChange());

        await Assert.ThrowsAsync<InvalidOperationException>(() => Editor(db).CreateAsync(Input(invoiceId: invoiceId)));

        Assert.Equal(0, await TransactionCount());
        await using var check = Database();
        Assert.Empty(await check.AccountUsages.ToListAsync());
        var invoice = await SavedInvoice(invoiceId);
        Assert.Null(invoice.PaidBy);
        Assert.True(InvoiceStatusRules.IsOpen(invoice));
    }

    [Fact]
    public async Task When_marking_the_Invoice_Paid_fails_an_edit_is_not_stored_either()
    {
        await using var setup = Database();
        await Club(setup);
        var invoiceId = await SeedInvoice(setup);
        var transaction = await SeedTransaction(setup);
        await using var db = Database(new FailOnPaidStatusChange());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Editor(db).UpdateAsync(transaction.Id, Input([Line(100m, 3600)], invoiceId: invoiceId, text: "Neu"))
        );

        var saved = await SavedTransaction(transaction.Id);
        Assert.Null(saved!.Text);
        Assert.Equal(3400, Assert.Single(saved.Entries).AccountNumber);
        Assert.Null((await SavedInvoice(invoiceId)).PaidBy);
    }

    /// <summary>Lets the save of the Paid status change fail, as a database error would.</summary>
    private sealed class FailOnPaidStatusChange : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        ) =>
            eventData.Context!.ChangeTracker.Entries<InvoiceStatusChange>().Any(e => e.State == EntityState.Added)
                ? throw new InvalidOperationException("Paid status change failed")
                : base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    // Deleting

    [Fact]
    public async Task Deleting_a_Transaction_deletes_its_Entries_and_Receipts_and_their_objects()
    {
        await using var db = Database();
        await Club(db);
        var transaction = await SeedTransaction(db);
        var other = await SeedTransaction(db);
        var receipts = new[] { transaction.Id, transaction.Id, other.Id }
            .Select(id => new Receipt { TransactionId = id, UploadedAt = DateTime.UtcNow })
            .ToList();
        db.Receipts.AddRange(receipts);
        await db.SaveChangesAsync();
        foreach (var receipt in receipts)
            await _receipts.PutAsync(receipt.Id, [1]);
        db.ChangeTracker.Clear();

        Assert.IsType<TransactionDeleteOutcome.Deleted>(await Editor(db).DeleteAsync(transaction.Id));

        Assert.Null(await SavedTransaction(transaction.Id));
        Assert.Equal([ReceiptStorageKey.For(receipts[2].Id)], _receipts.Keys);
        await using var check = Database();
        Assert.Equal([receipts[2].Id], await check.Receipts.Select(r => r.Id).ToListAsync());
        Assert.Equal(other.Entries[0].Id, await check.JournalEntries.Select(e => e.Id).SingleAsync());
    }

    [Fact]
    public async Task Deleting_an_unknown_Transaction_finds_nothing()
    {
        await using var db = Database();
        await Club(db);

        Assert.IsType<TransactionDeleteOutcome.NotFound>(await Editor(db).DeleteAsync(999999));
    }

    // Listing

    [Fact]
    public async Task Before_the_Settings_are_saved_no_Transaction_is_listed()
    {
        await using var db = Database();
        await Club(db, settings: false);
        await SeedTransaction(db);

        Assert.Empty(await Editor(db).ListAsync());
    }
}
