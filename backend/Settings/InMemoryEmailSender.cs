namespace backend.Settings;

/// <summary>Records test emails instead of sending them, or fails with <see cref="FailWith"/>.</summary>
public class InMemoryEmailSender : IEmailSender
{
    public record SentEmail(SmtpConnection Smtp, string Receiver);

    public List<SentEmail> Sent { get; } = [];
    public string? FailWith { get; set; }

    public Task<string?> SendTestAsync(SmtpConnection smtp, string receiver, CancellationToken ct)
    {
        if (FailWith is null)
        {
            Sent.Add(new SentEmail(smtp, receiver));
        }
        return Task.FromResult(FailWith);
    }
}
