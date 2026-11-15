namespace backend.Accounting;

/// <summary>
/// Startup consistency check between the Chart of Accounts file and the Accounts used by Entries (ADR 0001, ADR 0002).
/// Pure: the caller supplies each used Account number with the Category recorded when it was first used.
/// </summary>
public static class ChartConsistency
{
    /// <summary>Returns a German error naming the offending Account numbers, or null when consistent.</summary>
    public static string? Check(ChartOfAccounts chart, IReadOnlyDictionary<int, Category> recordedCategories)
    {
        var missing = new List<int>();
        var changed = new List<int>();
        foreach (var (number, recorded) in recordedCategories.OrderBy(kv => kv.Key))
        {
            var account = chart.Find(number);
            if (account is null)
            {
                missing.Add(number);
            }
            else if (account.Category != recorded)
            {
                changed.Add(number);
            }
        }

        var problems = new List<string>();
        if (missing.Count > 0)
        {
            problems.Add($"Verwendete Konten fehlen im Kontoplan: {string.Join(", ", missing)}.");
        }
        if (changed.Count > 0)
        {
            problems.Add($"Verwendete Konten haben die Kategorie gewechselt: {string.Join(", ", changed)}.");
        }
        return problems.Count == 0 ? null : string.Join(" ", problems);
    }
}
