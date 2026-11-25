using System.Net;
using backend.Invoicing;
using PersonEntity = backend.Person.Person;
using SettingsEntity = backend.Settings.Settings;

namespace backend.Tests.Invoicing;

public class EPostEmailInvoiceSenderTests
{
    private static SettingsEntity Configured() =>
        new()
        {
            EPostApiKey = "key-123",
            Name = "SC Wettingen",
            FromAddress = "kasse@example.ch",
        };

    private static PersonEntity Person(string? email = " anna@example.ch ") =>
        new()
        {
            FirstName = "Anna",
            LastName = "Muster",
            Email = email,
        };

    private static (EPostEmailInvoiceSender Sender, InMemoryEPostHandler Handler) Sender(
        HttpStatusCode status = HttpStatusCode.Created,
        string? body = null
    )
    {
        var handler = new InMemoryEPostHandler { Status = status, Body = body };
        return (new EPostEmailInvoiceSender(new HttpClient(handler)), handler);
    }

    private static Task<InvoiceSendResult> Send(EPostEmailInvoiceSender sender) =>
        sender.SendAsync(
            Configured(),
            Person(),
            42,
            new InvoiceMessage("Rechnung", "Guten Tag"),
            [1, 2, 3],
            CancellationToken.None
        );

    [Fact]
    public async Task The_metadata_describes_one_email_delivery_with_the_club_as_Reply_To()
    {
        var (sender, handler) = Sender();

        await Send(sender);

        var file = Assert.Single(handler.Deliveries);
        Assert.Equal("rechnung-0042.pdf", (string?)file["fileName"]);
        Assert.Equal(["EMAIL"], file["deliveryChannelPreferences"]!.AsArray().Select(c => (string?)c));
        Assert.Equal("SC Wettingen", (string?)file["senderName"]);
        Assert.Equal("0042", (string?)file["senderEndToEndId"]);
        Assert.Equal("anna@example.ch", (string?)file["recipients"]![0]!["unhashedCredentials"]!["email"]);
        var email = file["emailAdditionalInfo"]!;
        Assert.Equal("Rechnung", (string?)email["emailSubject"]);
        Assert.Equal("Guten Tag", (string?)email["emailText"]);
        var replyTo = Assert.Single(email["replyTo"]!.AsArray())!;
        Assert.Equal("kasse@example.ch", (string?)replyTo["email"]);
        Assert.Equal("SC Wettingen", (string?)replyTo["name"]);
    }

    [Fact]
    public async Task It_never_sets_a_sender_because_ePost_would_sign_for_a_domain_it_has_no_DKIM_key_of()
    {
        var (sender, handler) = Sender();

        await Send(sender);

        var email = Assert.Single(handler.Deliveries)["emailAdditionalInfo"]!.AsObject();
        Assert.False(email.ContainsKey("sender"));
    }

    [Fact]
    public async Task A_201_becomes_the_Remark_per_Email_via_ePost_with_the_delivery_id()
    {
        var (sender, _) = Sender();

        Assert.Equal(new InvoiceSendResult("per Email (ePost, dev-delivery-1)", null), await Send(sender));
    }

    [Fact]
    public async Task An_error_response_is_a_failure_with_the_message_of_ePost_and_no_Remark()
    {
        var (sender, _) = Sender(HttpStatusCode.BadRequest, """{"message":"Adresse ungültig"}""");

        Assert.Equal(
            new InvoiceSendResult(null, "Die E-Mail konnte nicht gesendet werden: Adresse ungültig"),
            await Send(sender)
        );
    }

    [Fact]
    public void It_is_configured_only_when_ePost_is_the_email_backend()
    {
        var sender = Sender().Sender;

        Assert.True(sender.IsConfigured(Configured()));
        Assert.False(sender.IsConfigured(new SettingsEntity { EPostApiKey = "key-123" }));
        var withSmtp = Configured();
        withSmtp.SmtpHost = "smtp.example.ch";
        Assert.False(sender.IsConfigured(withSmtp));
    }

    [Theory]
    [InlineData("anna@example.ch", true)]
    [InlineData(null, false)]
    [InlineData("  ", false)]
    public void It_delivers_only_to_a_Recipient_with_an_email_address(string? email, bool expected) =>
        Assert.Equal(expected, Sender().Sender.CanDeliverTo(Person(email)));
}
