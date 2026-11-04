using backend.Accounting;

namespace backend.Tests.Accounting;

public class ChartConsistencyTests
{
    private static readonly ChartOfAccounts Chart = ChartOfAccounts.Parse(
        """
        { "income": [ { "number": 3400, "name": "Mitgliederbeiträge" }, { "number": 3999, "name": "Alt", "inactive": true } ],
          "expense": [ { "number": 6010, "name": "Raummiete" } ] }
        """
    );

    private static readonly Dictionary<int, Category> Recorded = new()
    {
        [3400] = Category.Income,
        [3999] = Category.Income,
        [6010] = Category.Expense,
    };

    [Fact]
    public void A_consistent_chart_has_no_errors() => Assert.Null(ChartConsistency.Check(Chart, Recorded));

    [Fact]
    public void A_missing_used_account_is_reported_by_number()
    {
        var recorded = new Dictionary<int, Category>(Recorded) { [3500] = Category.Income, [6999] = Category.Expense };
        var error = ChartConsistency.Check(Chart, recorded);
        Assert.NotNull(error);
        Assert.Contains("3500", error);
        Assert.Contains("6999", error);
        Assert.DoesNotContain("3400", error);
    }

    [Fact]
    public void A_deactivated_account_still_used_is_allowed() =>
        Assert.Null(ChartConsistency.Check(Chart, new Dictionary<int, Category> { [3999] = Category.Income }));

    [Fact]
    public void A_renamed_account_is_allowed()
    {
        var renamed = ChartOfAccounts.Parse(
            """{ "income": [ { "number": 3400, "name": "Jahresbeiträge" } ], "expense": [ { "number": 6010, "name": "Miete" } ] }"""
        );
        Assert.Null(
            ChartConsistency.Check(
                renamed,
                new Dictionary<int, Category> { [3400] = Category.Income, [6010] = Category.Expense }
            )
        );
    }

    [Fact]
    public void A_used_account_that_changed_category_is_reported_by_number()
    {
        var recorded = new Dictionary<int, Category> { [3400] = Category.Expense, [6010] = Category.Expense };
        var error = ChartConsistency.Check(Chart, recorded);
        Assert.NotNull(error);
        Assert.Contains("3400", error);
        Assert.DoesNotContain("6010", error);
    }

    [Fact]
    public void Missing_and_changed_accounts_are_both_reported()
    {
        var recorded = new Dictionary<int, Category> { [3500] = Category.Income, [6010] = Category.Income };
        var error = ChartConsistency.Check(Chart, recorded)!;
        Assert.Contains("3500", error);
        Assert.Contains("6010", error);
    }
}
