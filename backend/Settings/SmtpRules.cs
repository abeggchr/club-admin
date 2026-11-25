using System.Net.Mail;

namespace backend.Settings;

/// <summary>Pure validation for the optional SMTP group of the Settings: all empty, or every required field filled. Returns a German error message, or null when valid.</summary>
public static class SmtpRules
{
    public static bool IsEmpty(SmtpInput? smtp) =>
        smtp is null
        || (
            string.IsNullOrWhiteSpace(smtp.Host)
            && smtp.Port is null
            && smtp.Security is null
            && string.IsNullOrWhiteSpace(smtp.Username)
            && string.IsNullOrWhiteSpace(smtp.Password)
            && string.IsNullOrWhiteSpace(smtp.FromAddress)
        );

    /// <summary>Username and Password are optional (some relays need no login); the rest is required once any field is filled.</summary>
    public static string? Validate(SmtpInput? smtp)
    {
        if (IsEmpty(smtp))
        {
            return null;
        }
        if (string.IsNullOrWhiteSpace(smtp!.Host))
        {
            return "Der SMTP-Server ist erforderlich.";
        }
        if (smtp.Port is null or < 1 or > 65535)
        {
            return "Der Port muss zwischen 1 und 65535 liegen.";
        }
        if (smtp.Security is null)
        {
            return "Die Verschlüsselung ist erforderlich.";
        }
        if (string.IsNullOrWhiteSpace(smtp.FromAddress))
        {
            return "Die Absenderadresse ist erforderlich.";
        }
        return IsEmailAddress(smtp.FromAddress)
            ? null
            : "Die Absenderadresse ist keine gültige E-Mail-Adresse.";
    }

    public static bool IsEmailAddress(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && MailAddress.TryCreate(value.Trim(), out var address)
        && address.Address == value.Trim();
}
