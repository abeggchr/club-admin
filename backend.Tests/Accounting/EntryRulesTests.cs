using backend.Accounting;

namespace backend.Tests.Accounting;

public class EntryRulesTests
{
    private static readonly OpeningBalance Opening = new() { Amount = 0m, StartDate = new DateOnly(2026, 1, 1) };

    private static readonly ChartOfAccounts Chart = ChartOfAccounts.Parse(
        """
        { "income": [ { "number": 3400, "name": "Jahresbeiträge" }, { "number": 3999, "name": "Alt", "inactive": true } ],
          "expense": [ { "number": 6010, "name": "Miete" } ] }
        """
    );

    private static string? Validate(
        string date = "2026-03-01",
        string text = "Miete März",
        int account = 6010,
        decimal amount = -120m,
        bool withOpening = true
    ) => EntryRules.Validate(withOpening ? Opening : null, Chart, DateOnly.Parse(date), text, account, amount);

    [Fact]
    public void A_valid_entry_has_no_error() => Assert.Null(Validate());

    [Fact]
    public void Without_an_opening_balance_entries_are_rejected() =>
        Assert.Contains("Anfangssaldo", Validate(withOpening: false));

    [Fact]
    public void An_entry_before_the_opening_date_is_rejected() =>
        Assert.Contains("vor dem Anfangssaldo", Validate(date: "2025-12-31"));

    [Fact]
    public void An_entry_on_the_opening_date_is_accepted() => Assert.Null(Validate(date: "2026-01-01"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_text_is_rejected(string text) => Assert.Contains("Text", Validate(text: text));

    [Fact]
    public void An_unknown_account_is_rejected() => Assert.Contains("Konto", Validate(account: 9999));

    [Fact]
    public void An_inactive_account_is_rejected() => Assert.Contains("inaktiv", Validate(account: 3999));

    [Fact]
    public void An_income_account_accepts_a_positive_amount() => Assert.Null(Validate(account: 3400, amount: 50m));

    [Fact]
    public void More_than_two_decimals_are_rejected() => Assert.Contains("Dezimalstellen", Validate(amount: 1.005m));

    [Fact]
    public void A_zero_amount_is_rejected() => Assert.NotNull(Validate(amount: 0m));

    [Fact]
    public void The_opening_date_may_move_up_to_the_earliest_entry() =>
        Assert.Null(EntryRules.ValidateOpeningStartDate(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 1)));

    [Fact]
    public void The_opening_date_cannot_move_later_than_the_earliest_entry() =>
        Assert.Contains(
            "frühesten Buchung",
            EntryRules.ValidateOpeningStartDate(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 1))
        );

    [Fact]
    public void The_opening_date_is_free_when_there_are_no_entries() =>
        Assert.Null(EntryRules.ValidateOpeningStartDate(new DateOnly(2030, 1, 1), null));
}
