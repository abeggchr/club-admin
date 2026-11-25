using backend.Accounting;
using backend.IncomeStatement;

namespace backend.Tests.IncomeStatement;

/// <summary>
/// Ticket #113: validating the Income Statement layout against the Chart of Accounts.
/// Each invalid layout aborts startup with a German message naming the offending entry.
/// </summary>
public class IncomeStatementConfigValidationTests
{
    private static readonly ChartOfAccounts Chart = ChartOfAccounts.Parse(
        """
        { "income": [ { "number": 3400, "name": "Jahresbeiträge" },
                      { "number": 3999, "name": "Alt", "inactive": true } ],
          "expense": [ { "number": 6010, "name": "Raummiete" } ] }
        """
    );

    private static string Layout(string body) =>
        "Erfolgsrechnung:\n  Ertrag:\n    I. Beiträge:\n      " + body +
        "\n  Aufwand:\n    I. Miete:\n      Raummiete: 6010";

    [Fact]
    public void A_valid_layout_has_no_errors()
    {
        var config = IncomeStatementConfig.Parse(Layout("Jahresbeiträge: 3400"));
        Assert.Null(IncomeStatementConfig.Validate(config, Chart));
    }

    [Fact]
    public void An_inactive_account_is_still_mappable()
    {
        var config = IncomeStatementConfig.Parse(Layout("Alt: 3999"));
        Assert.Null(IncomeStatementConfig.Validate(config, Chart));
    }

    [Fact]
    public void A_range_with_numbers_missing_from_the_chart_is_allowed()
    {
        // 3401 and 3402 are not in the chart, 3400 is: a range may span numbers that do not exist.
        var config = IncomeStatementConfig.Parse(Layout("Jahresbeiträge: 3400-3402"));
        Assert.Null(IncomeStatementConfig.Validate(config, Chart));
    }

    [Fact]
    public void A_single_account_missing_from_the_chart_is_rejected_by_number()
    {
        var config = IncomeStatementConfig.Parse(Layout("Unbekannt: 3500"));
        var error = IncomeStatementConfig.Validate(config, Chart);
        Assert.NotNull(error);
        Assert.Contains("3500", error);
        Assert.Contains("Unbekannt", error);
    }

    [Fact]
    public void An_account_mapped_twice_is_rejected_by_number()
    {
        var config = IncomeStatementConfig.Parse(
            """
            Erfolgsrechnung:
              Ertrag:
                I. Beiträge:
                  Jahresbeiträge: 3400
                II. Doppel:
                  Auch so: 3400
              Aufwand:
                I. Miete:
                  Raummiete: 6010
            """
        );
        var error = IncomeStatementConfig.Validate(config, Chart);
        Assert.NotNull(error);
        Assert.Contains("3400", error);
    }

    [Fact]
    public void An_account_covered_by_two_ranges_is_rejected_by_number()
    {
        var chart = ChartOfAccounts.Parse(
            """
            { "income": [ { "number": 3400, "name": "Jahresbeiträge" },
                          { "number": 3401, "name": "Zusatz" } ],
              "expense": [ { "number": 6010, "name": "Raummiete" } ] }
            """
        );
        var config = IncomeStatementConfig.Parse(
            """
            Erfolgsrechnung:
              Ertrag:
                I. Beiträge:
                  Beiträge: 3400-3401
                II. Überlappt:
                  Weiter: 3401-3402
              Aufwand:
                I. Miete:
                  Raummiete: 6010
            """
        );
        var error = IncomeStatementConfig.Validate(config, chart);
        Assert.Contains("3401", error);
        Assert.Contains("Beiträge", error);
        Assert.Contains("Weiter", error);
    }

    [Fact]
    public void A_reversed_range_is_rejected()
    {
        var config = IncomeStatementConfig.Parse(Layout("Dreht sich: 3405-3400"));
        var error = IncomeStatementConfig.Validate(config, Chart);
        Assert.NotNull(error);
        Assert.Contains("3405", error);
    }

    [Fact]
    public void A_range_matching_no_account_is_rejected()
    {
        var config = IncomeStatementConfig.Parse(Layout("Leere Strecke: 4500-4600"));
        var error = IncomeStatementConfig.Validate(config, Chart);
        Assert.NotNull(error);
        Assert.Contains("4500", error);
        Assert.Contains("Leere Strecke", error);
    }

    [Fact]
    public void An_expense_account_under_Ertrag_is_rejected_by_number()
    {
        var config = IncomeStatementConfig.Parse(Layout("Falsch platziert: 6010"));
        var error = IncomeStatementConfig.Validate(config, Chart);
        Assert.NotNull(error);
        Assert.Contains("6010", error);
        Assert.Contains("Ertrag", error);
    }

    [Fact]
    public void An_income_account_under_Aufwand_is_rejected_by_number()
    {
        var config = IncomeStatementConfig.Parse(
            """
            Erfolgsrechnung:
              Ertrag:
                I. Beiträge:
                  Jahresbeiträge: 3400
              Aufwand:
                I. Falsch:
                  Einnahme hier: 3400
            """
        );
        var error = IncomeStatementConfig.Validate(config, Chart);
        Assert.NotNull(error);
        Assert.Contains("3400", error);
        Assert.Contains("Aufwand", error);
    }
}
