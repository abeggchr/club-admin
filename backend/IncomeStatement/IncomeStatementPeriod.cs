namespace backend.IncomeStatement;

/// <summary>
/// Pure period rules of the Income Statement: from the Settings Start Date through today, inclusive
/// on both ends. The caller supplies "today" from the outside; nothing here reads a clock.
/// </summary>
public static class IncomeStatementPeriod
{
    /// <summary>Whether an Entry date lies in the report period, Start Date and today inclusive.</summary>
    public static bool IsInPeriod(DateOnly date, DateOnly startDate, DateOnly today) =>
        date >= startDate && date <= today;
}
