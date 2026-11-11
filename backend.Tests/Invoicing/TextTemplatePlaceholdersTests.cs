using backend.Invoicing;

namespace backend.Tests.Invoicing;

public class TextTemplatePlaceholdersTests
{
    private static readonly PlaceholderValues Values = new(
        "Anna Muster",
        "Hauptstrasse 1, 8000 Zürich",
        "CHF 1'234.50",
        "#0042",
        "11.11.2026"
    );

    [Fact]
    public void All_placeholders_are_replaced() =>
        Assert.Equal(
            "Anna Muster | Hauptstrasse 1, 8000 Zürich | CHF 1'234.50 | #0042 | 11.11.2026",
            TextTemplatePlaceholders.Resolve(
                "{{name}} | {{address}} | {{amount}} | {{invoiceNumber}} | {{invoiceDate}}",
                Values
            )
        );

    [Fact]
    public void A_text_without_placeholders_is_unchanged() =>
        Assert.Equal("Wir stellen {Ihnen} in Rechnung:", TextTemplatePlaceholders.Resolve("Wir stellen {Ihnen} in Rechnung:", Values));

    [Fact]
    public void A_repeated_placeholder_is_replaced_everywhere() =>
        Assert.Equal("Anna Muster, Anna Muster", TextTemplatePlaceholders.Resolve("{{name}}, {{name}}", Values));
}
