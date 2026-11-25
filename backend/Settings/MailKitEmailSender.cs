using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace backend.Settings;

public class MailKitEmailSender : IEmailSender
{
    public async Task<string?> SendTestAsync(SmtpConnection smtp, string receiver, CancellationToken ct)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtp.FromName ?? "", smtp.FromAddress));
        message.To.Add(MailboxAddress.Parse(receiver));
        message.Subject = "Test-E-Mail von clubAdmin";
        message.Body = new TextPart("plain")
        {
            Text = "Die SMTP-Einstellungen funktionieren. Diese Nachricht wurde als Test gesendet.",
        };

        var options = smtp.Security switch
        {
            SmtpSecurity.Ssl => SecureSocketOptions.SslOnConnect,
            SmtpSecurity.StartTls => SecureSocketOptions.StartTls,
            _ => SecureSocketOptions.None,
        };
        try
        {
            using var client = new SmtpClient { Timeout = 15_000 };
            await client.ConnectAsync(smtp.Host, smtp.Port, options, ct);
            if (!string.IsNullOrEmpty(smtp.Username))
            {
                await client.AuthenticateAsync(smtp.Username, smtp.Password ?? "", ct);
            }
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
            return null;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return e.Message;
        }
    }
}
