using System.Text.Json.Nodes;
using PersonEntity = backend.Person.Person;
using SettingsEntity = backend.Settings.Settings;

namespace backend.Invoicing;

/// <summary>
/// Delivery Channel Email through ePost, used when no SMTP group is set (ADR 0008): ePost mails the Invoice PDF as an
/// attachment. ePost sends it from its own address, so the club's address is only the Reply-To; setting a sender
/// would need DKIM and SPF records of the club's domain. Every call is a real, billed delivery.
/// </summary>
public class EPostEmailInvoiceSender(HttpClient http) : IInvoiceSender
{
    public DeliveryBackend Backend => DeliveryBackend.EPostEmail;

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
        var outcome = await EPostApi.DeliverAsync(
            http,
            settings.EPostApiKey!,
            fileName,
            pdf,
            new JsonObject
            {
                ["fileName"] = fileName,
                ["deliveryChannelPreferences"] = new JsonArray("EMAIL"),
                ["senderName"] = settings.Name,
                ["senderEndToEndId"] = formattedNumber,
                ["documentTitle"] = message.Subject,
                ["recipients"] = new JsonArray(
                    new JsonObject
                    {
                        ["unhashedCredentials"] = new JsonObject { ["email"] = recipient.Email!.Trim() },
                    }
                ),
                ["emailAdditionalInfo"] = new JsonObject
                {
                    ["emailSubject"] = message.Subject,
                    ["emailText"] = message.Text,
                    ["replyTo"] = new JsonArray(
                        new JsonObject { ["email"] = settings.FromAddress, ["name"] = settings.Name }
                    ),
                },
            },
            ct
        );
        return outcome.DeliveryId is { } id
            ? new InvoiceSendResult($"per Email (ePost, {id})", null)
            : new InvoiceSendResult(null, $"Die E-Mail konnte nicht gesendet werden: {outcome.Failure}");
    }
}
