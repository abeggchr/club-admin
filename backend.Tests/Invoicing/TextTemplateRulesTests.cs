using backend.Invoicing;

namespace backend.Tests.Invoicing;

public class TextTemplateRulesTests
{
    [Fact]
    public void A_template_with_title_text_and_greetings_has_no_error() =>
        Assert.Null(TextTemplateRules.Validate("Mitgliederbeitrag", "Wir stellen Ihnen in Rechnung:", "Freundliche Grüsse"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_title_is_rejected(string? subject) =>
        Assert.Equal("Der Betreff ist erforderlich.", TextTemplateRules.Validate(subject, "Text", "Grüsse"));

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void A_missing_text_is_rejected(string? text) =>
        Assert.Equal("Der Text ist erforderlich.", TextTemplateRules.Validate("Betreff", text, "Grüsse"));

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Missing_greetings_are_rejected(string? greetings) =>
        Assert.Equal("Die Grüsse sind erforderlich.", TextTemplateRules.Validate("Betreff", "Text", greetings));

    [Fact]
    public void Known_placeholders_are_accepted() =>
        Assert.Null(
            TextTemplateRules.Validate(
                "Rechnung {{invoiceNumber}}",
                "Guten Tag {{name}}, {{address}}: {{amount}} vom {{invoiceDate}}",
                "Grüsse"
            )
        );

    [Fact]
    public void An_unknown_placeholder_is_rejected_and_the_valid_keys_are_listed()
    {
        var error = TextTemplateRules.Validate("Betreff", "Hallo {{nmae}}", "Grüsse");
        Assert.Equal(
            "Unbekannter Platzhalter {{nmae}}. Gültig sind: {{name}}, {{address}}, {{amount}}, {{invoiceNumber}}, {{invoiceDate}}.",
            error
        );
    }

    [Fact]
    public void An_unknown_placeholder_in_the_greetings_is_rejected() =>
        Assert.Contains("{{foo}}", TextTemplateRules.Validate("Betreff", "Text", "Grüsse {{foo}}"));
}
