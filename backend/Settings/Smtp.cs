using System.Text.Json.Serialization;

namespace backend.Settings;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SmtpSecurity
{
    StartTls,
    Ssl,
    None,
}

/// <summary>The SMTP group as the client submits it. The Password is empty when it should stay as stored.</summary>
public record SmtpInput(
    string? Host,
    int? Port,
    SmtpSecurity? Security,
    string? Username,
    string? Password,
    string? FromAddress,
    string? FromName
);

/// <summary>The SMTP group as returned to the client: never the password, only whether it is set.</summary>
public record SmtpResponse(
    string? Host,
    int? Port,
    SmtpSecurity? Security,
    string? Username,
    bool PasswordSet,
    string? FromAddress,
    string? FromName
);

/// <summary>A complete SMTP group with the password resolved, ready to connect.</summary>
public record SmtpConnection(
    string Host,
    int Port,
    SmtpSecurity Security,
    string? Username,
    string? Password,
    string FromAddress,
    string? FromName
);
