using System.Text;
using backend.Invoicing;

namespace backend.Tests.Invoicing;

public class InvoiceDocumentTests
{
    private static InvoiceDocumentData Data() =>
        new(
            Number: "0042",
            Date: new DateOnly(2026, 11, 8),
            RecipientName: "Muster Hans",
            TemplateTitle: "Jahresbeitrag 2026",
            TemplateText: "Wir erlauben uns, Ihnen folgendes zu verrechnen.",
            TemplateGreetings: "Freundliche Grüsse",
            Lines: [new InvoiceLineDraft("Jahresbeitrag", 100m), new InvoiceLineDraft("Spende", 50.50m)]
        );

    [Fact]
    public void Rendering_produces_a_non_empty_PDF()
    {
        var pdf = InvoiceDocument.Render(Data());

        Assert.NotEmpty(pdf);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }
}
