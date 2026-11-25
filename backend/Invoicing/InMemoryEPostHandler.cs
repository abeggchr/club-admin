using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace backend.Invoicing;

/// <summary>Answers like api.epost.ch without any network call, for Development and tests: ePost has no sandbox and bills every letter and email.</summary>
public class InMemoryEPostHandler : HttpMessageHandler
{
    public int Calls { get; private set; }
    public string DeliveryId { get; } = "dev-delivery-1";
    public HttpStatusCode Status { get; set; } = HttpStatusCode.Created;
    public string? Body { get; set; }

    /// <summary>The file metadata of every delivery received, oldest first.</summary>
    public List<JsonNode> Deliveries { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++;
        if (request.Content is MultipartFormDataContent form)
        {
            foreach (var part in form.Where(p => p.Headers.ContentDisposition?.Name?.Trim('"') == "metadata"))
            {
                Deliveries.Add(JsonNode.Parse(await part.ReadAsStringAsync(ct))!["fileMetadata"]![0]!);
            }
        }
        var body = Body ?? $$"""{"id":"{{DeliveryId}}"}""";
        return new HttpResponseMessage(Status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
