using System.Text.Json.Serialization;

namespace backend.Settings;

/// <summary>Who delivers the club's emails: its own SMTP server, or ePost (ADR 0008).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EmailBackend
{
    Smtp,
    EPost,
}

public static class EmailBackends
{
    /// <summary>
    /// SMTP when the SMTP group is set, else ePost when an API key is stored, else null (no email). Needs the club's
    /// FromAddress either way. A failing SMTP server never falls back to ePost: that would bill the club unasked.
    /// </summary>
    public static EmailBackend? Resolve(Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.FromAddress))
        {
            return null;
        }
        if (settings.SmtpHost is not null)
        {
            return EmailBackend.Smtp;
        }
        return string.IsNullOrEmpty(settings.EPostApiKey) ? null : EmailBackend.EPost;
    }
}
