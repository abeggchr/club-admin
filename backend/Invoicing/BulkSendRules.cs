using PersonEntity = backend.Person.Person;
using SettingsEntity = backend.Settings.Settings;

namespace backend.Invoicing;

/// <summary>An Invoice with its Recipient and the Delivery Channel the bulk send picked, or why it is skipped.</summary>
public record BulkSendItem(Invoice Invoice, PersonEntity Recipient, DeliveryChannel? Channel, string? SkipReason);

/// <summary>How many unsent Invoices a bulk send would deliver by email, by post, or cannot send.</summary>
public record BulkSendPreview(int Email, int Post, int NotSendable);

/// <summary>Picks the Delivery Channel per unsent Invoice for the bulk send: Email first, then Post, else skipped.</summary>
public static class BulkSendRules
{
    private static readonly DeliveryChannel[] Preference = [DeliveryChannel.Email, DeliveryChannel.Post];

    public static List<BulkSendItem> Plan(
        IEnumerable<(Invoice Invoice, PersonEntity Recipient)> invoices,
        SettingsEntity settings,
        IEnumerable<IInvoiceSender> senders
    )
    {
        // A channel may have several backends; at most one is configured, so Pick finds it.
        var ordered = senders
            .Where(s => Preference.Contains(s.Channel))
            .OrderBy(s => Array.IndexOf(Preference, s.Channel))
            .ToList();
        return invoices
            .Where(i => InvoiceRules.LatestStatus(i.Invoice) == InvoiceStatus.Created)
            .Select(i => Pick(i.Invoice, i.Recipient, settings, ordered))
            .ToList();
    }

    public static BulkSendPreview Preview(IReadOnlyCollection<BulkSendItem> plan) =>
        new(
            plan.Count(i => i.Channel == DeliveryChannel.Email),
            plan.Count(i => i.Channel == DeliveryChannel.Post),
            plan.Count(i => i.Channel is null)
        );

    /// <summary>Subject and text are required only when at least one Invoice goes by email; a letter needs neither.</summary>
    public static string? ValidateMessage(IReadOnlyCollection<BulkSendItem> plan, string? subject, string? text) =>
        plan.Any(i => i.Channel == DeliveryChannel.Email)
            ? InvoiceRules.ValidateSend(InvoiceStatus.Created, subject, text)
            : null;

    private static BulkSendItem Pick(
        Invoice invoice,
        PersonEntity recipient,
        SettingsEntity settings,
        List<IInvoiceSender> senders
    )
    {
        var configured = senders.Where(s => s.IsConfigured(settings)).ToList();
        if (configured.FirstOrDefault(s => s.CanDeliverTo(recipient)) is { } sender)
        {
            return new(invoice, recipient, sender.Channel, null);
        }
        var reason = configured.Count == 0
            ? "Kein Versandweg ist eingerichtet."
            : string.Join(" ", configured.Select(s => s.CannotDeliverError));
        return new(invoice, recipient, null, reason);
    }
}
