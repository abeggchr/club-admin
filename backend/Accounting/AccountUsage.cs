namespace backend.Accounting;

/// <summary>
/// The Kategorie a Konto had when a Buchung first used it. Basis for the Kategorie rule (ADR 0002).
/// A row exists only while at least one Buchung uses the Konto.
/// </summary>
public class AccountUsage
{
    public int AccountNumber { get; set; }
    public Category Category { get; set; }
}

/// <summary>Pure rules for keeping the AccountUsage records in step with the Buchungen (ADR 0002).</summary>
public static class AccountUsageRules
{
    /// <summary>Recorded Konto numbers that no Buchung uses any more; their records must be dropped.</summary>
    public static IReadOnlyList<int> Unused(IEnumerable<int> recorded, IEnumerable<int> usedByEntries)
    {
        var used = usedByEntries.ToHashSet();
        return recorded.Where(n => !used.Contains(n)).Distinct().Order().ToList();
    }

    /// <summary>Used Konto numbers that have no record yet; their Kategorie is recorded now.</summary>
    public static IReadOnlyList<int> Unrecorded(IEnumerable<int> recorded, IEnumerable<int> usedByEntries)
    {
        var known = recorded.ToHashSet();
        return usedByEntries.Where(n => !known.Contains(n)).Distinct().Order().ToList();
    }
}
