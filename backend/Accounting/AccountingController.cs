using backend.Invoicing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Accounting;

[ApiController]
[Route("api")]
public class AccountingController(AppDbContext db, ChartOfAccounts chart) : ControllerBase
{
    [HttpGet("accounts")]
    public IActionResult GetAccounts() => Ok(chart.Accounts);

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings() =>
        await db.Settings.FindAsync(Settings.SingletonId) is { } settings
            ? Ok(settings)
            : NotFound();

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(SettingsUpdate update)
    {
        var earliest = await db.Transactions.AnyAsync()
            ? await db.Transactions.MinAsync(t => t.Date)
            : (DateOnly?)null;
        if (
            SettingsRules.Validate(
                update.OpeningBalance,
                update.StartDate,
                update.Iban,
                update.Name,
                update.Address,
                update.PostalCode,
                update.City,
                earliest
            ) is
            { } error
        )
        {
            return BadRequest(new { error });
        }

        var settings = await db.Settings.FindAsync(Settings.SingletonId);
        if (settings is null)
        {
            settings = new Settings();
            db.Settings.Add(settings);
        }

        settings.OpeningBalance = update.OpeningBalance;
        settings.StartDate = update.StartDate;
        settings.Iban = Iban.Normalize(update.Iban)!;
        settings.Name = update.Name!.Trim();
        settings.Address = update.Address!.Trim();
        settings.PostalCode = update.PostalCode!.Trim();
        settings.City = update.City!.Trim();

        await db.SaveChangesAsync();
        return Ok(settings);
    }

    [HttpGet("entries")]
    public async Task<IActionResult> GetEntries(
        DateOnly? from,
        DateOnly? to,
        [FromQuery(Name = "account")] int? accountNumber,
        [FromQuery(Name = "q")] string? search
    )
    {
        var opening = await db.Settings.FindAsync(Settings.SingletonId);
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
        var opening = await db.Settings.FindAsync(Settings.SingletonId);
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
        var opening = await db.Settings.FindAsync(Settings.SingletonId);
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
        var opening = await db.Settings.FindAsync(Settings.SingletonId);
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

        // The date, amount, text and person of an imported Bewegung are bank facts and cannot change.
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

        var opening = await db.Settings.FindAsync(Settings.SingletonId);
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

    // Deletes the Bewegung together with any Buchungen still stored for it (cascade), syncing AccountUsage.
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

        db.JournalEntries.RemoveRange(transaction.Entries);
        db.Transactions.Remove(transaction);
        await AccountUsageSync.SaveEntryChangeAsync(db, chart);
        return NoContent();
    }

    // Imports one Bewegung per simple CAMT.053 entry of the eBanking zip (ticket #25); a re-import skips
    // transactions whose BankReference is already stored.
    [HttpPost("transactions/import")]
    public async Task<IActionResult> ImportTransactions(IFormFile file)
    {
        var settings = await db.Settings.FindAsync(Settings.SingletonId);
        if (settings is null)
        {
            return BadRequest(
                new
                {
                    error = "Bitte zuerst die Einstellungen (Anfangssaldo, Startdatum, IBAN) speichern.",
                }
            );
        }

        var existingReferences = await db
            .Transactions.Where(t => t.BankReference != null)
            .Select(t => t.BankReference!)
            .ToListAsync();
        var persons = await db.Persons.ToListAsync();

        // Rechnungen for matching: QR-bill reference, total, and positions. A Rechnung that already
        // has a Paid status change is terminal (once Paid, always Paid) and is flagged so the matcher
        // never re-books or re-sets PaidBy, e.g. when the paying movement was deleted and re-imported.
        // existingPaid is 0 here: re-imports of the same movement are skipped via BankReference, so
        // the Buchungen of an already-imported movement are attributed to the Rechnung by its Paid
        // status change, not by a foreign key.
        var invoices = await db
            .Invoices.Include(i => i.Lines)
            .Include(i => i.StatusChanges)
            .ToListAsync();
        var importInvoices = invoices
            .Where(i => !string.IsNullOrEmpty(i.Reference))
            .Select(i => new ImportInvoice(
                i.Id,
                i.Reference,
                i.Lines.Sum(l => l.Amount),
                0m,
                i.StatusChanges.Any(s => s.Status == InvoiceStatus.Paid),
                i.Lines.OrderBy(l => l.Position)
                    .Select(l => new ImportInvoiceLine(l.Text, l.AccountNumber, l.Amount))
                    .ToList()
            ))
            .ToList();
        byte[] zip;
        await using (var upload = file.OpenReadStream())
        using (var memory = new MemoryStream())
        {
            await upload.CopyToAsync(memory);
            zip = memory.ToArray();
        }

        var result = BankImport.Import(
            zip,
            new ImportSnapshot(settings, existingReferences, persons, importInvoices, chart)
        );
        if (result.Errors.Count > 0)
        {
            return BadRequest(new { errors = result.Errors });
        }

        db.Transactions.AddRange(
            result.Transactions.Select(t => new Transaction
            {
                Date = t.Date,
                Amount = t.Amount,
                Text = t.Text,
                BankReference = t.BankReference,
                PersonId = t.PersonId,
                Entries = t
                    .AutoEntries.Select(e => new Entry
                    {
                        Text = e.Text,
                        AccountNumber = e.AccountNumber,
                        Amount = e.Amount,
                    })
                    .ToList(),
            })
        );

        // The first fully-covering credit Bewegung marks the Rechnung Bezahlt: append the Paid status
        // change and record the Bewegung's id as PaidBy before the save, so the Bewegung and its
        // AutoEntries are created and the Paid status change + PaidBy are all persisted in the same
        // SaveEntryChangeAsync save. The newly added Bewegungen are still in the context, so their ids
        // are resolved as part of the save below.
        foreach (
            var t in result.Transactions.Where(t =>
                t.PaidAt is not null && t.MatchedInvoiceId is not null
            )
        )
        {
            var invoice = db.Invoices.Single(i => i.Id == t.MatchedInvoiceId);
            var movement = db.Transactions.Single(x => x.BankReference == t.BankReference);
            invoice.StatusChanges.Add(
                new InvoiceStatusChange { Status = InvoiceStatus.Paid, Timestamp = t.PaidAt!.Value }
            );
            if (invoice.PaidBy is null)
            {
                invoice.PaidBy = movement.Id;
            }
        }
        await AccountUsageSync.SaveEntryChangeAsync(db, chart);

        // Ticket #62: the response reports which Rechnungen were auto-booked (in import order) and how
        // many of the imported movements still have no Buchungen (credits and debits alike).
        var bookedInvoices = BankImport.BookedInvoiceIds(result);
        var unmatched = BankImport.UnmatchedCount(result);
        return Ok(
            new
            {
                imported = result.Transactions.Count,
                skippedDuplicates = result.SkippedDuplicates,
                bookedInvoices,
                unmatched,
            }
        );
    }

    // Sets the fields a client supplies for a Buchung (already validated, so the text is not blank).
    private static Entry Line(Entry entry, string? text, int accountNumber, decimal amount)
    {
        entry.Text = text!.Trim();
        entry.AccountNumber = accountNumber;
        entry.Amount = amount;
        return entry;
    }

    private static string? NormalizeText(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private async Task<TransactionDto> LoadTransactionDtoAsync(Settings opening, int id) =>
        (await LoadTransactionDtosAsync(opening)).Single(t => t.Id == id);

    // All Bewegungen, newest first, each with the Kontostand after it.
    private async Task<List<TransactionDto>> LoadTransactionDtosAsync(Settings opening)
    {
        var transactions = await db
            .Transactions.Include(t => t.Entries)
            .AsNoTracking()
            .ToListAsync();
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
                r.Transaction.Entries.Select(e => e.Id).Order().ToList()
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
    List<int> EntryIds
);

public record SettingsUpdate(
    decimal OpeningBalance,
    DateOnly StartDate,
    string? Iban,
    string? Name,
    string? Address,
    string? PostalCode,
    string? City
);
