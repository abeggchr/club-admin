using backend.Accounting;

namespace backend.Tests.Accounting;

public class ExpectedBalanceWithEntriesTests
{
    private static readonly backend.Settings.Settings Opening = new()
    {
        OpeningBalance = 1000m,
        StartDate = new DateOnly(2026, 1, 1),
    };

    private static Entry E(int id, DateOnly date, decimal amount, int? transactionId = null) =>
        new()
        {
            Id = id,
            Transaction = new Transaction { Id = transactionId ?? id, Date = date },
            Text = "t",
            AccountNumber = 3400,
            Amount = amount,
        };

    private static readonly Entry[] Entries =
    [
        E(1, new DateOnly(2026, 1, 1), 100m),
        E(2, new DateOnly(2026, 3, 10), -30m),
        E(3, new DateOnly(2026, 3, 20), 5.50m),
    ];

    [Fact]
    public void Entry_on_the_opening_date_is_included()
    {
        Assert.Equal(
            1100m,
            AccountingCalculator.ExpectedBalance(Opening, Entries, new DateOnly(2026, 1, 1))
        );
    }

    [Fact]
    public void Entries_on_the_boundary_date_are_included()
    {
        Assert.Equal(
            1070m,
            AccountingCalculator.ExpectedBalance(Opening, Entries, new DateOnly(2026, 3, 10))
        );
    }

    [Fact]
    public void Entries_after_the_date_are_ignored()
    {
        Assert.Equal(
            1100m,
            AccountingCalculator.ExpectedBalance(Opening, Entries, new DateOnly(2026, 3, 9))
        );
    }

    [Fact]
    public void Date_after_all_entries_includes_everything()
    {
        Assert.Equal(
            1075.50m,
            AccountingCalculator.ExpectedBalance(Opening, Entries, new DateOnly(2026, 12, 31))
        );
    }

    [Fact]
    public void Before_the_opening_date_it_is_undefined()
    {
        Assert.Null(
            AccountingCalculator.ExpectedBalance(Opening, Entries, new DateOnly(2025, 12, 31))
        );
    }

    [Fact]
    public void Entries_are_counted_by_the_date_of_their_transaction()
    {
        var transaction = new Transaction
        {
            Id = 1,
            Date = new DateOnly(2026, 5, 1),
            Amount = -100m,
        };
        Entry[] split =
        [
            new()
            {
                Id = 1,
                Transaction = transaction,
                Text = "a",
                AccountNumber = 6010,
                Amount = -60m,
            },
            new()
            {
                Id = 2,
                Transaction = transaction,
                Text = "b",
                AccountNumber = 6010,
                Amount = -40m,
            },
        ];

        Assert.Equal(
            1000m,
            AccountingCalculator.ExpectedBalance(Opening, split, new DateOnly(2026, 4, 30))
        );
        Assert.Equal(
            900m,
            AccountingCalculator.ExpectedBalance(Opening, split, new DateOnly(2026, 5, 1))
        );
    }
}

public class RunningBalanceTests
{
    private static readonly backend.Settings.Settings Opening = new()
    {
        OpeningBalance = 1000m,
        StartDate = new DateOnly(2026, 1, 1),
    };

    private static Entry E(int id, DateOnly date, decimal amount, int? transactionId = null) =>
        new()
        {
            Id = id,
            Transaction = new Transaction { Id = transactionId ?? id, Date = date },
            Text = "t",
            AccountNumber = 3400,
            Amount = amount,
        };

    [Fact]
    public void Rows_are_newest_first_with_the_balance_up_to_each_row()
    {
        var rows = AccountingCalculator.RunningBalances(
            Opening,
            [E(2, new DateOnly(2026, 3, 10), -30m), E(1, new DateOnly(2026, 2, 1), 100m)]
        );

        Assert.Equal([2, 1], rows.Select(r => r.Entry.Id));
        Assert.Equal([1070m, 1100m], rows.Select(r => r.Balance));
    }

    [Fact]
    public void Same_day_entries_are_ordered_by_creation_newest_first()
    {
        var day = new DateOnly(2026, 2, 1);
        var rows = AccountingCalculator.RunningBalances(
            Opening,
            [E(7, day, 10m), E(9, day, 1m), E(8, day, 100m)]
        );

        Assert.Equal([9, 8, 7], rows.Select(r => r.Entry.Id));
        Assert.Equal([1111m, 1110m, 1010m], rows.Select(r => r.Balance));
    }

    [Fact]
    public void Entry_on_the_opening_date_starts_from_the_opening_amount()
    {
        var rows = AccountingCalculator.RunningBalances(
            Opening,
            [E(1, new DateOnly(2026, 1, 1), -5m)]
        );

        Assert.Equal(995m, Assert.Single(rows).Balance);
    }

    [Fact]
    public void Filtering_the_rows_afterwards_leaves_each_balance_unchanged()
    {
        var rows = AccountingCalculator.RunningBalances(
            Opening,
            [
                E(1, new DateOnly(2026, 2, 1), 100m),
                E(2, new DateOnly(2026, 3, 1), -30m),
                E(3, new DateOnly(2026, 4, 1), 7m),
            ]
        );

        var filtered = rows.Where(r => r.Entry.Id == 3).ToList();

        Assert.Equal(1077m, Assert.Single(filtered).Balance);
    }

    [Fact]
    public void Input_order_does_not_matter()
    {
        var a = E(1, new DateOnly(2026, 2, 1), 100m);
        var b = E(2, new DateOnly(2026, 3, 1), -30m);

        Assert.Equal(
            AccountingCalculator.RunningBalances(Opening, [a, b]).Select(r => r.Balance),
            AccountingCalculator.RunningBalances(Opening, [b, a]).Select(r => r.Balance)
        );
    }

    [Fact]
    public void Entries_are_ordered_by_the_date_of_their_transaction()
    {
        // Entry 1 was created first but its Transaction is the later one.
        var rows = AccountingCalculator.RunningBalances(
            Opening,
            [
                E(1, new DateOnly(2026, 3, 1), 10m, transactionId: 5),
                E(2, new DateOnly(2026, 2, 1), 100m, transactionId: 6),
            ]
        );

        Assert.Equal([1, 2], rows.Select(r => r.Entry.Id));
        Assert.Equal([1110m, 1100m], rows.Select(r => r.Balance));
    }

    [Fact]
    public void Same_day_entries_are_ordered_by_transaction_id_before_entry_id()
    {
        var day = new DateOnly(2026, 2, 1);
        var rows = AccountingCalculator.RunningBalances(
            Opening,
            [
                E(1, day, 1m, transactionId: 20),
                E(9, day, 10m, transactionId: 10),
                E(5, day, 100m, transactionId: 10),
            ]
        );

        // Ascending: (tx 10, entry 5), (tx 10, entry 9), (tx 20, entry 1).
        Assert.Equal([1, 9, 5], rows.Select(r => r.Entry.Id));
        Assert.Equal([1111m, 1110m, 1100m], rows.Select(r => r.Balance));
    }
}

public class TransactionBalanceAtDateTests
{
    private static readonly backend.Settings.Settings Opening = new()
    {
        OpeningBalance = 1000m,
        StartDate = new DateOnly(2026, 1, 1),
    };

    private static Transaction T(int id, DateOnly date, decimal amount) =>
        new()
        {
            Id = id,
            Date = date,
            Amount = amount,
        };

    private static readonly Transaction[] Transactions =
    [
        T(1, new DateOnly(2026, 1, 1), 100m),
        T(2, new DateOnly(2026, 3, 10), -30m),
        T(3, new DateOnly(2026, 3, 20), 5.50m),
    ];

    [Fact]
    public void Transaction_on_the_opening_date_is_included()
    {
        Assert.Equal(
            1100m,
            AccountingCalculator.TransactionBalance(Opening, Transactions, new DateOnly(2026, 1, 1))
        );
    }

    [Fact]
    public void Transactions_on_the_boundary_date_are_included()
    {
        Assert.Equal(
            1070m,
            AccountingCalculator.TransactionBalance(
                Opening,
                Transactions,
                new DateOnly(2026, 3, 10)
            )
        );
    }

    [Fact]
    public void Transactions_after_the_date_are_ignored()
    {
        Assert.Equal(
            1100m,
            AccountingCalculator.TransactionBalance(Opening, Transactions, new DateOnly(2026, 3, 9))
        );
    }

    [Fact]
    public void Date_after_all_transactions_includes_everything()
    {
        Assert.Equal(
            1075.50m,
            AccountingCalculator.TransactionBalance(
                Opening,
                Transactions,
                new DateOnly(2026, 12, 31)
            )
        );
    }

    [Fact]
    public void Before_the_opening_date_it_is_undefined()
    {
        Assert.Null(
            AccountingCalculator.TransactionBalance(
                Opening,
                Transactions,
                new DateOnly(2025, 12, 31)
            )
        );
    }

    [Fact]
    public void Input_order_does_not_matter()
    {
        Assert.Equal(
            AccountingCalculator.TransactionBalance(
                Opening,
                Transactions,
                new DateOnly(2026, 3, 10)
            ),
            AccountingCalculator.TransactionBalance(
                Opening,
                Transactions.ToList().AsEnumerable().Reverse(),
                new DateOnly(2026, 3, 10)
            )
        );
    }

    [Fact]
    public void No_transactions_give_the_opening_amount()
    {
        Assert.Equal(
            1000m,
            AccountingCalculator.TransactionBalance(Opening, [], new DateOnly(2026, 6, 30))
        );
    }
}

public class BalanceComparisonTests
{
    [Fact]
    public void Differing_balances_differ()
    {
        Assert.True(AccountingCalculator.BalancesDiffer(1070m, 1075.50m));
    }

    [Fact]
    public void Equal_balances_do_not_differ()
    {
        Assert.False(AccountingCalculator.BalancesDiffer(1075.50m, 1075.50m));
    }

    [Fact]
    public void Both_balances_undefined_do_not_differ()
    {
        Assert.False(AccountingCalculator.BalancesDiffer(null, null));
    }

    [Fact]
    public void A_defined_balance_differs_from_an_undefined_one()
    {
        Assert.True(AccountingCalculator.BalancesDiffer(1000m, null));
        Assert.True(AccountingCalculator.BalancesDiffer(null, 1000m));
    }
}

public class TransactionBalanceTests
{
    private static readonly backend.Settings.Settings Opening = new()
    {
        OpeningBalance = 1000m,
        StartDate = new DateOnly(2026, 1, 1),
    };

    private static Transaction T(int id, DateOnly date, decimal amount) =>
        new()
        {
            Id = id,
            Date = date,
            Amount = amount,
        };

    [Fact]
    public void Rows_are_newest_first_with_the_balance_up_to_each_transaction()
    {
        var rows = AccountingCalculator.TransactionBalances(
            Opening,
            [T(2, new DateOnly(2026, 3, 10), -30m), T(1, new DateOnly(2026, 2, 1), 100m)]
        );

        Assert.Equal([2, 1], rows.Select(r => r.Transaction.Id));
        Assert.Equal([1070m, 1100m], rows.Select(r => r.Balance));
    }

    [Fact]
    public void Same_day_transactions_are_ordered_by_id_newest_first()
    {
        var day = new DateOnly(2026, 2, 1);
        var rows = AccountingCalculator.TransactionBalances(
            Opening,
            [T(7, day, 10m), T(9, day, 1m), T(8, day, 100m)]
        );

        Assert.Equal([9, 8, 7], rows.Select(r => r.Transaction.Id));
        Assert.Equal([1111m, 1110m, 1010m], rows.Select(r => r.Balance));
    }

    [Fact]
    public void The_balance_starts_from_the_opening_amount()
    {
        var rows = AccountingCalculator.TransactionBalances(
            Opening,
            [T(1, new DateOnly(2026, 1, 1), -5m)]
        );

        Assert.Equal(995m, Assert.Single(rows).Balance);
    }

    [Fact]
    public void Input_order_does_not_matter()
    {
        var a = T(1, new DateOnly(2026, 2, 1), 100m);
        var b = T(2, new DateOnly(2026, 3, 1), -30m);

        Assert.Equal(
            AccountingCalculator.TransactionBalances(Opening, [a, b]).Select(r => r.Balance),
            AccountingCalculator.TransactionBalances(Opening, [b, a]).Select(r => r.Balance)
        );
    }

    [Fact]
    public void No_transactions_give_no_rows() =>
        Assert.Empty(AccountingCalculator.TransactionBalances(Opening, []));
}

public class UnbookedTransactionBalanceTests
{
    // A Transaction may have zero Entries ("Keine Buchung"): it counts in the bank's running balance and in the
    // balance at a date, and adds nothing to the journal until Entries exist (ticket #24).

    private static readonly backend.Settings.Settings Opening = new()
    {
        OpeningBalance = 1000m,
        StartDate = new DateOnly(2026, 1, 1),
    };

    private static Transaction T(int id, DateOnly date, decimal amount) =>
        new()
        {
            Id = id,
            Date = date,
            Amount = amount,
        };

    private static Entry E(int id, Transaction t, decimal amount) =>
        new()
        {
            Id = id,
            Transaction = t,
            Text = "t",
            AccountNumber = 3400,
            Amount = amount,
        };

    [Fact]
    public void A_transaction_without_entries_counts_in_the_running_balance()
    {
        var rows = AccountingCalculator.TransactionBalances(
            Opening,
            [
                T(1, new DateOnly(2026, 2, 1), 100m),
                T(2, new DateOnly(2026, 3, 1), -50m), // no Entries
            ]
        );

        Assert.Equal([2, 1], rows.Select(r => r.Transaction.Id));
        Assert.Equal([1050m, 1100m], rows.Select(r => r.Balance));
    }

    [Fact]
    public void A_transaction_without_entries_counts_in_the_balance_at_a_date()
    {
        Assert.Equal(
            950m,
            AccountingCalculator.TransactionBalance(
                Opening,
                [T(1, new DateOnly(2026, 3, 1), -50m)],
                new DateOnly(2026, 3, 31)
            )
        );
    }

    [Fact]
    public void A_transaction_without_entries_adds_nothing_to_the_journal()
    {
        var booked = T(1, new DateOnly(2026, 2, 1), 100m);
        var unbooked = T(2, new DateOnly(2026, 3, 1), -50m); // zero Entries
        var entries = new[] { E(1, booked, 100m) };

        var rows = AccountingCalculator.RunningBalances(Opening, entries);
        Assert.Equal([1], rows.Select(r => r.Entry.Id));
        Assert.Equal(1100m, rows.Single(r => r.Entry.Transaction.Id == booked.Id).Balance);
        Assert.Equal(
            1100m,
            AccountingCalculator.ExpectedBalance(Opening, entries, new DateOnly(2026, 12, 31))
        );
    }
}
