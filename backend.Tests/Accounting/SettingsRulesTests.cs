using backend.Accounting;

namespace backend.Tests.Accounting;

public class SettingsRulesTests
{
    private static string? Validate(
        decimal openingBalance = 1_000m,
        string? startDate = "2026-01-01",
        string? iban = "CH93 0076 2011 6238 5295 7",
        DateOnly? earliestTransaction = null
    ) =>
        SettingsRules.Validate(
            openingBalance,
            startDate is null ? default : DateOnly.Parse(startDate),
            iban,
            earliestTransaction
        );

    [Fact]
    public void Valid_settings_have_no_error() => Assert.Null(Validate());

    [Fact]
    public void A_zero_opening_balance_is_accepted() => Assert.Null(Validate(openingBalance: 0m));

    [Fact]
    public void A_balance_with_more_than_two_decimals_is_rejected() =>
        Assert.Contains("Dezimalstellen", Validate(openingBalance: -1.005m));

    [Fact]
    public void A_missing_start_date_is_rejected() => Assert.Contains("Startdatum", Validate(startDate: null));

    [Fact]
    public void A_missing_IBAN_is_rejected() => Assert.Contains("Ungültige IBAN", Validate(iban: null));

    [Fact]
    public void An_empty_IBAN_is_rejected() => Assert.Contains("Ungültige IBAN", Validate(iban: "  "));

    [Fact]
    public void An_IBAN_with_a_wrong_checksum_is_rejected() =>
        Assert.Contains("Ungültige IBAN", Validate(iban: "CH93 0076 2011 6238 5295 8"));

    [Fact]
    public void The_start_date_may_not_be_later_than_the_earliest_transaction() =>
        Assert.Contains(
            "frühesten Bewegung",
            Validate(startDate: "2026-03-02", earliestTransaction: new DateOnly(2026, 3, 1))
        );

    [Fact]
    public void The_start_date_may_equal_the_earliest_transaction() =>
        Assert.Null(Validate(startDate: "2026-03-01", earliestTransaction: new DateOnly(2026, 3, 1)));
}
