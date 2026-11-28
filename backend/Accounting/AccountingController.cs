using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Accounting;

[ApiController]
[Route("api")]
public class AccountingController(
    AppDbContext db,
    ChartOfAccounts chart,
    BankImporter importer,
    TransactionEditor editor,
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
    public async Task<IActionResult> GetTransactions() => Ok(await editor.ListAsync());

    [HttpPost("transactions")]
    public async Task<IActionResult> CreateTransaction(TransactionInput input) =>
        await editor.CreateAsync(input) switch
        {
            TransactionSaveOutcome.Refused refused => BadRequest(new { error = refused.Error }),
            TransactionSaveOutcome.Saved saved => Created($"/api/transactions/{saved.Transaction.Id}", saved.Transaction),
            _ => throw new InvalidOperationException(),
        };

    [HttpPut("transactions/{id:int}")]
    public async Task<IActionResult> UpdateTransaction(int id, TransactionInput input) =>
        await editor.UpdateAsync(id, input) switch
        {
            TransactionSaveOutcome.NotFound => NotFound(),
            TransactionSaveOutcome.Refused refused => BadRequest(new { error = refused.Error }),
            TransactionSaveOutcome.Saved saved => Ok(saved.Transaction),
            _ => throw new InvalidOperationException(),
        };

    // Deletes the Transaction together with any Entries and Receipts still stored for it.
    [HttpDelete("transactions/{id:int}")]
    public async Task<IActionResult> DeleteTransaction(int id) =>
        await editor.DeleteAsync(id) switch
        {
            TransactionDeleteOutcome.NotFound => NotFound(),
            TransactionDeleteOutcome.Deleted => NoContent(),
            _ => throw new InvalidOperationException(),
        };

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
        await receiptStorage.DeleteBestEffortAsync([id]);
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

