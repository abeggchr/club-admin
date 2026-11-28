using backend.Invoicing;
using Microsoft.EntityFrameworkCore;

namespace backend.Accounting;

/// <summary>The outcome of <see cref="TransactionEditor.CreateAsync"/> and <see cref="TransactionEditor.UpdateAsync"/>.</summary>
public abstract record TransactionSaveOutcome
{
    /// <summary>No Transaction has this id (update only).</summary>
    public sealed record NotFound : TransactionSaveOutcome;

    /// <summary>The input was refused; nothing was saved.</summary>
    public sealed record Refused(string Error) : TransactionSaveOutcome;

    /// <summary>The Transaction, its Entries and the Invoice it pays are saved.</summary>
    public sealed record Saved(TransactionDto Transaction) : TransactionSaveOutcome;
}

/// <summary>The outcome of <see cref="TransactionEditor.DeleteAsync"/>.</summary>
public abstract record TransactionDeleteOutcome
{
    /// <summary>No Transaction has this id.</summary>
    public sealed record NotFound : TransactionDeleteOutcome;

    /// <summary>The Transaction, its Entries and its Receipts are deleted.</summary>
    public sealed record Deleted : TransactionDeleteOutcome;
}

/// <summary>
/// Creates, edits and deletes a Transaction with its Entries: validates the input (<see cref="TransactionRules"/>,
/// the Person, the Invoice it pays), saves it together with the AccountUsage records (ADR 0002) and, for the Invoice
/// chosen in the Invoice picker, the Paid status change and PaidBy, all in one database transaction. Deleting also
/// removes the Transaction's Receipt objects from storage.
/// </summary>
public class TransactionEditor(AppDbContext db, ChartOfAccounts chart, IReceiptStorage receiptStorage)
{
    public async Task<TransactionSaveOutcome> CreateAsync(TransactionInput input)
    {
        var lines = input.Entries ?? [];
        if (TransactionRules.ValidateNewEntryIds(lines.Select(l => l.Id)) is { } idError)
        {
            return new TransactionSaveOutcome.Refused(idError);
        }
        var opening = await db.Settings.FindAsync(Settings.Settings.SingletonId);
        var drafts = lines.Select(l => new EntryDraft(l.Text, l.AccountNumber, l.Amount)).ToList();
        var error =
            TransactionRules.Validate(opening, chart, input.Date, input.Amount, drafts)
            ?? await ValidatePersonAsync(input.PersonId);
        if (error is not null)
        {
            return new TransactionSaveOutcome.Refused(error);
        }
        var (invoice, invoiceError) = await FindInvoiceToPayAsync(input.InvoiceId, null);
        if (invoiceError is not null)
        {
            return new TransactionSaveOutcome.Refused(invoiceError);
        }

        var transaction = new Transaction
        {
            Date = input.Date,
            Amount = input.Amount,
            Text = TransactionRules.NormalizeText(input.Text),
            PersonId = input.PersonId,
            Entries = lines.Select(l => Line(new Entry(), l)).ToList(),
        };
        db.Transactions.Add(transaction);
        await SaveAsync(transaction, invoice);
        return new TransactionSaveOutcome.Saved(await LoadDtoAsync(opening!, transaction.Id));
    }

    /// <summary>Entries with an id are edited, those without are added, and stored ones left out are deleted.</summary>
    public async Task<TransactionSaveOutcome> UpdateAsync(int id, TransactionInput input)
    {
        var transaction = await db.Transactions.Include(t => t.Entries).FirstOrDefaultAsync(t => t.Id == id);
        if (transaction is null)
        {
            return new TransactionSaveOutcome.NotFound();
        }

        var lines = input.Entries ?? [];
        var stored = transaction.Entries.ToDictionary(e => e.Id);
        var text = TransactionRules.NormalizeText(input.Text);
        // The date, amount, text and person of an imported Transaction are bank facts and cannot change.
        var shapeError =
            TransactionRules.ValidateUnchangedBankFacts(transaction, input.Date, input.Amount, text, input.PersonId)
            ?? TransactionRules.ValidateEntryIds(stored.Keys, lines.Select(l => l.Id));
        if (shapeError is not null)
        {
            return new TransactionSaveOutcome.Refused(shapeError);
        }

        var opening = await db.Settings.FindAsync(Settings.Settings.SingletonId);
        var drafts = lines
            .Select(l => new EntryDraft(
                l.Text,
                l.AccountNumber,
                l.Amount,
                l.Id is { } i ? stored[i].AccountNumber : null
            ))
            .ToList();
        var error =
            TransactionRules.Validate(opening, chart, input.Date, input.Amount, drafts)
            ?? await ValidatePersonAsync(input.PersonId);
        if (error is not null)
        {
            return new TransactionSaveOutcome.Refused(error);
        }
        var (invoice, invoiceError) = await FindInvoiceToPayAsync(input.InvoiceId, id);
        if (invoiceError is not null)
        {
            return new TransactionSaveOutcome.Refused(invoiceError);
        }

        transaction.Date = input.Date;
        transaction.Amount = input.Amount;
        transaction.Text = text;
        transaction.PersonId = input.PersonId;
        foreach (var line in lines)
        {
            if (line.Id is { } entryId)
            {
                Line(stored[entryId], line);
            }
            else
            {
                transaction.Entries.Add(Line(new Entry(), line));
            }
        }
        var kept = lines.Select(l => l.Id).OfType<int>().ToHashSet();
        db.JournalEntries.RemoveRange(stored.Values.Where(e => !kept.Contains(e.Id)));
        await SaveAsync(transaction, invoice);
        return new TransactionSaveOutcome.Saved(await LoadDtoAsync(opening!, id));
    }

    /// <summary>Deletes the Transaction together with its Entries and Receipts (cascade), syncing AccountUsage.</summary>
    public async Task<TransactionDeleteOutcome> DeleteAsync(int id)
    {
        var transaction = await db.Transactions.Include(t => t.Entries).FirstOrDefaultAsync(t => t.Id == id);
        if (transaction is null)
        {
            return new TransactionDeleteOutcome.NotFound();
        }

        // The Receipt rows cascade with the Transaction; their objects are deleted afterwards, best-effort.
        var receiptIds = await db.Receipts.Where(r => r.TransactionId == id).Select(r => r.Id).ToListAsync();
        db.JournalEntries.RemoveRange(transaction.Entries);
        db.Transactions.Remove(transaction);
        await AccountUsageSync.SaveEntryChangeAsync(db, chart);
        await receiptStorage.DeleteBestEffortAsync(receiptIds);
        return new TransactionDeleteOutcome.Deleted();
    }

    /// <summary>All Transactions, newest first, each with the running balance after it; none before the Settings are saved.</summary>
    public async Task<List<TransactionDto>> ListAsync()
    {
        var opening = await db.Settings.FindAsync(Settings.Settings.SingletonId);
        return opening is null ? [] : await LoadDtosAsync(opening);
    }

    private async Task<string?> ValidatePersonAsync(int? personId) =>
        personId is { } id && !await db.Persons.AnyAsync(p => p.Id == id) ? "Die Person existiert nicht." : null;

    // The Invoice a Transaction is saved as paying: it must exist and be open, or already be paid by this very
    // Transaction. A link is permanent (the Paid status is terminal), so a null id never removes it.
    private async Task<(Invoice? Invoice, string? Error)> FindInvoiceToPayAsync(int? invoiceId, int? transactionId)
    {
        if (invoiceId is not { } id)
        {
            return (null, null);
        }
        var invoice = await db.Invoices.Include(i => i.StatusChanges).FirstOrDefaultAsync(i => i.Id == id);
        if (invoice is null)
        {
            return (null, "Die Rechnung existiert nicht.");
        }
        if (transactionId is not null && invoice.PaidBy == transactionId)
        {
            return (null, null);
        }
        if (InvoiceStatusRules.ValidatePay(invoice) is { } error)
        {
            return (null, error);
        }
        var alreadyPays = transactionId is { } t && await db.Invoices.AnyAsync(i => i.PaidBy == t);
        return alreadyPays ? (null, "Die Bewegung bezahlt bereits eine andere Rechnung.") : (invoice, null);
    }

    // Once the Transaction has its id, the Invoice it pays is marked Paid in the same database transaction, so a
    // failure leaves neither the Transaction change nor the Paid status behind.
    private Task SaveAsync(Transaction transaction, Invoice? invoice) =>
        AccountUsageSync.SaveEntryChangeAsync(
            db,
            chart,
            afterFirstSave: () =>
            {
                if (invoice is not null && InvoiceStatusRules.MarkPaid(invoice, transaction.Id, transaction.Date) is { } error)
                {
                    throw new InvalidOperationException(error);
                }
            }
        );

    // Sets the fields a client supplies for an Entry (already validated, so the text is not blank).
    private static Entry Line(Entry entry, TransactionEntryInput line)
    {
        entry.Text = line.Text!.Trim();
        entry.AccountNumber = line.AccountNumber;
        entry.Amount = line.Amount;
        return entry;
    }

    private async Task<TransactionDto> LoadDtoAsync(Settings.Settings opening, int id) =>
        (await LoadDtosAsync(opening)).Single(t => t.Id == id);

    private async Task<List<TransactionDto>> LoadDtosAsync(Settings.Settings opening)
    {
        var transactions = await db.Transactions.Include(t => t.Entries).AsNoTracking().ToListAsync();
        var receipts = (await db.Receipts.AsNoTracking().OrderBy(r => r.Id).ToListAsync()).ToLookup(r =>
            r.TransactionId
        );
        var invoiceIds = (
            await db
                .Invoices.AsNoTracking()
                .Where(i => i.PaidBy != null)
                .Select(i => new { i.Id, PaidBy = i.PaidBy!.Value })
                .ToListAsync()
        )
            .GroupBy(i => i.PaidBy)
            .ToDictionary(g => g.Key, g => g.Min(i => i.Id));
        return AccountingCalculator
            .TransactionBalances(opening, transactions)
            .Select(r => new TransactionDto(
                r.Transaction.Id,
                r.Transaction.Date,
                r.Transaction.Amount,
                r.Transaction.Text,
                r.Transaction.PersonId,
                r.Transaction.BankReference,
                r.Balance,
                r.Transaction.Entries.Select(e => e.Id).Order().ToList(),
                receipts[r.Transaction.Id].Select(x => new ReceiptDto(x.Id, x.UploadedAt)).ToList(),
                invoiceIds.TryGetValue(r.Transaction.Id, out var invoiceId) ? invoiceId : null
            ))
            .ToList();
    }
}

public record TransactionEntryInput(int? Id, string? Text, int AccountNumber, decimal Amount);

public record TransactionInput(
    DateOnly Date,
    decimal Amount,
    string? Text,
    int? PersonId,
    List<TransactionEntryInput>? Entries,
    int? InvoiceId = null
);

public record TransactionDto(
    int Id,
    DateOnly Date,
    decimal Amount,
    string? Text,
    int? PersonId,
    string? BankReference,
    decimal Balance,
    List<int> EntryIds,
    List<ReceiptDto> Receipts,
    int? InvoiceId
);

public record ReceiptDto(int Id, DateTime UploadedAt);
