using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using backend.Invoicing;
using PersonEntity = backend.Person.Person;
using SettingsEntity = backend.Settings.Settings;

namespace backend.Tests.Invoicing;

public class EPostInvoiceSenderTests
{
    /// <summary>Stands in for api.epost.ch: records the request and its multipart parts, answers with a canned response.</summary>
    private sealed class FakeEPostHandler : HttpMessageHandler
    {
        public record Part(string Name, string? FileName, string? ContentType, byte[] Bytes);

        public HttpStatusCode Status { get; set; } = HttpStatusCode.Created;
        public string Body { get; set; } = """{"id":"d-123"}""";
        public HttpRequestMessage? Request { get; private set; }
        public List<Part> Parts { get; } = [];

        public JsonNode Metadata =>
            JsonNode.Parse(Encoding.UTF8.GetString(Parts.Single(p => p.Name == "metadata").Bytes))!;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            foreach (var part in (MultipartFormDataContent)request.Content!)
            {
                Parts.Add(
                    new Part(
                        part.Headers.ContentDisposition!.Name!.Trim('"'),
                        part.Headers.ContentDisposition.FileName?.Trim('"'),
                        part.Headers.ContentType?.MediaType,
                        await part.ReadAsByteArrayAsync(ct)
                    )
                );
            }
            return new HttpResponseMessage(Status) { Content = new StringContent(Body, Encoding.UTF8, "application/json") };
        }
    }

    private static SettingsEntity Configured() =>
        new()
        {
            EPostApiKey = "key-123",
            Name = "SC Wettingen",
            Address = "Sportweg 12",
            PostalCode = "5430",
            City = "Wettingen",
        };

    private static PersonEntity Recipient(
        string? country = null,
        string? street = "Bahnhofstrasse",
        string? postalCode = "5400",
        string? city = "Baden"
    ) =>
        new()
        {
            FirstName = "Anna",
            LastName = "Muster",
            Street = street,
            Number = "7a",
            PostalCode = postalCode,
            City = city,
            Country = country,
        };

    private static EPostInvoiceSender Sender(FakeEPostHandler? handler = null) =>
        new(new HttpClient(handler ?? new FakeEPostHandler()));

    private static Task<InvoiceSendResult> Send(FakeEPostHandler handler) =>
        Sender(handler).SendAsync(
            Configured(),
            Recipient(),
            42,
            new InvoiceMessage(null, null),
            [1, 2, 3],
            CancellationToken.None
        );

    [Fact]
    public async Task It_posts_to_the_deliveries_endpoint_with_the_API_key_and_the_PDF()
    {
        var handler = new FakeEPostHandler();

        await Send(handler);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://api.epost.ch/epost/v2/deliveries", handler.Request.RequestUri!.ToString());
        Assert.Equal("key-123", Assert.Single(handler.Request.Headers.GetValues("X-API-KEY")));
        Assert.Equal(["files", "metadata"], handler.Parts.Select(p => p.Name));
        var file = handler.Parts.Single(p => p.Name == "files");
        Assert.Equal("rechnung-0042.pdf", file.FileName);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal(new byte[] { 1, 2, 3 }, file.Bytes);
        Assert.Equal("application/json", handler.Parts.Single(p => p.Name == "metadata").ContentType);
    }

    [Fact]
    public async Task The_metadata_describes_one_physical_B_Post_letter_to_the_Recipient()
    {
        var handler = new FakeEPostHandler();

        await Send(handler);

        var file = handler.Metadata["fileMetadata"]!.AsArray().Single()!;
        Assert.Equal("rechnung-0042.pdf", (string?)file["fileName"]);
        Assert.Equal(["PHYSICAL"], file["deliveryChannelPreferences"]!.AsArray().Select(c => (string?)c));
        Assert.Equal("0042", (string?)file["senderEndToEndId"]);
        var credentials = file["recipients"]!.AsArray().Single()!["unhashedCredentials"]!;
        Assert.Equal("Anna", (string?)credentials["firstName"]);
        Assert.Equal("Muster", (string?)credentials["lastName"]);
        Assert.Equal("Bahnhofstrasse", (string?)credentials["street"]);
        Assert.Equal("7a", (string?)credentials["streetNumber"]);
        Assert.Equal("5400", (string?)credentials["zipCode"]);
        Assert.Equal("Baden", (string?)credentials["city"]);
        var physical = file["physicalAdditionalInfo"]!;
        Assert.Equal("B_POST", (string?)physical["postage"]);
        Assert.False((bool)physical["duplex"]!);
        Assert.Equal("RIGHT", (string?)physical["windowLocation"]);
        Assert.Equal("C5", (string?)physical["envelopeSize"]);
        Assert.Equal("CH", (string?)physical["shippingCountry"]);
        var sender = physical["senderAddress"]!;
        Assert.Equal("SC Wettingen", (string?)sender["name"]);
        Assert.Equal("Sportweg 12", (string?)sender["street"]);
        Assert.Equal("5430", (string?)sender["zipCode"]);
        Assert.Equal("Wettingen", (string?)sender["city"]);
    }

    [Fact]
    public async Task The_metadata_leaves_out_the_eBill_fields()
    {
        var handler = new FakeEPostHandler();

        await Send(handler);

        var file = handler.Metadata["fileMetadata"]!.AsArray().Single()!.AsObject();
        Assert.False(file.ContainsKey("documentTypes"));
        Assert.False(file["physicalAdditionalInfo"]!.AsObject().ContainsKey("qrInvoicePageNumber"));
    }

    [Fact]
    public async Task A_201_becomes_the_Remark_per_Post_with_the_delivery_id()
    {
        var result = await Send(new FakeEPostHandler());

        Assert.Equal(new InvoiceSendResult("per Post (d-123)", null), result);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.UnsupportedMediaType)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task An_error_response_is_a_failure_with_the_message_of_ePost_and_no_Remark(HttpStatusCode status)
    {
        var handler = new FakeEPostHandler { Status = status, Body = """{"code":"X","message":"Adresse ungültig"}""" };

        var result = await Send(handler);

        Assert.Equal(new InvoiceSendResult(null, "Der Brief konnte nicht versendet werden: Adresse ungültig"), result);
    }

    [Fact]
    public async Task An_error_response_without_a_message_names_the_status_code()
    {
        var handler = new FakeEPostHandler { Status = HttpStatusCode.TooManyRequests, Body = "" };

        var result = await Send(handler);

        Assert.Equal(new InvoiceSendResult(null, "Der Brief konnte nicht versendet werden: HTTP 429"), result);
    }

    [Fact]
    public async Task An_unexpected_success_code_is_a_failure()
    {
        var result = await Send(new FakeEPostHandler { Status = HttpStatusCode.OK });

        Assert.Null(result.Remark);
        Assert.NotNull(result.Error);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("CH", true)]
    [InlineData("ch", true)]
    [InlineData("Schweiz", true)]
    [InlineData("SCHWEIZ", true)]
    [InlineData("DE", false)]
    [InlineData("Deutschland", false)]
    public void It_delivers_only_to_Switzerland(string? country, bool expected) =>
        Assert.Equal(expected, Sender().CanDeliverTo(Recipient(country)));

    [Theory]
    [InlineData(null, "5400", "Baden")]
    [InlineData("Bahnhofstrasse", " ", "Baden")]
    [InlineData("Bahnhofstrasse", "5400", null)]
    public void It_needs_Street_PostalCode_and_City(string? street, string? postalCode, string? city) =>
        Assert.False(Sender().CanDeliverTo(Recipient(street: street, postalCode: postalCode, city: city)));

    [Fact]
    public void The_channel_is_configured_once_an_API_key_is_stored()
    {
        Assert.True(Sender().IsConfigured(Configured()));
        Assert.False(Sender().IsConfigured(new SettingsEntity()));
    }
}
