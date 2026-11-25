using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PersonEntity = backend.Person.Person;
using SettingsEntity = backend.Settings.Settings;

namespace backend.Invoicing;

/// <summary>Delivery Channel Post: has ePost print and mail the Invoice PDF as a B-Post letter. Every call is a real, billed letter; ePost has no sandbox.</summary>
public class EPostInvoiceSender(HttpClient http) : IInvoiceSender
{
    private const string DeliveriesUrl = "https://api.epost.ch/epost/v2/deliveries";

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
        var pdfPart = new ByteArrayContent(pdf);
        pdfPart.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        // documentTypes and qrInvoicePageNumber are left out on purpose: they only matter for eBill.
        var metadata = new JsonObject
        {
            ["fileMetadata"] = new JsonArray(
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
                }
            ),
        };
        var metadataPart = new StringContent(metadata.ToJsonString(), Encoding.UTF8, "application/json");
        using var form = new MultipartFormDataContent
        {
            { pdfPart, "files", fileName },
            { metadataPart, "metadata" },
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, DeliveriesUrl) { Content = form };
        request.Headers.Add("X-API-KEY", settings.EPostApiKey);

        try
        {
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Created && await ReadAsync(response, "id", ct) is { } id)
            {
                return new InvoiceSendResult($"per Post ({id})", null);
            }
            return Failure(await ReadAsync(response, "message", ct) ?? $"HTTP {(int)response.StatusCode}");
        }
        catch (HttpRequestException e)
        {
            return Failure(e.Message);
        }
    }

    private static InvoiceSendResult Failure(string reason) =>
        new(null, $"Der Brief konnte nicht versendet werden: {reason}");

    // ePost answers JSON for every documented status; anything else counts as "no value".
    private static async Task<string?> ReadAsync(HttpResponseMessage response, string property, CancellationToken ct)
    {
        try
        {
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            return json.TryGetProperty(property, out var value) && value.GetString() is { Length: > 0 } text
                ? text
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
