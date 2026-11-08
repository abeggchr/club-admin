namespace backend.Accounting;

/// <summary>
/// Narrows journal rows. Works on rows that already carry their running balance, so filtering
/// never changes a balance: it is always that of the whole journal.
/// </summary>
public static class JournalFilter
{
    public static IReadOnlyList<JournalRow> Apply(
        IEnumerable<JournalRow> rows,
        DateOnly? from,
        DateOnly? to,
        int? accountNumber,
        string? text
    )
    {
        var search = text?.Trim();
        return rows.Where(r =>
                (from is null || r.Entry.Date >= from)
                && (to is null || r.Entry.Date <= to)
                && (accountNumber is null || r.Entry.AccountNumber == accountNumber)
                && (string.IsNullOrEmpty(search) || r.Entry.Text.Contains(search, StringComparison.OrdinalIgnoreCase))
            )
            .ToList();
    }
}
