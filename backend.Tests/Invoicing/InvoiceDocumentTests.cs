using System.Text;
using backend.Invoicing;
using UglyToad.PdfPig;

namespace backend.Tests.Invoicing;

public class InvoiceDocumentTests
{
    private static InvoiceDocumentData Data(IReadOnlyList<string>? address = null) =>
        new(
            Number: "0042",
            Date: new DateOnly(2026, 11, 8),
            RecipientName: "Muster Hans",
            RecipientAddressLines: address ?? ["Dorfstrasse 1", "8000 Zürich", "Schweiz"],
            TemplateTitle: "Jahresbeitrag 2026",
            TemplateText: "Wir erlauben uns, Ihnen folgendes zu verrechnen.",
            TemplateGreetings: "Freundliche Grüsse",
            Lines: [new InvoiceLineDraft("Jahresbeitrag", 100m), new InvoiceLineDraft("Spende", 1234.50m)]
        );

    private static string Text(byte[] pdf)
    {
        using var doc = PdfDocument.Open(pdf);
        return string.Join("\n", doc.GetPages().Select(p => p.Text));
    }

    [Fact]
    public void Rendering_produces_a_non_empty_PDF()
    {
        var pdf = InvoiceDocument.Render(Data());

        Assert.NotEmpty(pdf);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void PDF_shows_number_date_and_formatted_total()
    {
        var text = Text(InvoiceDocument.Render(Data()));

        Assert.Contains("Rechnung Nr. 0042", text);
        Assert.Contains("Rechnungsdatum: 08.11.2026", text);
        Assert.Contains("CHF 1'334.50", text);
    }

    [Fact]
    public void PDF_shows_recipient_address_title_text_and_greetings()
    {
        var text = Text(InvoiceDocument.Render(Data()));

        Assert.Contains("Muster Hans", text);
        Assert.Contains("Dorfstrasse 1", text);
        Assert.Contains("8000 Zürich", text);
        Assert.Contains("Schweiz", text);
        Assert.Contains("Jahresbeitrag 2026", text);
        Assert.Contains("Wir erlauben uns", text);
        Assert.Contains("Freundliche Grüsse", text);
    }

    [Fact]
    public void PDF_for_a_Person_without_address_shows_only_the_name()
    {
        var text = Text(InvoiceDocument.Render(Data(address: [])));

        Assert.Contains("Muster Hans", text);
        Assert.Contains("Rechnung Nr. 0042", text);
        Assert.Contains("Rechnungsdatum: 08.11.2026", text);
        Assert.Contains("CHF 1'334.50", text);
        Assert.DoesNotContain("Dorfstrasse", text);
    }

    [Fact]
    public void AddressLines_omit_missing_parts_and_print_Land_only_if_set()
    {
        Assert.Equal(
            ["Dorfstrasse 1", "8000 Zürich"],
            InvoiceDocument.AddressLines("Dorfstrasse 1", "8000", "Zürich", null)
        );
        Assert.Equal(["Zürich", "Schweiz"], InvoiceDocument.AddressLines(" ", null, "Zürich", "Schweiz"));
        Assert.Empty(InvoiceDocument.AddressLines(null, null, null, null));
    }
}
