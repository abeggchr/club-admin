using backend.Accounting;

namespace backend.Tests.Accounting;

public class IbanTests
{
    [Fact]
    public void A_valid_ch_IBAN_has_no_error() => Assert.Null(Iban.Validate("CH93 0076 2011 6238 5295 7"));

    [Fact]
    public void A_valid_li_IBAN_has_no_error() => Assert.Null(Iban.Validate("LI78 0881 0136 0023 2001 8"));

    [Fact]
    public void A_valid_IBAN_without_spaces_has_no_error() => Assert.Null(Iban.Validate("CH4431999123456789012"));

    [Fact]
    public void A_lower_case_IBAN_is_accepted_after_normalisation() => Assert.Null(Iban.Validate("ch44 3199 9123 4567 8901 2"));

    [Fact]
    public void An_IBAN_with_extra_spaces_is_accepted_after_normalisation() =>
        Assert.Null(Iban.Validate("  CH93 0076 2011 6238 5295  7 "));

    [Fact]
    public void A_wrong_checksum_is_rejected() => Assert.Contains("Ungültige IBAN", Iban.Validate("CH93 0076 2011 6238 5295 8"));

    [Fact]
    public void An_foreign_IBAN_is_rejected() => Assert.Contains("Ungültige IBAN", Iban.Validate("DE89 3704 0044 0532 0130 00"));

    [Fact]
    public void An_IBAN_that_is_too_short_is_rejected() => Assert.Contains("Ungültige IBAN", Iban.Validate("CH93 0076 2011 6238 5295"));

    [Fact]
    public void An_IBAN_that_is_too_long_is_rejected() => Assert.Contains("Ungültige IBAN", Iban.Validate("CH93 0076 2011 6238 5295 71"));

    [Fact]
    public void An_IBAN_with_letters_after_the_country_code_is_rejected() =>
        Assert.Contains("Ungültige IBAN", Iban.Validate("CH93 0076 2011 6238 5295 X"));

    [Fact]
    public void An_empty_IBAN_is_rejected() => Assert.Contains("Ungültige IBAN", Iban.Validate(""));

    [Fact]
    public void A_null_IBAN_is_rejected() => Assert.Contains("Ungültige IBAN", Iban.Validate(null));

    [Fact]
    public void Normalisation_removes_spaces_and_upper_cases() =>
        Assert.Equal("CH9300762011623852957", Iban.Normalize("ch93 0076 2011 6238 5295 7"));

    [Fact]
    public void Normalisation_of_null_is_null() => Assert.Null(Iban.Normalize(null));
}
