using backend.Settings;
using SettingsEntity = backend.Settings.Settings;

namespace backend.Tests.Settings;

public class EmailBackendsTests
{
    private static SettingsEntity Club(string? from = "kasse@example.ch", string? smtpHost = null, string? ePostKey = null) =>
        new()
        {
            FromAddress = from,
            SmtpHost = smtpHost,
            EPostApiKey = ePostKey,
        };

    [Fact]
    public void An_SMTP_group_means_SMTP() =>
        Assert.Equal(EmailBackend.Smtp, EmailBackends.Resolve(Club(smtpHost: "smtp.example.ch")));

    [Fact]
    public void An_SMTP_group_wins_over_an_ePost_key() =>
        Assert.Equal(EmailBackend.Smtp, EmailBackends.Resolve(Club(smtpHost: "smtp.example.ch", ePostKey: "key")));

    [Fact]
    public void Without_an_SMTP_group_an_ePost_key_means_ePost() =>
        Assert.Equal(EmailBackend.EPost, EmailBackends.Resolve(Club(ePostKey: "key")));

    [Fact]
    public void Without_SMTP_and_ePost_there_is_no_email() =>
        Assert.Null(EmailBackends.Resolve(Club()));

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Without_the_club_address_there_is_no_email_whatever_the_backend(string? from)
    {
        Assert.Null(EmailBackends.Resolve(Club(from, smtpHost: "smtp.example.ch")));
        Assert.Null(EmailBackends.Resolve(Club(from, ePostKey: "key")));
    }
}
