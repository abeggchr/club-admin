using PersonEntity = backend.Person.Person;
using SettingsEntity = backend.Settings.Settings;

namespace backend.Invoicing;

/// <summary>
/// The single entry point for sending an Invoice: checks the status, asks <see cref="DeliveryChannelRules"/> who
/// delivers the chosen Delivery Channel, loads the PDF, hands it to that backend's sender and appends the Sent
/// status change when the sender succeeded.
/// </summary>
public class InvoiceDispatcher(IEnumerable<IInvoiceSender> senders, IInvoiceStorage storage)
{
    /// <summary>Returns a German error, or null after adding the Sent status change to <paramref name="invoice"/> (the caller saves).</summary>
    public async Task<string?> SendAsync(
        Invoice invoice,
        PersonEntity recipient,
        SettingsEntity settings,
        DeliveryChannel channel,
        InvoiceMessage message,
        DateTime now,
        CancellationToken ct
    )
    {
        var invalid =
            InvoiceStatusRules.ValidateSend(invoice)
            ?? (channel == DeliveryChannel.Email ? InvoiceRules.ValidateMessage(message.Subject, message.Text) : null);
        if (invalid is not null)
        {
            return invalid;
        }
        var option = DeliveryChannelRules.Option(settings, recipient, channel);
        if (option.BlockedReason is { } blocked)
        {
            return blocked;
        }
        var pdf = await storage.GetAsync(invoice.Id, ct);
        if (pdf is null)
        {
            return "Das PDF der Rechnung wurde nicht gefunden.";
        }

        var sender = senders.Single(s => s.Backend == option.Backend);
        var result = await sender.SendAsync(settings, recipient, invoice.Id, message, pdf, ct);
        if (result.Error is not null)
        {
            return result.Error;
        }
        return InvoiceStatusRules.Send(invoice, result.Remark, now);
    }
}
