using backend.Accounting;

namespace backend.Tests.Accounting;

public class SettingsRulesTests
{
    private static string? Validate(
        decimal openingBalance = 1_000m,
        string? startDate = "2026-01-01",
        string? iban = "CH93 0076 2011 6238 5295 7",
        DateOnly? earliestTransaction = null,
        string? name = "SC Wiedikon",
        string? address = "Sportweg 12",
        string? postalCode = "8000",
        string? city = "Zürich"
    ) =>
        SettingsRules.Validate(
            openingBalance,
            startDate is null ? default : DateOnly.Parse(startDate),
            iban,
            name,
            address,
            postalCode,
            city,
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

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void A_missing_name_is_rejected(string? name) => Assert.Contains("Name", Validate(name: name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void A_missing_address_is_rejected(string? address) => Assert.Contains("Adresse", Validate(address: address));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void A_missing_postal_code_is_rejected(string? postalCode) =>
        Assert.Contains("PLZ", Validate(postalCode: postalCode));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void A_missing_city_is_rejected(string? city) => Assert.Contains("Ort", Validate(city: city));

    [Fact]
    public void Values_at_the_QR_bill_limits_are_accepted() =>
        Assert.Null(
            Validate(name: new string('a', 70), address: new string('a', 70), postalCode: new string('1', 16), city: new string('a', 35))
        );

    [Fact]
    public void A_name_over_70_characters_is_rejected() =>
        Assert.Contains("70", Validate(name: new string('a', 71)));

    [Fact]
    public void An_address_over_70_characters_is_rejected() =>
        Assert.Contains("70", Validate(address: new string('a', 71)));

    [Fact]
    public void A_postal_code_over_16_characters_is_rejected() =>
        Assert.Contains("16", Validate(postalCode: new string('1', 17)));

    [Fact]
    public void A_city_over_35_characters_is_rejected() =>
        Assert.Contains("35", Validate(city: new string('a', 36)));

    [Fact]
    public void A_QR_IBAN_is_rejected() =>
        Assert.Equal("Bitte eine normale IBAN statt der QR-IBAN verwenden", Validate(iban: "CH44 3199 9123 0008 8901 2"));

    [Fact]
    public void A_QR_IBAN_at_the_lower_institution_ID_bound_is_rejected() =>
        Assert.Contains("QR-IBAN", Validate(iban: "CH5730000123456789012"));

    [Fact]
    public void An_IBAN_just_outside_the_QR_range_is_accepted() =>
        Assert.Null(Validate(iban: "CH5232000123456789012"));
}
