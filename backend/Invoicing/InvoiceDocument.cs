using System.Globalization;
using System.Reflection;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace backend.Invoicing;

/// <summary>
/// Everything the PDF shows. The template texts are not persisted anywhere else.
/// <paramref name="RecipientAddressLines"/> is empty when the Person has no address (only the name is printed).
/// </summary>
public record InvoiceDocumentData(
    string Number,
    DateOnly Date,
    string RecipientName,
    IReadOnlyList<string> RecipientAddressLines,
    string TemplateTitle,
    string TemplateText,
    string TemplateGreetings,
    IReadOnlyList<InvoiceLineDraft> Lines
);

/// <summary>Renders a Rechnung to PDF bytes: A4, German, logo top left, address for a right-window envelope.</summary>
public static class InvoiceDocument
{
    // Swiss right-window envelope (C5/6): the window starts ~118 mm from the left edge and ~45 mm from the top.
    private const float PageMarginMm = 20;
    private const float AddressLeftMm = 118;
    private const float AddressWidthMm = 68;
    private const float LogoWidthMm = 40;
    private const float LogoHeightMm = 20;
    private const float AddressTopMm = 45;

    private static readonly byte[] Logo = LoadLogo();

    /// <summary>The club is well below the revenue threshold of the QuestPDF Community license.</summary>
    public static void UseCommunityLicense() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] Render(InvoiceDocumentData data)
    {
        UseCommunityLicense();
        var total = InvoiceRules.Total(data.Lines);
        return Document
            .Create(doc =>
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(PageMarginMm, Unit.Millimetre);
                    page.DefaultTextStyle(t => t.FontSize(11).FontFamily(Fonts.Arial));
                    page.Content().Column(col =>
                    {
                        col.Item().Height(LogoHeightMm, Unit.Millimetre).Width(LogoWidthMm, Unit.Millimetre).Image(Logo);

                        col.Item()
                            .PaddingTop(AddressTopMm - PageMarginMm - LogoHeightMm, Unit.Millimetre)
                            .PaddingLeft(AddressLeftMm - PageMarginMm, Unit.Millimetre)
                            .Width(AddressWidthMm, Unit.Millimetre)
                            .Column(addr =>
                            {
                                addr.Item().Text(data.RecipientName);
                                foreach (var line in data.RecipientAddressLines)
                                    addr.Item().Text(line);
                            });

                        col.Item().PaddingTop(25, Unit.Millimetre).Text($"Rechnungsdatum: {data.Date:dd.MM.yyyy}");
                        col.Item().PaddingTop(2, Unit.Millimetre).Text($"Rechnung Nr. {data.Number}");
                        col.Item().PaddingTop(10, Unit.Millimetre).Text(data.TemplateTitle).Bold().FontSize(13);
                        col.Item().PaddingTop(4, Unit.Millimetre).Text(data.TemplateText);
                        col.Item()
                            .PaddingTop(8, Unit.Millimetre)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn();
                                    c.ConstantColumn(35, Unit.Millimetre);
                                });
                                table.Header(h =>
                                {
                                    h.Cell().Element(HeaderCell).Text("Text").Bold();
                                    h.Cell().Element(HeaderCell).AlignRight().Text("Betrag").Bold();
                                });
                                foreach (var line in data.Lines)
                                {
                                    table.Cell().Element(BodyCell).Text(line.Text);
                                    table.Cell().Element(BodyCell).AlignRight().Text(FormatAmount(line.Amount));
                                }
                                table.Cell().Element(TotalCell).Text("Total").Bold();
                                table.Cell().Element(TotalCell).AlignRight().Text(FormatAmount(total)).Bold();
                            });
                        col.Item().PaddingTop(10, Unit.Millimetre).Text(data.TemplateGreetings);
                    });
                })
            )
            .GeneratePdf();
    }

    /// <summary>Address lines below the name: street, "PLZ Ort", Land. Missing parts are omitted.</summary>
    public static IReadOnlyList<string> AddressLines(string? address, string? postalCode, string? city, string? country)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(address))
            lines.Add(address.Trim());
        var cityLine = $"{postalCode?.Trim()} {city?.Trim()}".Trim();
        if (cityLine.Length > 0)
            lines.Add(cityLine);
        if (!string.IsNullOrWhiteSpace(country))
            lines.Add(country.Trim());
        return lines;
    }

    /// <summary>Swiss format, e.g. CHF 1'234.50.</summary>
    public static string FormatAmount(decimal amount) =>
        "CHF " + amount.ToString("#,##0.00", new NumberFormatInfo { NumberGroupSeparator = "'", NumberDecimalSeparator = "." });

    private static IContainer HeaderCell(IContainer c) =>
        c.BorderBottom(1).BorderColor(Colors.Black).PaddingVertical(2, Unit.Millimetre);

    private static IContainer BodyCell(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(2, Unit.Millimetre);

    private static IContainer TotalCell(IContainer c) => c.PaddingVertical(2, Unit.Millimetre);

    private static byte[] LoadLogo()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames().Single(n => n.EndsWith("logo-placeholder.png"));
        using var stream = asm.GetManifestResourceStream(name)!;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
