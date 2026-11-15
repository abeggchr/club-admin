using Microsoft.EntityFrameworkCore;

namespace backend.Accounting;

public static class AccountUsageSync
{
    // Keeps the AccountUsage records in step with the Entries (ADR 0002): an Account's Category is recorded
    // when an Entry first uses it and the record is dropped once no Entry uses the Account any more.
    // Returns the recorded Category per used Account; a used Account missing from the Chart of Accounts gets a
    // placeholder (not persisted) so the consistency check reports it.
    public static Dictionary<int, Category> Sync(AppDbContext db, ChartOfAccounts chart)
    {
        var usages = db.AccountUsages.ToList();
        var used = db.JournalEntries.Select(e => e.AccountNumber).Distinct().ToList();
        var recordedNumbers = usages.Select(u => u.AccountNumber).ToList();

        var unused = AccountUsageRules.Unused(recordedNumbers, used);
        db.AccountUsages.RemoveRange(usages.Where(u => unused.Contains(u.AccountNumber)));
        usages.RemoveAll(u => unused.Contains(u.AccountNumber));

        var result = usages.ToDictionary(u => u.AccountNumber, u => u.Category);
        // Accounts used before their Category was recorded adopt the file's current Category.
        foreach (var number in AccountUsageRules.Unrecorded(recordedNumbers, used))
        {
            if (chart.Find(number) is { } account)
            {
                db.AccountUsages.Add(
                    new AccountUsage { AccountNumber = number, Category = account.Category }
                );
                result[number] = account.Category;
            }
            else
            {
                result[number] = Category.Income;
            }
        }
        return result;
    }

    // Saves a pending Entry change and the matching AccountUsage changes atomically.
    public static async Task SaveEntryChangeAsync(AppDbContext db, ChartOfAccounts chart)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.SaveChangesAsync();
        Sync(db, chart);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
