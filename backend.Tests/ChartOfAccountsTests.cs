using backend.Accounting;

namespace backend.Tests;

public class ChartOfAccountsTests
{
    private const string Sample = """
        {
          "income": [ { "number": 3400, "name": "Jahresbeiträge" } ],
          "expense": [
            { "number": 6010, "name": "Miete" },
            { "number": 6999, "name": "Altes Konto", "inactive": true }
          ]
        }
        """;

    [Fact]
    public void Parse_assigns_category_and_active_state()
    {
        var chart = ChartOfAccounts.Parse(Sample);

        Assert.Equal(
            [
                new Account(3400, "Jahresbeiträge", Category.Income, true),
                new Account(6010, "Miete", Category.Expense, true),
                new Account(6999, "Altes Konto", Category.Expense, false),
            ],
            chart.Accounts
        );
    }

    [Fact]
    public void Inactive_account_is_still_resolvable_by_number()
    {
        var chart = ChartOfAccounts.Parse(Sample);

        var account = chart.Find(6999);

        Assert.NotNull(account);
        Assert.False(account.Active);
    }

    [Fact]
    public void Find_returns_null_for_unknown_number()
    {
        Assert.Null(ChartOfAccounts.Parse(Sample).Find(1));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{ "income": [ { "number": 1 } ], "expense": [] }""")]
    [InlineData("""{ "income": [ { "number": 1, "name": " " } ], "expense": [] }""")]
    [InlineData("""{ "income": [ { "number": 0, "name": "x" } ], "expense": [] }""")]
    public void Malformed_file_is_rejected(string json)
    {
        Assert.Throws<ChartOfAccountsException>(() => ChartOfAccounts.Parse(json));
    }

    [Fact]
    public void Duplicate_number_is_rejected_and_named()
    {
        var json = """
            { "income": [ { "number": 3400, "name": "A" } ],
              "expense": [ { "number": 3400, "name": "B" } ] }
            """;

        var e = Assert.Throws<ChartOfAccountsException>(() => ChartOfAccounts.Parse(json));
        Assert.Contains("3400", e.Message);
    }

    [Fact]
    public void Checked_in_plan_has_7_income_and_12_expense_accounts()
    {
        var chart = ChartOfAccounts.Load(
            Path.Combine(AppContext.BaseDirectory, "Accounting", "chart-of-accounts.json")
        );

        Assert.Equal(7, chart.Accounts.Count(a => a.Category == Category.Income));
        Assert.Equal(12, chart.Accounts.Count(a => a.Category == Category.Expense));
        Assert.Equal("Jahresbeiträge", chart.Find(3400)?.Name);
        Assert.Equal("Bankgebühren", chart.Find(6940)?.Name);
        Assert.All(chart.Accounts, a => Assert.True(a.Active));
    }
}
