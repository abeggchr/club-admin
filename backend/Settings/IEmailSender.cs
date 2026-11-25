namespace backend.Settings;

/// <summary>One plain-text email with an optional PDF attachment.</summary>
public record EmailMessage(string To, string Subject, string Text, string? AttachmentName = null, byte[]? Attachment = null);

/// <summary>Sends email through an SMTP server. Real: <see cref="MailKitEmailSender"/>; in tests: <see cref="InMemoryEmailSender"/>.</summary>
public interface IEmailSender
{
    /// <summary>Sends the fixed test message; returns the SMTP error text, or null on success.</summary>
    Task<string?> SendTestAsync(SmtpConnection smtp, string receiver, CancellationToken ct);

    /// <summary>Sends one message; returns the SMTP error text, or null on success.</summary>
    Task<string?> SendAsync(SmtpConnection smtp, EmailMessage message, CancellationToken ct);
}
