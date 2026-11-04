using backend.Accounting;

namespace backend.Tests.Accounting;

public class ExpectedBalanceWithEntriesTests
{
    private static readonly OpeningBalance Opening = new() { Amount = 1000m, StartDate = new DateOnly(2026, 1, 1) };

    private static Entry E(int id, DateOnly date, decimal amount) =>
        new() { Id = id, Date = date, Text = "t", AccountNumber = 3400, Amount = amount };

    private static readonly Entry[] Entries =
    [
        E(1, new DateOnly(2026, 1, 1), 100m),
        E(2, new DateOnly(2026, 3, 10), -30m),
        E(3, new DateOnly(2026, 3, 20), 5.50m),
    ];

    [Fact]
    public void Entry_on_the_opening_date_is_included()
    {
        Assert.Equal(1100m, AccountingCalculator.ExpectedBalance(Opening, Entries, new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void Entries_on_the_boundary_date_are_included()
    {
        Assert.Equal(1070m, AccountingCalculator.ExpectedBalance(Opening, Entries, new DateOnly(2026, 3, 10)));
    }

    [Fact]
    public void Entries_after_the_date_are_ignored()
    {
        Assert.Equal(1100m, AccountingCalculator.ExpectedBalance(Opening, Entries, new DateOnly(2026, 3, 9)));
    }

    [Fact]
    public void Date_after_all_entries_includes_everything()
    {
        Assert.Equal(1075.50m, AccountingCalculator.ExpectedBalance(Opening, Entries, new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void Before_the_opening_date_it_is_undefined()
    {
        Assert.Null(AccountingCalculator.ExpectedBalance(Opening, Entries, new DateOnly(2025, 12, 31)));
    }
}

public class RunningBalanceTests
{
    private static readonly OpeningBalance Opening = new() { Amount = 1000m, StartDate = new DateOnly(2026, 1, 1) };

    private static Entry E(int id, DateOnly date, decimal amount) =>
        new() { Id = id, Date = date, Text = "t", AccountNumber = 3400, Amount = amount };

    [Fact]
    public void Rows_are_newest_first_with_the_balance_up_to_each_row()
    {
        var rows = AccountingCalculator.RunningBalances(Opening,
        [
            E(2, new DateOnly(2026, 3, 10), -30m),
            E(1, new DateOnly(2026, 2, 1), 100m),
        ]);

        Assert.Equal([2, 1], rows.Select(r => r.Entry.Id));
        Assert.Equal([1070m, 1100m], rows.Select(r => r.Balance));
    }

    [Fact]
    public void Same_day_entries_are_ordered_by_creation_newest_first()
    {
        var day = new DateOnly(2026, 2, 1);
        var rows = AccountingCalculator.RunningBalances(Opening,
        [
            E(7, day, 10m),
            E(9, day, 1m),
            E(8, day, 100m),
        ]);

        Assert.Equal([9, 8, 7], rows.Select(r => r.Entry.Id));
        Assert.Equal([1111m, 1110m, 1010m], rows.Select(r => r.Balance));
    }

    [Fact]
    public void Entry_on_the_opening_date_starts_from_the_opening_amount()
    {
        var rows = AccountingCalculator.RunningBalances(Opening, [E(1, new DateOnly(2026, 1, 1), -5m)]);

        Assert.Equal(995m, Assert.Single(rows).Balance);
    }

    [Fact]
    public void Filtering_the_rows_afterwards_leaves_each_balance_unchanged()
    {
        var rows = AccountingCalculator.RunningBalances(Opening,
        [
            E(1, new DateOnly(2026, 2, 1), 100m),
            E(2, new DateOnly(2026, 3, 1), -30m),
            E(3, new DateOnly(2026, 4, 1), 7m),
        ]);

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
            AccountingCalculator.RunningBalances(Opening, [b, a]).Select(r => r.Balance));
    }
}
