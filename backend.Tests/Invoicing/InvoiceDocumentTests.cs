using System.Text;
using backend.Invoicing;
using UglyToad.PdfPig;

namespace backend.Tests.Invoicing;

public class InvoiceDocumentTests
{
    static InvoiceDocumentTests() => InvoiceDocument.UseCommunityLicense();

    private static readonly QrBillCreditor Club = new(
        "CH9300762011623852957",
        "SC Wettingen",
        "Sportweg 12",
        "5430",
        "Wettingen"
    );

    private static InvoiceDocumentData Data(
        IReadOnlyList<string>? address = null,
        int extraLines = 0,
        bool openAmount = false
    ) =>
        new(
            Number: "0042",
            Date: new DateOnly(2026, 11, 8),
            RecipientName: "Muster Hans",
            RecipientAddressLines: address ?? ["Dorfstrasse 1", "8000 Zürich", "Schweiz"],
            TemplateTitle: "Jahresbeitrag 2026",
            TemplateText: "Wir erlauben uns, Ihnen folgendes zu verrechnen.",
            TemplateGreetings: "Freundliche Grüsse",
            Lines:
            [
                new InvoiceLineDraft("Jahresbeitrag", 100m, 3400),
                new InvoiceLineDraft("Spende", 1234.50m, 3600),
                .. Enumerable.Range(1, extraLines).Select(i => new InvoiceLineDraft($"Position {i}", 1m, 3400)),
            ],
            Creditor: Club,
            Debtor: null,
            OpenAmount: openAmount
        );

    private static string Text(byte[] pdf)
    {
        using var doc = PdfDocument.Open(pdf);
        return string.Join("\n", doc.GetPages().Select(p => p.Text));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Total_is_printed_in_the_table_whether_or_not_the_amount_is_left_open(bool openAmount)
    {
        var text = Text(InvoiceDocument.Render(Data(openAmount: openAmount)));

        Assert.Contains("CHF 1'334.50", text);
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

        Assert.Contains("Rechnung: #0042 / 08.11.2026", text);
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
        Assert.Contains("Rechnung: #0042 / 08.11.2026", text);
        Assert.Contains("CHF 1'334.50", text);
        Assert.DoesNotContain("Dorfstrasse", text);
    }

    [Fact]
    public void AddressLines_omit_missing_parts_and_print_Land_only_if_set()
    {
        Assert.Equal(
            ["Dorfstrasse 1", "8000 Zürich"],
            InvoiceDocument.AddressLines("Dorfstrasse", "1", "8000", "Zürich", null)
        );
        Assert.Equal(
            ["Zürich", "Schweiz"],
            InvoiceDocument.AddressLines(" ", "", null, "Zürich", "Schweiz")
        );
        Assert.Empty(InvoiceDocument.AddressLines(null, null, null, null, null));
    }

    private const double PointsPerMm = 72 / 25.4;

    /// <summary>Rounding slack in PDF coordinates, in points.</summary>
    private const double PositionTolerance = 0.5;

    [Fact]
    public void Short_Rechnung_has_the_payment_part_at_the_bottom_of_its_only_page()
    {
        using var doc = PdfDocument.Open(InvoiceDocument.Render(Data()));

        var page = Assert.Single(doc.GetPages());
        var slip = Assert.Single(page.GetImages(), i => i.Bounds.Width > 100 * PointsPerMm);
        Assert.InRange(slip.Bounds.Width, 594, 596); // QuestPDF A4 is 595 x 842 pt
        Assert.InRange(slip.Bounds.Height, 296.5, 298.5);
        Assert.InRange(slip.Bounds.Left, -PositionTolerance, PositionTolerance);
        Assert.InRange(slip.Bounds.Bottom, -PositionTolerance, PositionTolerance); // PDF origin is bottom left
    }

    [Fact]
    public void Long_Rechnung_pushes_the_payment_part_onto_its_own_page_without_overlap()
    {
        // Enough lines to leave less than 105 mm free on the last page of the content.
        for (var extra = 20; extra < 60; extra++)
        {
            using var doc = PdfDocument.Open(InvoiceDocument.Render(Data(extraLines: extra)));
            var pages = doc.GetPages().ToList();
            var last = pages[^1];
            var slip = Assert.Single(last.GetImages(), i => i.Bounds.Width > 100 * PointsPerMm);
            Assert.InRange(slip.Bounds.Bottom, -PositionTolerance, PositionTolerance); // PDF origin is bottom left
            // No text on the last page reaches into the payment part.
            Assert.All(last.Letters, l => Assert.True(l.GlyphRectangle.Bottom >= slip.Bounds.Top - PositionTolerance));
            // Every page except the last carries no payment part.
            Assert.All(pages.SkipLast(1), p => Assert.DoesNotContain(p.GetImages(), i => i.Bounds.Width > 100 * PointsPerMm));
        }
    }

    [Fact]
    public void At_least_one_long_Rechnung_gets_a_page_with_only_the_payment_part()
    {
        var sawOwnPage = false;
        for (var extra = 20; extra < 60 && !sawOwnPage; extra++)
        {
            using var doc = PdfDocument.Open(InvoiceDocument.Render(Data(extraLines: extra)));
            sawOwnPage = doc.GetPages().Last().Letters.Count == 0;
        }
        Assert.True(sawOwnPage);
    }
}
