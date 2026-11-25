using backend.Settings;

namespace backend.Tests.Settings;

public class SmtpRulesTests
{
    private static SmtpInput Smtp(
        string? host = "smtp.example.ch",
        int? port = 587,
        SmtpSecurity? security = SmtpSecurity.StartTls,
        string? username = null,
        string? password = null,
        string? fromAddress = "kasse@example.ch"
    ) => new(host, port, security, username, password, fromAddress);

    [Fact]
    public void No_SMTP_group_and_a_group_with_only_blank_fields_are_empty()
    {
        Assert.True(SmtpRules.IsEmpty(null));
        Assert.True(SmtpRules.IsEmpty(Smtp(host: " ", port: null, security: null, fromAddress: null)));
    }

    [Fact]
    public void A_group_with_any_field_filled_is_not_empty() =>
        Assert.False(SmtpRules.IsEmpty(Smtp(host: null, port: null, security: null, fromAddress: null, username: "u")));

    [Fact]
    public void An_empty_group_is_valid() =>
        Assert.Null(SmtpRules.Validate(Smtp(host: null, port: null, security: null, fromAddress: null)));

    [Fact]
    public void A_complete_group_without_login_is_valid() => Assert.Null(SmtpRules.Validate(Smtp()));

    [Fact]
    public void The_host_is_required_once_any_field_is_filled() =>
        Assert.Equal("Der SMTP-Server ist erforderlich.", SmtpRules.Validate(Smtp(host: " ")));

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(65536)]
    public void The_port_must_be_between_1_and_65535(int? port) =>
        Assert.Equal("Der Port muss zwischen 1 und 65535 liegen.", SmtpRules.Validate(Smtp(port: port)));

    [Fact]
    public void The_security_is_required() =>
        Assert.Equal("Die Verschlüsselung ist erforderlich.", SmtpRules.Validate(Smtp(security: null)));

    [Fact]
    public void The_from_address_is_required() =>
        Assert.Equal("Die Absenderadresse ist erforderlich.", SmtpRules.Validate(Smtp(fromAddress: " ")));

    [Fact]
    public void The_from_address_must_be_an_email_address() =>
        Assert.Equal(
            "Die Absenderadresse ist keine gültige E-Mail-Adresse.",
            SmtpRules.Validate(Smtp(fromAddress: "kasse"))
        );

    [Theory]
    [InlineData("anna@example.ch", true)]
    [InlineData(" anna@example.ch ", true)]
    [InlineData("anna", false)]
    [InlineData("Anna <anna@example.ch>", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void An_email_address_is_a_bare_address(string? value, bool expected) =>
        Assert.Equal(expected, SmtpRules.IsEmailAddress(value));
}
