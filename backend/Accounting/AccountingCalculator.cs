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

    /// <summary>
    /// Erwarteter Kontostand on <paramref name="date"/>, counting all Buchungen whose Bewegung is dated up to
    /// and including it. The entries need their Bewegung loaded.
    /// </summary>
    public static decimal? ExpectedBalance(Settings opening, IEnumerable<Entry> entries, DateOnly date)
    {
        if (ExpectedBalance(opening, date) is not { } start)
        {
            return null;
        }

        return start + entries.Where(e => e.Date <= date).Sum(e => e.Amount);
    }

    /// <summary>
    /// The bank's balance on <paramref name="date"/>: the Anfangssaldo plus all Bewegungen up to and including
    /// that date. Returns null before the Anfangssaldo start date, where no balance is defined.
    /// </summary>
    public static decimal? TransactionBalance(Settings opening, IEnumerable<Transaction> transactions, DateOnly date)
    {
        if (ExpectedBalance(opening, date) is not { } start)
        {
            return null;
        }

        return start + transactions.Where(t => t.Date <= date).Sum(t => t.Amount);
    }

    /// <summary>
    /// Whether the bank's view of the balance (Bewegungen) and the journal's view (Buchungen) on a date
    /// disagree, in which case the UI turns "Kontostand heute" red. Two undefined balances (before the
    /// Anfangssaldo) do not disagree.
    /// </summary>
    public static bool BalancesDiffer(decimal? expected, decimal? transactions)
    {
        return expected != transactions;
    }

    /// <summary>
    /// The whole journal, newest first, each row carrying the running Erwarteter Kontostand up to and including
    /// that row. Ordered by Bewegung date, then Bewegung Id, then Buchung Id (creation order). Always takes the
    /// unfiltered journal; the entries need their Bewegung loaded.
    /// </summary>
    public static IReadOnlyList<JournalRow> RunningBalances(Settings opening, IEnumerable<Entry> entries)
    {
        var balance = opening.OpeningBalance;
        var rows = new List<JournalRow>();
        foreach (var entry in entries.OrderBy(e => e.Date).ThenBy(e => e.Transaction.Id).ThenBy(e => e.Id))
        {
            balance += entry.Amount;
            rows.Add(new JournalRow(entry, balance));
        }

        rows.Reverse();
        return rows;
    }

    /// <summary>
    /// All Bewegungen, newest first (same-day ties by creation order, i.e. Id), each row carrying the Kontostand
    /// after it: the Anfangssaldo plus the amounts of all Bewegungen up to and including that one.
    /// </summary>
    public static IReadOnlyList<TransactionRow> TransactionBalances(Settings opening, IEnumerable<Transaction> transactions)
    {
        var balance = opening.OpeningBalance;
        var rows = new List<TransactionRow>();
        foreach (var transaction in transactions.OrderBy(t => t.Date).ThenBy(t => t.Id))
        {
            balance += transaction.Amount;
            rows.Add(new TransactionRow(transaction, balance));
        }

        rows.Reverse();
        return rows;
    }
}

public record TransactionRow(Transaction Transaction, decimal Balance);

public record JournalRow(Entry Entry, decimal Balance);
