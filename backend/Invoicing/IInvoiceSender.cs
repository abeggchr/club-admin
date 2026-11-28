using System.Text.Json.Serialization;
using PersonEntity = backend.Person.Person;
using SettingsEntity = backend.Settings.Settings;

namespace backend.Invoicing;

/// <summary>Delivery Channel (German: Versandart): the way an Invoice reaches its Recipient.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeliveryChannel
{
    Email,
    Post,
}

/// <summary>The subject and text the treasurer enters when sending an Invoice.</summary>
public record InvoiceMessage(string? Subject, string? Text);

/// <summary>The body of a send request. The Delivery Channel defaults to Email; a letter ignores subject and text.</summary>
public record InvoiceSendRequest(string? Subject, string? Text, DeliveryChannel Channel = DeliveryChannel.Email)
{
    public InvoiceMessage Message => new(Subject, Text);
}

/// <summary>Outcome of one send: the Remark for the Sent status change, or a German error and nothing recorded.</summary>
public record InvoiceSendResult(string? Remark, string? Error);

/// <summary>
/// Hands an Invoice PDF to one <see cref="DeliveryBackend"/>. Whether it may be used is decided beforehand by
/// <see cref="DeliveryChannelRules"/>. Real: <see cref="SmtpInvoiceSender"/>, <see cref="EPostEmailInvoiceSender"/>,
/// <see cref="EPostInvoiceSender"/>.
/// </summary>
public interface IInvoiceSender
{
    DeliveryBackend Backend { get; }

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
