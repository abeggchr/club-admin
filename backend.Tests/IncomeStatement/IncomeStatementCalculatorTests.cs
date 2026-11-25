using backend.Accounting;
using backend.IncomeStatement;

namespace backend.Tests.IncomeStatement;

/// <summary>The pure calculator: per-Account sign rules and the full tree, driven through Compute.</summary>
public class IncomeStatementCalculatorTests
{
    private static readonly ChartOfAccounts Chart = ChartOfAccounts.Load(
        Path.Combine(AppContext.BaseDirectory, "Accounting", "chart-of-accounts.json")
    );

    private static readonly IncomeStatementConfig Config = new(
        [
            new Section(
                "Ertrag",
                [new Group("I. Beiträge", [new Line("Jahresbeiträge", 3400), new Line("Diverse Einnahmen", 3610)])]
            ),
            new Section(
                "Aufwand",
                [new Group("I. Anlässe", [new Line("Anlässe", 4000)]), new Group("II. Skihaus", [new Line("Miete", 6010)])]
            ),
        ]
    );

    private static Entry EntryFor(int accountNumber, decimal amount) => new()
    {
        AccountNumber = accountNumber,
        Amount = amount,
        Transaction = new Transaction(),
    };

    [Fact]
    public void A_deficit_is_negative_and_totals_follow_the_sections()
    {
        var result = IncomeStatementCalculator.Compute(
            Config,
            Chart,
            [EntryFor(3400, 500m), EntryFor(4000, -300m), EntryFor(6010, -400m)]
        );

        Assert.Equal(500m, result.TotalRevenue);
        Assert.Equal(700m, result.TotalExpense);
        Assert.Equal(-200m, result.OverallResult);
    }

    [Fact]
    public void A_refund_reduces_the_side_of_its_Category()
    {
        // A debit on an Income Account reduces Ertrag; a credit on an Expense Account reduces Aufwand.
        var result = IncomeStatementCalculator.Compute(
            Config,
            Chart,
            [
                EntryFor(3400, 500m),
                EntryFor(3400, -50m),
                EntryFor(4000, -300m),
                EntryFor(4000, 30m),
            ]
        );

        Assert.Equal(450m, result.TotalRevenue);
        Assert.Equal(270m, result.TotalExpense);
    }

    [Fact]
    public void A_range_covers_the_Accounts_in_the_Chart_and_skips_missing_numbers()
    {
        var config = new IncomeStatementConfig(
            [new Section("Ertrag", [new Group("I. Diverse", [new Line("Diverse Einnahmen", 3610, 3620)])])]
        );

        var result = IncomeStatementCalculator.Compute(
            config,
            Chart,
            [EntryFor(3610, 42m)]
        );

        // 3620 is in the range but not in the Chart; the line still shows the found account.
        Assert.Equal(42m, result.TotalRevenue);
        var line = Assert.Single(result.Sections[0].Groups[0].Lines);
        Assert.Equal(42m, line.Amount);
    }
}
