namespace backend.Settings;

/// <summary>Sends email through an SMTP server. Real: <see cref="MailKitEmailSender"/>; in tests: <see cref="InMemoryEmailSender"/>.</summary>
public interface IEmailSender
{
    /// <summary>Sends the fixed test message; returns the SMTP error text, or null on success.</summary>
    Task<string?> SendTestAsync(SmtpConnection smtp, string receiver, CancellationToken ct);
}
