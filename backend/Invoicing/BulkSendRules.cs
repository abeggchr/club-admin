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
    public static List<BulkSendItem> Plan(
        IEnumerable<(Invoice Invoice, PersonEntity Recipient)> invoices,
        SettingsEntity settings
    ) =>
        invoices
            .Where(i => InvoiceStatusRules.Current(i.Invoice) == InvoiceStatus.Created)
            .Select(i => Pick(i.Invoice, i.Recipient, settings))
            .ToList();

    public static BulkSendPreview Preview(IReadOnlyCollection<BulkSendItem> plan) =>
        new(
            plan.Count(i => i.Channel == DeliveryChannel.Email),
            plan.Count(i => i.Channel == DeliveryChannel.Post),
            plan.Count(i => i.Channel is null)
        );

    /// <summary>Subject and text are required only when at least one Invoice goes by email; a letter needs neither.</summary>
    public static string? ValidateMessage(IReadOnlyCollection<BulkSendItem> plan, string? subject, string? text) =>
        plan.Any(i => i.Channel == DeliveryChannel.Email)
            ? InvoiceRules.ValidateMessage(subject, text)
            : null;

    private static BulkSendItem Pick(Invoice invoice, PersonEntity recipient, SettingsEntity settings)
    {
        var options = DeliveryChannelRules.Options(settings, recipient);
        if (options.FirstOrDefault(o => o.BlockedReason is null) is { } usable)
        {
            return new(invoice, recipient, usable.Channel, null);
        }
        // A channel nobody set up is named only when none is set up, so the reason points at what this Recipient lacks.
        var configured = options.Where(o => o.Backend is not null).ToList();
        var reason = configured.Count == 0
            ? "Kein Versandweg ist eingerichtet."
            : string.Join(" ", configured.Select(o => o.BlockedReason));
        return new(invoice, recipient, null, reason);
    }
}
