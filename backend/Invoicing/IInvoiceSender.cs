using PersonEntity = backend.Person.Person;
using SettingsEntity = backend.Settings.Settings;

namespace backend.Invoicing;

/// <summary>Delivery Channel (German: Versandart): the way an Invoice reaches its Recipient.</summary>
public enum DeliveryChannel
{
    Email,
}

/// <summary>The subject and text the treasurer enters when sending an Invoice.</summary>
public record InvoiceMessage(string? Subject, string? Text);

/// <summary>Outcome of one send: the Remark for the Sent status change, or a German error and nothing recorded.</summary>
public record InvoiceSendResult(string? Remark, string? Error);

/// <summary>Strategy for one Delivery Channel. Real: <see cref="SmtpInvoiceSender"/>; development and tests register fakes.</summary>
public interface IInvoiceSender
{
    DeliveryChannel Channel { get; }

    bool IsConfigured(SettingsEntity settings);

    bool CanDeliverTo(PersonEntity recipient);

    /// <summary>Sends the Invoice PDF to the Recipient and returns the Remark text.</summary>
    Task<InvoiceSendResult> SendAsync(
        SettingsEntity settings,
        PersonEntity recipient,
        int number,
        InvoiceMessage message,
        byte[] pdf,
        CancellationToken ct
    );
}
