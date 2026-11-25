using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace backend.Invoicing;

/// <summary>The one ePost call both senders share: a multipart delivery of a PDF with its file metadata. Every call is a real, billed delivery; ePost has no sandbox.</summary>
internal static class EPostApi
{
    private const string DeliveriesUrl = "https://api.epost.ch/epost/v2/deliveries";

    /// <summary>The delivery id, or a reason (the message of ePost, or the HTTP status) when the delivery was not accepted.</summary>
    public record Outcome(string? DeliveryId, string? Failure);

    public static async Task<Outcome> DeliverAsync(
        HttpClient http,
        string apiKey,
        string fileName,
        byte[] pdf,
        JsonObject fileMetadata,
        CancellationToken ct
    )
    {
        var pdfPart = new ByteArrayContent(pdf);
        pdfPart.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        var metadata = new JsonObject { ["fileMetadata"] = new JsonArray(fileMetadata) };
        var metadataPart = new StringContent(metadata.ToJsonString(), Encoding.UTF8, "application/json");
        using var form = new MultipartFormDataContent
        {
            { pdfPart, "files", fileName },
            { metadataPart, "metadata" },
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, DeliveriesUrl) { Content = form };
        request.Headers.Add("X-API-KEY", apiKey);

        try
        {
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Created && await ReadAsync(response, "id", ct) is { } id)
            {
                return new Outcome(id, null);
            }
            return new Outcome(null, await ReadAsync(response, "message", ct) ?? $"HTTP {(int)response.StatusCode}");
        }
        catch (HttpRequestException e)
        {
            return new Outcome(null, e.Message);
        }
        // HttpClient reports its own timeout as a cancellation; only the caller's token is a real cancel.
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new Outcome(null, "Zeitüberschreitung");
        }
    }

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
