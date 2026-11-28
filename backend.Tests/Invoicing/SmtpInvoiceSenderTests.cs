using backend.Invoicing;
using backend.Settings;
using PersonEntity = backend.Person.Person;
using SettingsEntity = backend.Settings.Settings;

namespace backend.Tests.Invoicing;

public class SmtpInvoiceSenderTests
{
    private static SettingsEntity Configured() =>
        new()
        {
            Name = "SC Wettingen",
            SmtpHost = "smtp.example.ch",
            SmtpPort = 587,
            SmtpSecurity = SmtpSecurity.StartTls,
            SmtpUsername = "kasse",
            SmtpPassword = "geheim",
            FromAddress = "kasse@example.ch",
        };

    private static PersonEntity Person(string? email) =>
        new()
        {
            FirstName = "Anna",
            LastName = "Muster",
            Email = email,
        };

    [Fact]
    public async Task It_emails_the_PDF_as_rechnung_number_from_the_configured_address_and_remarks_per_Email()
    {
        var emails = new InMemoryEmailSender();
        var sender = new SmtpInvoiceSender(emails);
        byte[] pdf = [1, 2, 3];

        var result = await sender.SendAsync(
            Configured(),
            Person("anna@example.ch"),
            42,
            new InvoiceMessage("Rechnung", "Guten Tag"),
            pdf,
            CancellationToken.None
        );

        Assert.Equal(new InvoiceSendResult("per Email", null), result);
        var mail = Assert.Single(emails.Invoices);
        Assert.Equal("smtp.example.ch", mail.Smtp.Host);
        Assert.Equal("kasse@example.ch", mail.Smtp.FromAddress);
        Assert.Equal("SC Wettingen", mail.Smtp.FromName);
        Assert.Equal("geheim", mail.Smtp.Password);
        Assert.Equal("anna@example.ch", mail.Email.To);
        Assert.Equal("Rechnung", mail.Email.Subject);
        Assert.Equal("Guten Tag", mail.Email.Text);
        Assert.Equal("rechnung-0042.pdf", mail.Email.AttachmentName);
        Assert.Equal(pdf, mail.Email.Attachment);
    }

    [Fact]
    public async Task An_SMTP_failure_becomes_a_German_error_without_a_Remark()
    {
        var sender = new SmtpInvoiceSender(new InMemoryEmailSender { FailWith = "Connection refused" });

        var result = await sender.SendAsync(
            Configured(),
            Person("anna@example.ch"),
            42,
            new InvoiceMessage("Rechnung", "Guten Tag"),
            [1],
            CancellationToken.None
        );

        Assert.Equal(
            new InvoiceSendResult(null, "Die E-Mail konnte nicht gesendet werden: Connection refused"),
            result
        );
    }
}
