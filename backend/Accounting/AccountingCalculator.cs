namespace backend.Accounting;

/// <summary>Pure accounting rules. No database or HTTP dependency.</summary>
public static class AccountingCalculator
{
    /// <summary>
    /// Expected Balance on <paramref name="date"/>: the Opening Balance plus all Entries up to that date.
    /// Returns null before the Opening Balance start date, where no balance is defined.
    /// </summary>
    public static decimal? ExpectedBalance(Settings.Settings opening, DateOnly date)
    {
        if (date < opening.StartDate)
        {
            return null;
        }

        return opening.OpeningBalance;
    }

    /// <summary>
    /// Expected Balance on <paramref name="date"/>, counting all Entries whose Transaction is dated up to
    /// and including it. The entries need their Transaction loaded.
    /// </summary>
    public static decimal? ExpectedBalance(
        Settings.Settings opening,
        IEnumerable<Entry> entries,
        DateOnly date
    )
    {
        if (ExpectedBalance(opening, date) is not { } start)
        {
            return null;
        }

        return start + entries.Where(e => e.Date <= date).Sum(e => e.Amount);
    }

    /// <summary>
    /// The bank's balance on <paramref name="date"/>: the Opening Balance plus all Transactions up to and including
    /// that date. Returns null before the Opening Balance start date, where no balance is defined.
    /// </summary>
    public static decimal? TransactionBalance(
        Settings.Settings opening,
        IEnumerable<Transaction> transactions,
        DateOnly date
    )
    {
        if (ExpectedBalance(opening, date) is not { } start)
        {
            return null;
        }

        return start + transactions.Where(t => t.Date <= date).Sum(t => t.Amount);
    }

    /// <summary>
    /// Whether the bank's view of the balance (Transactions) and the journal's view (Entries) on a date
    /// disagree, in which case the UI turns "Kontostand heute" red. Two undefined balances (before the
    /// Opening Balance) do not disagree.
    /// </summary>
    public static bool BalancesDiffer(decimal? expected, decimal? transactions)
    {
        return expected != transactions;
    }

    /// <summary>
    /// The whole journal, newest first, each row carrying the running Expected Balance up to and including
    /// that row. Ordered by Transaction date, then Transaction Id, then Entry Id (creation order). Always takes the
    /// unfiltered journal; the entries need their Transaction loaded.
    /// </summary>
    public static IReadOnlyList<JournalRow> RunningBalances(
        Settings.Settings opening,
        IEnumerable<Entry> entries
    )
    {
        var balance = opening.OpeningBalance;
        var rows = new List<JournalRow>();
        foreach (
            var entry in entries
                .OrderBy(e => e.Date)
                .ThenBy(e => e.Transaction.Id)
                .ThenBy(e => e.Id)
        )
        {
            balance += entry.Amount;
            rows.Add(new JournalRow(entry, balance));
        }

        rows.Reverse();
        return rows;
    }

    /// <summary>
    /// All Transactions, newest first (same-day ties by creation order, i.e. Id), each row carrying the running balance
    /// after it: the Opening Balance plus the amounts of all Transactions up to and including that one.
    /// </summary>
    public static IReadOnlyList<TransactionRow> TransactionBalances(
        Settings.Settings opening,
        IEnumerable<Transaction> transactions
    )
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
