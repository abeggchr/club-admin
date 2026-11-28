using backend.Settings;
using PersonEntity = backend.Person.Person;
using SettingsEntity = backend.Settings.Settings;

namespace backend.Invoicing;

/// <summary>Who hands an Invoice over on its Delivery Channel; each one has its own <see cref="IInvoiceSender"/>.</summary>
public enum DeliveryBackend
{
    SmtpEmail,
    EPostEmail,
    EPostLetter,
}

/// <summary>
/// One Delivery Channel for a Recipient. Backend is set once the channel is configured, so it says who would deliver;
/// the channel can be used only when BlockedReason is null.
/// </summary>
public record DeliveryChannelOption(DeliveryChannel Channel, DeliveryBackend? Backend, string? BlockedReason);

/// <summary>
/// Pure rules for which Delivery Channels reach a Recipient with the club's Settings, who delivers each one, and
/// in German why a channel is blocked. The single place that combines configuration and Recipient checks.
/// </summary>
public static class DeliveryChannelRules
{
    /// <summary>Email, then Post: the order of preference of the bulk send and the preselection of the send dialog.</summary>
    public static IReadOnlyList<DeliveryChannelOption> Options(SettingsEntity settings, PersonEntity recipient) =>
        [Email(settings, recipient), Post(settings, recipient)];

    public static DeliveryChannelOption Option(
        SettingsEntity settings,
        PersonEntity recipient,
        DeliveryChannel channel
    ) => Options(settings, recipient).Single(o => o.Channel == channel);

    private static DeliveryChannelOption Email(SettingsEntity settings, PersonEntity recipient)
    {
        DeliveryBackend? backend = EmailBackends.Resolve(settings) switch
        {
            EmailBackend.Smtp => DeliveryBackend.SmtpEmail,
            EmailBackend.EPost => DeliveryBackend.EPostEmail,
            _ => null,
        };
        var blocked = backend is null
            ? "E-Mail ist nicht eingerichtet: Es braucht eine Absenderadresse und entweder SMTP oder ePost."
            : string.IsNullOrWhiteSpace(recipient.Email)
                ? "Die Person hat keine E-Mail-Adresse."
                : null;
        return new(DeliveryChannel.Email, backend, blocked);
    }

    private static DeliveryChannelOption Post(SettingsEntity settings, PersonEntity recipient)
    {
        DeliveryBackend? backend = string.IsNullOrEmpty(settings.EPostApiKey) ? null : DeliveryBackend.EPostLetter;
        var blocked = backend is null
            ? "ePost ist nicht eingerichtet."
            : HasSwissAddress(recipient)
                ? null
                : "Die Person hat keine vollständige Schweizer Adresse (Strasse, PLZ und Ort).";
        return new(DeliveryChannel.Post, backend, blocked);
    }

    // ePost letters are sent with shippingCountry CH; an empty country counts as Switzerland.
    private static bool HasSwissAddress(PersonEntity recipient) =>
        !string.IsNullOrWhiteSpace(recipient.Street)
        && !string.IsNullOrWhiteSpace(recipient.PostalCode)
        && !string.IsNullOrWhiteSpace(recipient.City)
        && (
            string.IsNullOrWhiteSpace(recipient.Country)
            || recipient.Country.Trim().Equals("CH", StringComparison.OrdinalIgnoreCase)
            || recipient.Country.Trim().Equals("Schweiz", StringComparison.OrdinalIgnoreCase)
        );
}
