using backend.Invoicing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Accounting;

[ApiController]
[Route("api")]
public class AccountingController(
    AppDbContext db,
    ChartOfAccounts chart,
    BankImporter importer,
    IReceiptStorage receiptStorage
) : ControllerBase
{
    [HttpGet("accounts")]
    public IActionResult GetAccounts() => Ok(chart.Accounts);

    [HttpGet("entries")]
    public async Task<IActionResult> GetEntries(
        DateOnly? from,
        DateOnly? to,
        [FromQuery(Name = "account")] int? accountNumber,
        [FromQuery(Name = "q")] string? search
    )
    {
        var opening = await db.Settings.FindAsync(Settings.Settings.SingletonId);
        if (opening is null)
        {
            return Ok(Array.Empty<JournalEntryDto>());
        }

        var entries = await db.JournalEntries.Include(e => e.Transaction).ToListAsync();
        // Balances are computed over the whole journal; filters only hide rows.
        return Ok(
            JournalFilter
                .Apply(
                    AccountingCalculator.RunningBalances(opening, entries),
                    from,
                    to,
                    accountNumber,
                    search
                )
                .Select(r => new JournalEntryDto(
                    r.Entry.Id,
                    r.Entry.TransactionId,
                    r.Entry.Date,
                    r.Entry.Text,
                    r.Entry.AccountNumber,
                    r.Entry.Amount,
                    r.Balance
                ))
                .ToList()
        );
    }

    [HttpGet("expected-balance")]
    public async Task<IActionResult> GetExpectedBalance(DateOnly date)
    {
        var opening = await db.Settings.FindAsync(Settings.Settings.SingletonId);
        if (opening is null)
        {
            return NotFound();
        }

        var entries = await db
            .JournalEntries.Include(e => e.Transaction)
            .Where(e => e.Transaction.Date <= date)
            .ToListAsync();
        var transactions = await db.Transactions.Where(t => t.Date <= date).ToListAsync();

        var journalBalance = AccountingCalculator.ExpectedBalance(opening, entries, date);
        var transactionBalance = AccountingCalculator.TransactionBalance(
            opening,
            transactions,
            date
        );
        return Ok(
            new
            {
                date,
                balance = journalBalance,
                transactionBalance,
                differs = AccountingCalculator.BalancesDiffer(journalBalance, transactionBalance),
            }
        );
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions()
    {
        var opening = await db.Settings.FindAsync(Settings.Settings.SingletonId);
        return Ok(opening is null ? [] : await LoadTransactionDtosAsync(opening));
    }

    [HttpPost("transactions")]
    public async Task<IActionResult> CreateTransaction(TransactionInput input)
    {
        var lines = input.Entries ?? [];
        if (lines.Any(l => l.Id is not null))
        {
            return BadRequest(new { error = "Neue Buchungen dürfen keine Id haben." });
        }
        var opening = await db.Settings.FindAsync(Settings.Settings.SingletonId);
        var drafts = lines.Select(l => new EntryDraft(l.Text, l.AccountNumber, l.Amount)).ToList();
        if (
            TransactionRules.Validate(opening, chart, input.Date, input.Amount, drafts) is { } error
        )
        {
            return BadRequest(new { error });
        }
        if (input.PersonId is { } personId && !await db.Persons.AnyAsync(p => p.Id == personId))
        {
            return BadRequest(new { error = "Die Person existiert nicht." });
        }

        var transaction = new Transaction
        {
            Date = input.Date,
            Amount = input.Amount,
            Text = NormalizeText(input.Text),
            PersonId = input.PersonId,
            Entries = lines
                .Select(l => Line(new Entry(), l.Text, l.AccountNumber, l.Amount))
                .ToList(),
        };
        db.Transactions.Add(transaction);
        await AccountUsageSync.SaveEntryChangeAsync(db, chart);
        return Created(
            $"/api/transactions/{transaction.Id}",
            await LoadTransactionDtoAsync(opening!, transaction.Id)
        );
    }

    [HttpPut("transactions/{id:int}")]
    public async Task<IActionResult> UpdateTransaction(int id, TransactionInput input)
    {
        var transaction = await db
            .Transactions.Include(t => t.Entries)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (transaction is null)
        {
            return NotFound();
        }

        // The date, amount, text and person of an imported Transaction are bank facts and cannot change.
        if (
            TransactionRules.ValidateUnchangedBankFacts(
                transaction,
                input.Date,
                input.Amount,
                NormalizeText(input.Text),
                input.PersonId
            ) is
            { } bankFactError
        )
        {
            return BadRequest(new { error = bankFactError });
        }

        var lines = input.Entries ?? [];
        var stored = transaction.Entries.ToDictionary(e => e.Id);
        var givenIds = lines.Where(l => l.Id is not null).Select(l => l.Id!.Value).ToList();
        if (givenIds.Any(i => !stored.ContainsKey(i)))
        {
            return BadRequest(new { error = "Eine Buchung gehört nicht zu dieser Bewegung." });
        }
        if (givenIds.Distinct().Count() != givenIds.Count)
        {
            return BadRequest(new { error = "Eine Buchung kommt mehrfach vor." });
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
        if (
            TransactionRules.Validate(opening, chart, input.Date, input.Amount, drafts) is { } error
        )
        {
            return BadRequest(new { error });
        }
        if (input.PersonId is { } personId && !await db.Persons.AnyAsync(p => p.Id == personId))
        {
            return BadRequest(new { error = "Die Person existiert nicht." });
        }

        transaction.Date = input.Date;
        transaction.Amount = input.Amount;
        transaction.Text = NormalizeText(input.Text);
        transaction.PersonId = input.PersonId;
        foreach (var line in lines)
        {
            if (line.Id is { } entryId)
            {
                Line(stored[entryId], line.Text, line.AccountNumber, line.Amount);
            }
            else
            {
                transaction.Entries.Add(
                    Line(new Entry(), line.Text, line.AccountNumber, line.Amount)
                );
            }
        }
        db.JournalEntries.RemoveRange(stored.Values.Where(e => !givenIds.Contains(e.Id)));
        await AccountUsageSync.SaveEntryChangeAsync(db, chart);
        return Ok(await LoadTransactionDtoAsync(opening!, id));
    }

    // Deletes the Transaction together with any Entries and Receipts still stored for it (cascade), syncing AccountUsage.
    [HttpDelete("transactions/{id:int}")]
    public async Task<IActionResult> DeleteTransaction(int id)
    {
        var transaction = await db
            .Transactions.Include(t => t.Entries)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (transaction is null)
        {
            return NotFound();
        }

        // The Receipt rows cascade with the Transaction; their objects are deleted afterwards, best-effort.
        var receiptIds = await db
            .Receipts.Where(r => r.TransactionId == id)
            .Select(r => r.Id)
            .ToListAsync();
        db.JournalEntries.RemoveRange(transaction.Entries);
        db.Transactions.Remove(transaction);
        await AccountUsageSync.SaveEntryChangeAsync(db, chart);
        await DeleteReceiptObjectsAsync(receiptIds);
        return NoContent();
    }

    // Attaches one PDF Receipt to a Transaction (imported or not, booked or not): none of the bank facts change.
    // A row pointing at a missing file must never occur; an orphaned object is harmless. The object key needs the
    // row's Id, so the row is inserted inside a database transaction, the object is written, and only then the
    // transaction commits: the row becomes visible only once its object exists. A failed commit leaves the object
    // behind and is not cleaned up: the commit may have gone through unnoticed, and a row pointing at a missing
    // file is worse than an orphan.
    [HttpPost("transactions/{id:int}/receipts")]
    public async Task<IActionResult> UploadReceipt(int id, IFormFile? file, CancellationToken ct)
    {
        if (!await db.Transactions.AnyAsync(t => t.Id == id, ct))
        {
            return NotFound();
        }
        byte[] content = [];
        if (file is not null)
        {
            await using var upload = file.OpenReadStream();
            using var memory = new MemoryStream();
            await upload.CopyToAsync(memory, ct);
            content = memory.ToArray();
        }
        var existing = await db.Receipts.CountAsync(r => r.TransactionId == id, ct);
        if (ReceiptRules.Validate(content, existing) is { } error)
        {
            return BadRequest(new { error });
        }

        var receipt = new Receipt { TransactionId = id, UploadedAt = DateTime.UtcNow };
        await using var dbTransaction = await db.Database.BeginTransactionAsync(ct);
        db.Receipts.Add(receipt);
        await db.SaveChangesAsync(ct);
        await receiptStorage.PutAsync(receipt.Id, content, ct);
        await dbTransaction.CommitAsync(ct);
        return Ok(new ReceiptDto(receipt.Id, receipt.UploadedAt));
    }

    [HttpGet("transactions/receipts/{id:int}")]
    public async Task<IActionResult> GetReceipt(int id, CancellationToken ct)
    {
        if (!await db.Receipts.AnyAsync(r => r.Id == id, ct))
        {
            return NotFound();
        }
        var pdf = await receiptStorage.GetAsync(id, ct);
        if (pdf is null)
        {
            return NotFound();
        }
        // Set directly: File with a filename would emit "attachment".
        Response.Headers.ContentDisposition = $"inline; filename=\"Beleg-{id}.pdf\"";
        return File(pdf, "application/pdf");
    }

    // The row goes first, then the object best-effort: the reverse order could leave a row pointing at a missing file.
    [HttpDelete("transactions/receipts/{id:int}")]
    public async Task<IActionResult> DeleteReceipt(int id, CancellationToken ct)
    {
        var receipt = await db.Receipts.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (receipt is null)
        {
            return NotFound();
        }
        db.Receipts.Remove(receipt);
        await db.SaveChangesAsync(ct);
        await DeleteReceiptObjectsAsync([id]);
        return NoContent();
    }

    // Imports one Transaction per simple CAMT.053 entry of the eBanking zip or a single CAMT.053 XML file
    // (tickets #25, #88); a re-import skips transactions whose BankReference is already stored.
    [HttpPost("transactions/import")]
    public async Task<IActionResult> ImportTransactions(IFormFile file)
    {
        byte[] content;
        await using (var upload = file.OpenReadStream())
        using (var memory = new MemoryStream())
        {
            await upload.CopyToAsync(memory);
            content = memory.ToArray();
        }

        // Ticket #62: the response reports which Invoices were auto-booked (in import order) and how
        // many of the imported movements still have no Entries (credits and debits alike).
        return await importer.ImportAsync(content, file.FileName) switch
        {
            BankImportOutcome.SettingsMissing => BadRequest(
                new
                {
                    error = "Bitte zuerst die Einstellungen (Anfangssaldo, Startdatum, IBAN) speichern.",
                }
            ),
            BankImportOutcome.Rejected rejected => BadRequest(new { errors = rejected.Errors }),
            BankImportOutcome.Imported done => Ok(
                new
                {
                    imported = done.ImportedCount,
                    skippedDuplicates = done.SkippedDuplicates,
                    bookedInvoices = done.BookedInvoices,
                    unmatched = done.Unmatched,
                }
            ),
            _ => throw new InvalidOperationException(),
        };
    }

    // Sets the fields a client supplies for an Entry (already validated, so the text is not blank).
    private static Entry Line(Entry entry, string? text, int accountNumber, decimal amount)
    {
        entry.Text = text!.Trim();
        entry.AccountNumber = accountNumber;
        entry.Amount = amount;
        return entry;
    }

    // Best-effort: an object that stays behind is an unreferenced orphan, which is harmless and accepted.
    private async Task DeleteReceiptObjectsAsync(IEnumerable<int> ids)
    {
        foreach (var id in ids)
        {
            try
            {
                await receiptStorage.DeleteAsync(id);
            }
            catch
            {
                // See above.
            }
        }
    }

    private static string? NormalizeText(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private async Task<TransactionDto> LoadTransactionDtoAsync(Settings.Settings opening, int id) =>
        (await LoadTransactionDtosAsync(opening)).Single(t => t.Id == id);

    // All Transactions, newest first, each with the running balance after it.
    private async Task<List<TransactionDto>> LoadTransactionDtosAsync(Settings.Settings opening)
    {
        var transactions = await db
            .Transactions.Include(t => t.Entries)
            .AsNoTracking()
            .ToListAsync();
        var receipts = (await db.Receipts.AsNoTracking().OrderBy(r => r.Id).ToListAsync())
            .ToLookup(r => r.TransactionId);
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
                receipts[r.Transaction.Id].Select(x => new ReceiptDto(x.Id, x.UploadedAt)).ToList()
            ))
            .ToList();
    }
}

public record JournalEntryDto(
    int Id,
    int TransactionId,
    DateOnly Date,
    string Text,
    int AccountNumber,
    decimal Amount,
    decimal Balance
);

public record TransactionEntryInput(int? Id, string? Text, int AccountNumber, decimal Amount);

public record TransactionInput(
    DateOnly Date,
    decimal Amount,
    string? Text,
    int? PersonId,
    List<TransactionEntryInput>? Entries
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
    List<ReceiptDto> Receipts
);

public record ReceiptDto(int Id, DateTime UploadedAt);
