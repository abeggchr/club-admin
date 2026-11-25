namespace backend.Settings;

/// <summary>Records emails instead of sending them, fails with <see cref="FailWith"/>, or throws <see cref="ThrowWith"/> like a transport that times out.</summary>
public class InMemoryEmailSender : IEmailSender
{
    public record SentEmail(SmtpConnection Smtp, string Receiver);

    public record SentMessage(SmtpConnection Smtp, EmailMessage Email);

    public List<SentEmail> Sent { get; } = [];
    public List<SentMessage> Invoices { get; } = [];
    public string? FailWith { get; set; }
    public Exception? ThrowWith { get; set; }

    public Task<string?> SendTestAsync(SmtpConnection smtp, string receiver, CancellationToken ct)
    {
        if (FailWith is null)
        {
            Sent.Add(new SentEmail(smtp, receiver));
        }
        return Task.FromResult(FailWith);
    }

    public Task<string?> SendAsync(SmtpConnection smtp, EmailMessage message, CancellationToken ct)
    {
        if (ThrowWith is not null)
        {
            throw ThrowWith;
        }
        if (FailWith is null)
        {
            Invoices.Add(new SentMessage(smtp, message));
        }
        return Task.FromResult(FailWith);
    }
}
