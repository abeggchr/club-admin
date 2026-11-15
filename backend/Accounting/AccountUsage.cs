namespace backend.Accounting;

/// <summary>
/// The Category an Account had when an Entry first used it. Basis for the Category rule (ADR 0002).
/// A row exists only while at least one Entry uses the Account.
/// </summary>
public class AccountUsage
{
    public int AccountNumber { get; set; }
    public Category Category { get; set; }
}

/// <summary>Pure rules for keeping the AccountUsage records in step with the Entries (ADR 0002).</summary>
public static class AccountUsageRules
{
    /// <summary>Recorded Account numbers that no Entry uses any more; their records must be dropped.</summary>
    public static IReadOnlyList<int> Unused(IEnumerable<int> recorded, IEnumerable<int> usedByEntries)
    {
        var used = usedByEntries.ToHashSet();
        return recorded.Where(n => !used.Contains(n)).Distinct().Order().ToList();
    }

    /// <summary>Used Account numbers that have no record yet; their Category is recorded now.</summary>
    public static IReadOnlyList<int> Unrecorded(IEnumerable<int> recorded, IEnumerable<int> usedByEntries)
    {
        var known = recorded.ToHashSet();
        return usedByEntries.Where(n => !known.Contains(n)).Distinct().Order().ToList();
    }
}
