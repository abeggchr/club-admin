using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace backend.Settings;

public class MailKitEmailSender : IEmailSender
{
    public Task<string?> SendTestAsync(SmtpConnection smtp, string receiver, CancellationToken ct) =>
        SendAsync(
            smtp,
            new EmailMessage(
                receiver,
                "Test-E-Mail von clubAdmin",
                "Die SMTP-Einstellungen funktionieren. Diese Nachricht wurde als Test gesendet."
            ),
            ct
        );

    public async Task<string?> SendAsync(SmtpConnection smtp, EmailMessage email, CancellationToken ct)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(smtp.FromName ?? "", smtp.FromAddress));
            message.To.Add(MailboxAddress.Parse(email.To));
            message.Subject = email.Subject;
            var body = new BodyBuilder { TextBody = email.Text };
            if (email.Attachment is not null)
            {
                body.Attachments.Add(
                    email.AttachmentName ?? "anhang.pdf",
                    email.Attachment,
                    ContentType.Parse("application/pdf")
                );
            }
            message.Body = body.ToMessageBody();

            var options = smtp.Security switch
            {
                SmtpSecurity.Ssl => SecureSocketOptions.SslOnConnect,
                SmtpSecurity.StartTls => SecureSocketOptions.StartTls,
                _ => SecureSocketOptions.None,
            };
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
