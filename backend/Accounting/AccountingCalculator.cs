namespace backend.Accounting;

/// <summary>Pure accounting rules. No database or HTTP dependency.</summary>
public static class AccountingCalculator
{
    /// <summary>
    /// Erwarteter Kontostand on <paramref name="date"/>: the Anfangssaldo plus all Buchungen up to that date.
    /// Returns null before the Anfangssaldo start date, where no balance is defined.
    /// </summary>
    public static decimal? ExpectedBalance(Settings opening, DateOnly date)
    {
        if (date < opening.StartDate)
        {
            return null;
        }

        return opening.OpeningBalance;
    }

    /// <summary>Erwarteter Kontostand on <paramref name="date"/>, counting all entries dated up to and including it.</summary>
    public static decimal? ExpectedBalance(Settings opening, IEnumerable<Entry> entries, DateOnly date)
    {
        if (ExpectedBalance(opening, date) is not { } start)
        {
            return null;
        }

        return start + entries.Where(e => e.Date <= date).Sum(e => e.Amount);
    }

    /// <summary>
    /// The whole journal, newest first (same-day ties by creation order, i.e. Id), each row carrying the
    /// running Erwarteter Kontostand up to and including that row. Always takes the unfiltered journal.
    /// </summary>
    public static IReadOnlyList<JournalRow> RunningBalances(Settings opening, IEnumerable<Entry> entries)
    {
        var balance = opening.OpeningBalance;
        var rows = new List<JournalRow>();
        foreach (var entry in entries.OrderBy(e => e.Date).ThenBy(e => e.Id))
        {
            balance += entry.Amount;
            rows.Add(new JournalRow(entry, balance));
        }

        rows.Reverse();
        return rows;
    }
}

public record JournalRow(Entry Entry, decimal Balance);
