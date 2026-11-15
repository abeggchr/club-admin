using backend.Invoicing;
using Microsoft.EntityFrameworkCore;

namespace backend.Accounting;

/// <summary>The outcome of <see cref="BankImporter.ImportAsync"/>.</summary>
public abstract record BankImportOutcome
{
    /// <summary>The club Settings are not saved yet, so there is no Iban or start date to import against.</summary>
    public sealed record SettingsMissing : BankImportOutcome;

    /// <summary>The file was rejected; nothing was saved.</summary>
    public sealed record Rejected(IReadOnlyList<ImportError> Errors) : BankImportOutcome;

    /// <summary>The Transactions were saved: how many, how many were skipped as already stored, the Invoice Numbers that were auto-booked (in import order) and how many imported movements still have no Entries.</summary>
    public sealed record Imported(
        int ImportedCount,
        int SkippedDuplicates,
        IReadOnlyList<int> BookedInvoices,
        int Unmatched
    ) : BankImportOutcome;
}

/// <summary>
/// Imports an eBanking zip or a single CAMT.053 XML file into the database: loads what the import
/// validates against, runs <see cref="BankImport"/>, saves the new Transactions with their Entries and,
/// for each Invoice a payment covers, the Paid status change and PaidBy (tickets #25, #88, #62).
/// All or nothing: when the file is rejected or the save fails, no Transaction is stored.
/// </summary>
public class BankImporter(AppDbContext db, ChartOfAccounts chart)
{
    public async Task<BankImportOutcome> ImportAsync(byte[] content, string fileName)
    {
        var settings = await db.Settings.FindAsync(Settings.Settings.SingletonId);
        if (settings is null)
        {
            return new BankImportOutcome.SettingsMissing();
        }

        var existingReferences = await db
            .Transactions.Where(t => t.BankReference != null)
            .Select(t => t.BankReference!)
            .ToListAsync();
        var persons = await db.Persons.ToListAsync();

        // Invoices for matching: QR-bill reference, total, and positions. An Invoice that already
        // has a Paid status change is terminal (once Paid, always Paid) and is flagged so the matcher
        // never re-books or re-sets PaidBy, e.g. when the paying movement was deleted and re-imported.
        // existingPaid is 0 here: re-imports of the same movement are skipped via BankReference, so
        // the Entries of an already-imported movement are attributed to the Invoice by its Paid
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

        var result = BankImport.Import(
            content,
            new ImportSnapshot(settings, existingReferences, persons, importInvoices, chart),
            fileName
        );
        if (result.Errors.Count > 0)
        {
            return new BankImportOutcome.Rejected(result.Errors);
        }

        var saved = result
            .Transactions.Select(t =>
                (
                    Imported: t,
                    Movement: new Transaction
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
                    }
                )
            )
            .ToList();
        db.Transactions.AddRange(saved.Select(s => s.Movement));

        // The first fully-covering credit Transaction marks the Invoice Paid: once the Transactions are
        // saved and have their ids, append the Paid status change and record the Transaction's id as
        // PaidBy. Both saves share one database transaction, so the import is all or nothing.
        await AccountUsageSync.SaveEntryChangeAsync(
            db,
            chart,
            afterFirstSave: () =>
            {
                foreach (var (imported, movement) in saved)
                {
                    if (imported.PaidAt is null || imported.MatchedInvoiceId is null)
                    {
                        continue;
                    }

                    var invoice = invoices.Single(i => i.Id == imported.MatchedInvoiceId);
                    invoice.StatusChanges.Add(
                        new InvoiceStatusChange
                        {
                            Status = InvoiceStatus.Paid,
                            Timestamp = imported.PaidAt.Value,
                        }
                    );
                    invoice.PaidBy ??= movement.Id;
                }
            }
        );

        return new BankImportOutcome.Imported(
            result.Transactions.Count,
            result.SkippedDuplicates,
            BankImport.BookedInvoiceIds(result),
            BankImport.UnmatchedCount(result)
        );
    }
}
