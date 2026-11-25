using backend.Settings;
using PersonEntity = backend.Person.Person;
using SettingsEntity = backend.Settings.Settings;

namespace backend.Invoicing;

/// <summary>Delivery Channel Email: mails the Invoice PDF to the Recipient through the configured SMTP server.</summary>
public class SmtpInvoiceSender(IEmailSender email) : IInvoiceSender
{
    public DeliveryChannel Channel => DeliveryChannel.Email;

    // The SMTP group is all-or-nothing (SmtpRules), so a host means a complete group.
    public bool IsConfigured(SettingsEntity settings) => settings.SmtpHost is not null;

    public bool CanDeliverTo(PersonEntity recipient) => !string.IsNullOrWhiteSpace(recipient.Email);

    public async Task<InvoiceSendResult> SendAsync(
        SettingsEntity settings,
        PersonEntity recipient,
        int number,
        InvoiceMessage message,
        byte[] pdf,
        CancellationToken ct
    )
    {
        var smtp = new SmtpConnection(
            settings.SmtpHost!,
            settings.SmtpPort!.Value,
            settings.SmtpSecurity!.Value,
            settings.SmtpUsername,
            settings.SmtpPassword,
            settings.SmtpFromAddress!,
            settings.SmtpFromName
        );
        var mail = new EmailMessage(
            recipient.Email!.Trim(),
            message.Subject!,
            message.Text!,
            $"rechnung-{InvoiceRules.FormatNumber(number)}.pdf",
            pdf
        );
        return await email.SendAsync(smtp, mail, ct) is { } failure
            ? new InvoiceSendResult(null, $"Die E-Mail konnte nicht gesendet werden: {failure}")
            : new InvoiceSendResult("per Email", null);
    }
}
