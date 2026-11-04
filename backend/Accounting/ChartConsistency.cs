namespace backend.Accounting;

/// <summary>
/// The Kategorie a Konto had when a Buchung first used it. Basis for the Kategorie rule (ADR 0002).
/// </summary>
public class AccountUsage
{
    public int AccountNumber { get; set; }
    public Category Category { get; set; }
}

/// <summary>
/// Startup consistency check between the Kontoplan file and the Konten used by Buchungen (ADR 0001, ADR 0002).
/// Pure: the caller supplies each used Konto number with the Kategorie recorded when it was first used.
/// </summary>
public static class ChartConsistency
{
    /// <summary>Returns a German error naming the offending Konto numbers, or null when consistent.</summary>
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
