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
    public void A_missing_title_is_rejected(string? title) =>
        Assert.Equal("Der Titel ist erforderlich.", TextTemplateRules.Validate(title, "Text", "Grüsse"));

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void A_missing_text_is_rejected(string? text) =>
        Assert.Equal("Der Text ist erforderlich.", TextTemplateRules.Validate("Titel", text, "Grüsse"));

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Missing_greetings_are_rejected(string? greetings) =>
        Assert.Equal("Die Grüsse sind erforderlich.", TextTemplateRules.Validate("Titel", "Text", greetings));
}
