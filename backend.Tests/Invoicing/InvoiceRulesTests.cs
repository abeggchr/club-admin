using backend.Accounting;
using backend.Invoicing;

namespace backend.Tests.Invoicing;

public class InvoiceRulesTests
{
    private const string ChartJson = """
        {
          "income": [ { "number": 3400, "name": "Jahresbeiträge" } ],
          "expense": [
            { "number": 6010, "name": "Miete" },
            { "number": 6999, "name": "Altes Konto", "inactive": true }
          ]
        }
        """;

    private static ChartOfAccounts Chart() => ChartOfAccounts.Parse(ChartJson);

    private static InvoiceLineDraft Line(
        string? text = "Jahresbeitrag",
        decimal amount = 100m,
        int accountNumber = 3400
    ) => new(text, amount, accountNumber);

    private static string? Validate(
        int? personId = 1,
        int? templateId = 1,
        InvoiceLineDraft[]? lines = null
    ) => InvoiceRules.Validate(personId, templateId, lines ?? [Line()], Chart());

    [Fact]
    public void A_complete_invoice_has_no_error() => Assert.Null(Validate());

    [Fact]
    public void A_missing_Person_is_refused() => Assert.NotNull(Validate(personId: null));

    [Fact]
    public void A_missing_Textvorlage_is_refused() => Assert.NotNull(Validate(templateId: null));

    [Fact]
    public void An_invoice_without_lines_is_refused() => Assert.NotNull(Validate(lines: []));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_line_without_text_is_refused(string? text) =>
        Assert.NotNull(Validate(lines: [Line(), Line(text)]));

    [Fact]
    public void A_line_with_an_amount_of_zero_is_refused() =>
        Assert.NotNull(Validate(lines: [Line(), Line(amount: 0)]));

    [Fact]
    public void A_negative_line_is_accepted_while_the_total_stays_positive() =>
        Assert.Null(Validate(lines: [Line(amount: 100m), Line(amount: -30m)]));

    [Theory]
    [InlineData(-5)]
    [InlineData(0)]
    public void A_total_of_zero_or_less_is_refused(decimal negative) =>
        Assert.NotNull(Validate(lines: [Line(amount: 50m), Line(amount: -50m + negative)]));

    [Fact]
    public void A_line_with_more_than_two_decimals_is_refused() =>
        Assert.NotNull(Validate(lines: [Line(amount: 10.005m)]));

    [Fact]
    public void A_line_with_two_decimals_is_accepted() => Assert.Null(Validate(lines: [Line(amount: 10.05m)]));

    [Fact]
    public void A_line_with_an_unknown_account_is_refused() =>
        Assert.NotNull(Validate(lines: [Line(accountNumber: 9999)]));

    [Fact]
    public void A_line_with_an_inactive_account_is_refused() =>
        Assert.NotNull(Validate(lines: [Line(accountNumber: 6999)]));

    [Fact]
    public void A_line_with_an_expense_account_is_accepted() =>
        Assert.Null(Validate(lines: [Line(accountNumber: 6010)]));

    [Theory]
    [InlineData(1, "0001")]
    [InlineData(42, "0042")]
    [InlineData(12345, "12345")]
    public void The_Rechnungsnummer_has_at_least_four_digits(int id, string number) =>
        Assert.Equal(number, InvoiceRules.FormatNumber(id));

    [Fact]
    public void The_total_is_the_sum_of_the_line_amounts() =>
        Assert.Equal(150.50m, InvoiceRules.Total([Line(amount: 100m), Line(amount: 50.50m)]));
}
