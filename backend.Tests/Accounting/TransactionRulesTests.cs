using backend.Accounting;

namespace backend.Tests.Accounting;

public class TransactionRulesTests
{
    private static readonly Settings Opening = new() { OpeningBalance = 0m, StartDate = new DateOnly(2026, 1, 1) };

    private static readonly ChartOfAccounts Chart = ChartOfAccounts.Parse(
        """
        { "income": [ { "number": 3400, "name": "Jahresbeiträge" }, { "number": 3999, "name": "Alt", "inactive": true } ],
          "expense": [ { "number": 6010, "name": "Miete" } ] }
        """
    );

    private static EntryDraft Line(
        decimal amount,
        int account = 6010,
        string? text = "Miete März",
        int? existingAccount = null
    ) => new(text, account, amount, existingAccount);

    private static string? Validate(
        decimal amount = -120m,
        EntryDraft[]? entries = null,
        string date = "2026-03-01",
        bool withOpening = true
    ) =>
        TransactionRules.Validate(
            withOpening ? Opening : null,
            Chart,
            DateOnly.Parse(date),
            amount,
            entries ?? [Line(-120m)]
        );

    [Fact]
    public void A_balanced_transaction_has_no_error() => Assert.Null(Validate());

    [Fact]
    public void Without_an_opening_balance_transactions_are_rejected() =>
        Assert.Contains("Anfangssaldo", Validate(withOpening: false));

    [Fact]
    public void A_transaction_before_the_opening_date_is_rejected() =>
        Assert.Contains("vor dem Anfangssaldo", Validate(date: "2025-12-31"));

    [Fact]
    public void A_transaction_on_the_opening_date_is_accepted() => Assert.Null(Validate(date: "2026-01-01"));

    [Fact]
    public void A_zero_amount_is_rejected() =>
        Assert.Contains("0", Validate(amount: 0m, entries: [Line(1m), Line(-1m)]));

    [Fact]
    public void More_than_two_decimals_are_rejected() =>
        Assert.Contains("Dezimalstellen", Validate(amount: -1.005m, entries: [Line(-1.005m)]));

    [Fact]
    public void A_transaction_without_entries_is_accepted() => Assert.Null(Validate(entries: []));

    [Fact]
    public void An_entry_with_empty_text_is_rejected() =>
        Assert.Contains("Text", Validate(entries: [Line(-120m, text: " ")]));

    [Fact]
    public void An_entry_on_an_unknown_account_is_rejected() =>
        Assert.Contains("Konto 9999", Validate(entries: [Line(-120m, account: 9999)]));

    [Fact]
    public void An_entry_on_an_inactive_account_is_rejected() =>
        Assert.Contains("inaktiv", Validate(amount: 50m, entries: [Line(50m, account: 3999)]));

    [Fact]
    public void An_existing_entry_may_keep_its_now_inactive_account() =>
        Assert.Null(Validate(amount: 50m, entries: [Line(50m, account: 3999, existingAccount: 3999)]));

    [Fact]
    public void An_existing_entry_may_not_switch_to_a_different_inactive_account() =>
        Assert.Contains(
            "inaktiv",
            Validate(amount: 50m, entries: [Line(50m, account: 3999, existingAccount: 6010)])
        );

    [Fact]
    public void An_entry_with_more_than_two_decimals_is_rejected() =>
        Assert.Contains("Dezimalstellen", Validate(amount: -1m, entries: [Line(-1.005m), Line(0.005m)]));

    [Fact]
    public void An_entry_with_a_zero_amount_is_rejected() =>
        Assert.Contains("0", Validate(entries: [Line(-120m), Line(0m)]));

    [Fact]
    public void Entries_must_sum_to_the_transaction_amount() =>
        Assert.Contains("Summe", Validate(amount: -120m, entries: [Line(-100m)]));

    [Fact]
    public void Entries_with_mixed_signs_may_balance() =>
        Assert.Null(Validate(amount: -100m, entries: [Line(-120m), Line(20m, account: 3400)]));

    [Fact]
    public void Mixed_signs_that_do_not_cancel_out_are_unbalanced() =>
        Assert.Contains("Summe", Validate(amount: -100m, entries: [Line(-120m), Line(-20m)]));

    [Fact]
    public void The_opening_date_may_move_up_to_the_earliest_transaction() =>
        Assert.Null(TransactionRules.ValidateOpeningStartDate(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 1)));

    [Fact]
    public void The_opening_date_cannot_move_later_than_the_earliest_transaction() =>
        Assert.Contains(
            "frühesten Bewegung",
            TransactionRules.ValidateOpeningStartDate(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 1))
        );

    [Fact]
    public void The_opening_date_is_free_when_there_are_no_transactions() =>
        Assert.Null(TransactionRules.ValidateOpeningStartDate(new DateOnly(2030, 1, 1), null));
}
