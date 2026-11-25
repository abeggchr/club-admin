using System.Net;
using System.Text;

namespace backend.Invoicing;

/// <summary>Answers like api.epost.ch without any network call, for Development and tests: ePost has no sandbox and bills every letter.</summary>
public class InMemoryEPostHandler : HttpMessageHandler
{
    public int Calls { get; private set; }
    public string DeliveryId { get; } = "dev-delivery-1";
    public HttpStatusCode Status { get; set; } = HttpStatusCode.Created;
    public string? Body { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++;
        var body = Body ?? $$"""{"id":"{{DeliveryId}}"}""";
        return Task.FromResult(
            new HttpResponseMessage(Status) { Content = new StringContent(body, Encoding.UTF8, "application/json") }
        );
    }
}
