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
                [new Group("I. Beiträge", [new Line("Jahresbeiträge", 3400)])]
            ),
            new Section(
                "Aufwand",
                [new Group("I. Anlässe", [new Line("Anlässe", 4000)]), new Group("II. Skihaus", [new Line("Miete", 6010)])]
            ),
        ]
    );

    // 3610 (Diverse Einnahmen) and 4099 (Diverses Clubbetrieb) are in the real chart but not in
    // the mapping above, so their Entries fall into the "Nicht zugeordnet" group (ticket #115).
    private static readonly IncomeStatementConfig UnmappedConfig = new(
        [
            new Section(
                "Ertrag",
                [new Group("I. Beiträge", [new Line("Jahresbeiträge", 3400)])]
            ),
            new Section(
                "Aufwand",
                [new Group("I. Anlässe", [new Line("Anlässe", 4000)])]
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

    [Fact]
    public void Unmapped_Accounts_with_Entries_appear_in_a_trailing_Nicht_zugeordnet_group()
    {
        var result = IncomeStatementCalculator.Compute(
            UnmappedConfig,
            Chart,
            [EntryFor(3400, 500m), EntryFor(3610, 42m), EntryFor(4000, -300m), EntryFor(4099, -18m)]
        );

        var revenueGroups = result.Sections[0].Groups;
        var unmappedRevenue = Assert.Single(revenueGroups, group => group.Label == "Nicht zugeordnet");
        Assert.Equal(revenueGroups.Count - 1, revenueGroups.ToList().IndexOf(unmappedRevenue));
        Assert.Equal(42m, unmappedRevenue.Total);

        var expenseGroups = result.Sections[1].Groups;
        var unmappedExpense = Assert.Single(expenseGroups, group => group.Label == "Nicht zugeordnet");
        Assert.Equal(expenseGroups.Count - 1, expenseGroups.ToList().IndexOf(unmappedExpense));
        Assert.Equal(18m, unmappedExpense.Total);
    }

    [Fact]
    public void The_Nicht_zugeordnet_group_is_hidden_when_it_has_no_Entries()
    {
        var result = IncomeStatementCalculator.Compute(
            UnmappedConfig,
            Chart,
            [EntryFor(3400, 500m), EntryFor(4000, -300m)]
        );

        Assert.DoesNotContain(result.Sections[0].Groups, group => group.Label == "Nicht zugeordnet");
        Assert.DoesNotContain(result.Sections[1].Groups, group => group.Label == "Nicht zugeordnet");
    }

    [Fact]
    public void Unmapped_amounts_count_toward_the_totals_and_the_overall_result()
    {
        var result = IncomeStatementCalculator.Compute(
            UnmappedConfig,
            Chart,
            [EntryFor(3400, 500m), EntryFor(3610, 42m), EntryFor(4000, -300m), EntryFor(4099, -18m)]
        );

        Assert.Equal(542m, result.TotalRevenue);
        Assert.Equal(318m, result.TotalExpense);
        Assert.Equal(224m, result.OverallResult);
    }

    [Fact]
    public void The_Nicht_zugeordnet_group_stays_in_the_simplified_view()
    {
        var result = IncomeStatementCalculator.Compute(
            UnmappedConfig,
            Chart,
            [EntryFor(3610, 42m), EntryFor(4099, -18m)],
            IncomeStatementView.Simplified
        );

        // Only the unmapped Accounts have Entries, so each section holds exactly the group.
        var revenueGroup = Assert.Single(result.Sections[0].Groups);
        Assert.Equal("Nicht zugeordnet", revenueGroup.Label);
        Assert.Equal(42m, revenueGroup.Total);

        var expenseGroup = Assert.Single(result.Sections[1].Groups);
        Assert.Equal("Nicht zugeordnet", expenseGroup.Label);
        Assert.Equal(18m, expenseGroup.Total);
    }
}
