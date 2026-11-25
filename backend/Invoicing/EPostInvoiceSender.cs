using System.Text.Json.Nodes;
using PersonEntity = backend.Person.Person;
using SettingsEntity = backend.Settings.Settings;

namespace backend.Invoicing;

/// <summary>Delivery Channel Post: has ePost print and mail the Invoice PDF as a B-Post letter. Every call is a real, billed letter; ePost has no sandbox.</summary>
public class EPostInvoiceSender(HttpClient http) : IInvoiceSender
{
    public DeliveryChannel Channel => DeliveryChannel.Post;

    public string NotConfiguredError => "ePost ist nicht eingerichtet.";

    public string CannotDeliverError =>
        "Die Person hat keine vollständige Schweizer Adresse (Strasse, PLZ und Ort).";

    public bool IsConfigured(SettingsEntity settings) => !string.IsNullOrEmpty(settings.EPostApiKey);

    public bool CanDeliverTo(PersonEntity recipient) =>
        !string.IsNullOrWhiteSpace(recipient.Street)
        && !string.IsNullOrWhiteSpace(recipient.PostalCode)
        && !string.IsNullOrWhiteSpace(recipient.City)
        && (
            string.IsNullOrWhiteSpace(recipient.Country)
            || recipient.Country.Trim().Equals("CH", StringComparison.OrdinalIgnoreCase)
            || recipient.Country.Trim().Equals("Schweiz", StringComparison.OrdinalIgnoreCase)
        );

    public async Task<InvoiceSendResult> SendAsync(
        SettingsEntity settings,
        PersonEntity recipient,
        int number,
        InvoiceMessage message,
        byte[] pdf,
        CancellationToken ct
    )
    {
        var formattedNumber = InvoiceRules.FormatNumber(number);
        var fileName = $"rechnung-{formattedNumber}.pdf";
        // documentTypes and qrInvoicePageNumber are left out on purpose: they only matter for eBill.
        var outcome = await EPostApi.DeliverAsync(
            http,
            settings.EPostApiKey!,
            fileName,
            pdf,
            new JsonObject
            {
                ["fileName"] = fileName,
                ["deliveryChannelPreferences"] = new JsonArray("PHYSICAL"),
                ["senderEndToEndId"] = formattedNumber,
                ["recipients"] = new JsonArray(
                    new JsonObject
                    {
                        ["unhashedCredentials"] = new JsonObject
                        {
                            ["firstName"] = recipient.FirstName,
                            ["lastName"] = recipient.LastName,
                            ["street"] = recipient.Street!.Trim(),
                            ["streetNumber"] = recipient.Number?.Trim(),
                            ["zipCode"] = recipient.PostalCode!.Trim(),
                            ["city"] = recipient.City!.Trim(),
                        },
                    }
                ),
                ["physicalAdditionalInfo"] = new JsonObject
                {
                    ["postage"] = "B_POST",
                    ["duplex"] = false,
                    ["windowLocation"] = "RIGHT",
                    ["envelopeSize"] = "C5",
                    ["shippingCountry"] = "CH",
                    ["senderAddress"] = new JsonObject
                    {
                        ["name"] = settings.Name,
                        ["street"] = settings.Address,
                        ["zipCode"] = settings.PostalCode,
                        ["city"] = settings.City,
                    },
                },
            },
            ct
        );
        return outcome.DeliveryId is { } id
            ? new InvoiceSendResult($"per Post ({id})", null)
            : Failure(outcome.Failure!);
    }

    private static InvoiceSendResult Failure(string reason) =>
        new(null, $"Der Brief konnte nicht versendet werden: {reason}");
}
