using backend.Accounting;

namespace backend.Tests.Accounting;

public class JournalFilterTests
{
    private static readonly Settings Opening = new() { OpeningBalance = 1000m, StartDate = new DateOnly(2026, 1, 1) };

    private static Entry E(int id, DateOnly date, string text, int account, decimal amount) =>
        new()
        {
            Id = id,
            Transaction = new Transaction { Id = id, Date = date },
            Text = text,
            AccountNumber = account,
            Amount = amount,
        };

    private static readonly IReadOnlyList<JournalRow> Rows = AccountingCalculator.RunningBalances(Opening,
    [
        E(1, new DateOnly(2026, 2, 1), "Jahresbeitrag Müller", 3400, 100m),
        E(2, new DateOnly(2026, 3, 1), "Miete März", 6010, -30m),
        E(3, new DateOnly(2026, 4, 1), "Spende", 3600, 7m),
    ]);

    private static int[] Ids(IEnumerable<JournalRow> rows) => rows.Select(r => r.Entry.Id).ToArray();

    [Fact]
    public void Date_range_is_inclusive_on_both_ends()
    {
        var result = JournalFilter.Apply(Rows, new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 1), null, null);
        Assert.Equal([2, 1], Ids(result));
    }

    [Fact]
    public void Open_ended_ranges_work()
    {
        Assert.Equal([3, 2], Ids(JournalFilter.Apply(Rows, new DateOnly(2026, 3, 1), null, null, null)));
        Assert.Equal([1], Ids(JournalFilter.Apply(Rows, null, new DateOnly(2026, 2, 28), null, null)));
    }

    [Fact]
    public void Account_filter_keeps_only_that_konto()
    {
        Assert.Equal([2], Ids(JournalFilter.Apply(Rows, null, null, 6010, null)));
    }

    [Fact]
    public void Text_search_ignores_case_and_matches_substrings()
    {
        Assert.Equal([2], Ids(JournalFilter.Apply(Rows, null, null, null, "miete")));
        Assert.Equal([1], Ids(JournalFilter.Apply(Rows, null, null, null, "MÜLLER")));
    }

    [Fact]
    public void Blank_search_does_not_filter()
    {
        Assert.Equal([3, 2, 1], Ids(JournalFilter.Apply(Rows, null, null, null, "  ")));
    }

    [Fact]
    public void Filters_combine()
    {
        var result = JournalFilter.Apply(Rows, new DateOnly(2026, 3, 1), null, 3600, "spen");
        Assert.Equal([3], Ids(result));
        Assert.Empty(JournalFilter.Apply(Rows, new DateOnly(2026, 4, 2), null, 3600, null));
    }

    [Fact]
    public void Balances_are_those_of_the_whole_journal()
    {
        var result = JournalFilter.Apply(Rows, null, null, 3400, null);
        Assert.Equal(1100m, Assert.Single(result).Balance);
        Assert.Equal(1077m, JournalFilter.Apply(Rows, null, null, 3600, null).Single().Balance);
    }
}
