using backend.Invoicing;
using backend.Settings;
using PersonEntity = backend.Person.Person;
using SettingsEntity = backend.Settings.Settings;

namespace backend.Tests.Invoicing;

public class DeliveryChannelRulesTests
{
    private const string EmailNotConfigured =
        "E-Mail ist nicht eingerichtet: Es braucht eine Absenderadresse und entweder SMTP oder ePost.";
    private const string NoEmail = "Die Person hat keine E-Mail-Adresse.";
    private const string EPostNotConfigured = "ePost ist nicht eingerichtet.";
    private const string NoSwissAddress = "Die Person hat keine vollständige Schweizer Adresse (Strasse, PLZ und Ort).";

    private static SettingsEntity Club(string shape) =>
        shape switch
        {
            "none" => new(),
            "smtp" => new() { FromAddress = "kasse@example.ch", SmtpHost = "smtp.example.ch", SmtpPort = 587, SmtpSecurity = SmtpSecurity.StartTls },
            "epost" => new() { FromAddress = "kasse@example.ch", EPostApiKey = "key-123" },
            "both" => new() { FromAddress = "kasse@example.ch", SmtpHost = "smtp.example.ch", SmtpPort = 587, SmtpSecurity = SmtpSecurity.StartTls, EPostApiKey = "key-123" },
            "no from address" => new() { SmtpHost = "smtp.example.ch", SmtpPort = 587, SmtpSecurity = SmtpSecurity.StartTls, EPostApiKey = "key-123" },
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };

    private static PersonEntity Recipient(
        string? email = "anna@example.ch",
        string? street = "Bahnhofstrasse",
        string? postalCode = "5400",
        string? city = "Baden",
        string? country = null
    ) =>
        new()
        {
            FirstName = "Anna",
            LastName = "Muster",
            Email = email,
            Street = street,
            Number = "7",
            PostalCode = postalCode,
            City = city,
            Country = country,
        };

    private static PersonEntity Recipient(string shape) =>
        shape switch
        {
            "email and Swiss address" => Recipient(),
            "Swiss address only" => Recipient(email: null),
            "email and foreign address" => Recipient(country: "Deutschland"),
            "email and incomplete address" => Recipient(street: null),
            "nothing" => Recipient(email: null, street: null),
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };

    // Each expectation is the backend that delivers the channel, or the German reason it is blocked.
    [Theory]
    [InlineData("none", "email and Swiss address", EmailNotConfigured, EPostNotConfigured)]
    [InlineData("none", "nothing", EmailNotConfigured, EPostNotConfigured)]
    [InlineData("smtp", "email and Swiss address", "SmtpEmail", EPostNotConfigured)]
    [InlineData("smtp", "Swiss address only", NoEmail, EPostNotConfigured)]
    [InlineData("epost", "email and Swiss address", "EPostEmail", "EPostLetter")]
    [InlineData("epost", "Swiss address only", NoEmail, "EPostLetter")]
    [InlineData("epost", "email and foreign address", "EPostEmail", NoSwissAddress)]
    [InlineData("epost", "email and incomplete address", "EPostEmail", NoSwissAddress)]
    [InlineData("epost", "nothing", NoEmail, NoSwissAddress)]
    [InlineData("both", "email and Swiss address", "SmtpEmail", "EPostLetter")]
    [InlineData("both", "Swiss address only", NoEmail, "EPostLetter")]
    [InlineData("both", "email and foreign address", "SmtpEmail", NoSwissAddress)]
    [InlineData("no from address", "email and Swiss address", EmailNotConfigured, "EPostLetter")]
    [InlineData("no from address", "email and incomplete address", EmailNotConfigured, NoSwissAddress)]
    public void Each_Settings_and_Recipient_shape_resolves_to_a_backend_or_a_blocked_reason(
        string settings,
        string recipient,
        string email,
        string post
    )
    {
        var options = DeliveryChannelRules.Options(Club(settings), Recipient(recipient));

        Assert.Equal([DeliveryChannel.Email, DeliveryChannel.Post], options.Select(o => o.Channel));
        Assert.Equal(email, Describe(options[0]));
        Assert.Equal(post, Describe(options[1]));
    }

    private static string? Describe(DeliveryChannelOption option) =>
        option.BlockedReason ?? option.Backend?.ToString();

    [Fact]
    public void With_SMTP_and_an_ePost_key_email_goes_through_SMTP() =>
        Assert.Equal(
            DeliveryBackend.SmtpEmail,
            DeliveryChannelRules.Option(Club("both"), Recipient(), DeliveryChannel.Email).Backend
        );

    [Fact]
    public void Without_SMTP_an_ePost_key_delivers_both_email_and_letters()
    {
        var options = DeliveryChannelRules.Options(Club("epost"), Recipient());

        Assert.Equal([DeliveryBackend.EPostEmail, DeliveryBackend.EPostLetter], options.Select(o => o.Backend));
        Assert.All(options, o => Assert.Null(o.BlockedReason));
    }

    [Fact]
    public void A_channel_blocked_by_the_Recipient_still_names_the_backend_it_is_configured_with()
    {
        var option = DeliveryChannelRules.Option(Club("smtp"), Recipient(email: null), DeliveryChannel.Email);

        Assert.Equal(DeliveryBackend.SmtpEmail, option.Backend);
        Assert.Equal(NoEmail, option.BlockedReason);
    }

    [Fact]
    public void A_channel_that_is_not_configured_has_no_backend() =>
        Assert.All(DeliveryChannelRules.Options(Club("none"), Recipient()), o => Assert.Null(o.Backend));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_email_address_blocks_email(string? email) =>
        Assert.Equal(
            NoEmail,
            DeliveryChannelRules.Option(Club("smtp"), Recipient(email: email), DeliveryChannel.Email).BlockedReason
        );

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("CH")]
    [InlineData("ch")]
    [InlineData(" Schweiz ")]
    [InlineData("SCHWEIZ")]
    public void A_letter_goes_to_Switzerland_in_any_spelling_or_without_a_country(string? country) =>
        Assert.Null(
            DeliveryChannelRules.Option(Club("epost"), Recipient(country: country), DeliveryChannel.Post).BlockedReason
        );

    [Theory]
    [InlineData("DE")]
    [InlineData("Deutschland")]
    public void A_letter_never_goes_abroad(string country) =>
        Assert.Equal(
            NoSwissAddress,
            DeliveryChannelRules.Option(Club("epost"), Recipient(country: country), DeliveryChannel.Post).BlockedReason
        );

    [Theory]
    [InlineData(null, "5400", "Baden")]
    [InlineData("Bahnhofstrasse", " ", "Baden")]
    [InlineData("Bahnhofstrasse", "5400", null)]
    public void A_letter_needs_Street_PostalCode_and_City(string? street, string? postalCode, string? city) =>
        Assert.Equal(
            NoSwissAddress,
            DeliveryChannelRules
                .Option(Club("epost"), Recipient(street: street, postalCode: postalCode, city: city), DeliveryChannel.Post)
                .BlockedReason
        );
}
