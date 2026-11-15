using System.Globalization;
using System.Reflection;
using QuestPDF.Elements;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace backend.Invoicing;

/// <summary>
/// Everything the PDF shows. The template texts are not persisted anywhere else.
/// <paramref name="RecipientAddressLines"/> is empty when the Person has no address (only the name is printed).
/// <paramref name="Debtor"/> is null when the Person's address is incomplete (the QR-bill then has no debtor).
/// </summary>
public record InvoiceDocumentData(
    string Number,
    DateOnly Date,
    string RecipientName,
    IReadOnlyList<string> RecipientAddressLines,
    string TemplateTitle,
    string TemplateText,
    string TemplateGreetings,
    IReadOnlyList<InvoiceLineDraft> Lines,
    QrBillCreditor Creditor,
    QrBillDebtor? Debtor,
    bool OpenAmount = false
);

/// <summary>Renders an Invoice to PDF bytes: A4, German, logo top left, address for a right-window envelope.</summary>
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

    /// <summary>The club is well below the revenue threshold of the QuestPDF Community license. Call once at startup.</summary>
    public static void UseCommunityLicense() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] Render(InvoiceDocumentData data)
    {
        var total = InvoiceRules.Total(data.Lines);
        var slip = QrBillSlip.RenderPng(
            QrBillSlip.Build(
                data.Creditor,
                data.Number,
                data.OpenAmount ? null : total,
                data.Debtor
            )
        );
        return Document
            .Create(doc =>
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    // No page margin: the payment part spans the full page width. The content is padded instead.
                    page.DefaultTextStyle(t => t.FontSize(11).FontFamily(Fonts.Arial));
                    page.Content()
                        .Column(outer =>
                        {
                            outer
                                .Item()
                                .PaddingHorizontal(PageMarginMm, Unit.Millimetre)
                                .PaddingTop(PageMarginMm, Unit.Millimetre)
                                .Column(col =>
                                {
                                    col.Item()
                                        .Height(LogoHeightMm, Unit.Millimetre)
                                        .Width(LogoWidthMm, Unit.Millimetre)
                                        .Image(Logo);

                                    col.Item()
                                        .PaddingTop(
                                            AddressTopMm - PageMarginMm - LogoHeightMm,
                                            Unit.Millimetre
                                        )
                                        .PaddingLeft(AddressLeftMm - PageMarginMm, Unit.Millimetre)
                                        .Width(AddressWidthMm, Unit.Millimetre)
                                        .Column(addr =>
                                        {
                                            addr.Item().Text(data.RecipientName);
                                            foreach (var line in data.RecipientAddressLines)
                                                addr.Item().Text(line);
                                        });

                                    col.Item()
                                        .PaddingTop(10, Unit.Millimetre)
                                        .Text($"Rechnung: #{data.Number} / {data.Date:dd.MM.yyyy}");
                                    col.Item()
                                        .PaddingTop(10, Unit.Millimetre)
                                        .Text(data.TemplateTitle)
                                        .Bold()
                                        .FontSize(13);
                                    col.Item()
                                        .PaddingTop(4, Unit.Millimetre)
                                        .Text(data.TemplateText);
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
                                                h.Cell()
                                                    .Element(HeaderCell)
                                                    .AlignRight()
                                                    .Text("Betrag")
                                                    .Bold();
                                            });
                                            foreach (var line in data.Lines)
                                            {
                                                table.Cell().Element(BodyCell).Text(line.Text);
                                                table
                                                    .Cell()
                                                    .Element(BodyCell)
                                                    .AlignRight()
                                                    .Text(FormatAmount(line.Amount));
                                            }
                                            table.Cell().Element(TotalCell).Text("Total").Bold();
                                            table
                                                .Cell()
                                                .Element(TotalCell)
                                                .AlignRight()
                                                .Text(FormatAmount(total))
                                                .Bold();
                                        });
                                    col.Item()
                                        .PaddingTop(10, Unit.Millimetre)
                                        .Text(data.TemplateGreetings);
                                });
                            outer.Item().Dynamic(new PaymentPartComponent(slip));
                        });
                })
            )
            .GeneratePdf();
    }

    /// <summary>Address lines below the name: street, "PLZ Ort", Land. Missing parts are omitted.</summary>
    public static IReadOnlyList<string> AddressLines(
        string? street,
        string? number,
        string? postalCode,
        string? city,
        string? country
    )
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(street) && !string.IsNullOrWhiteSpace(number))
            lines.Add(street.Trim() + " " + number.Trim());
        var cityLine = $"{postalCode?.Trim()} {city?.Trim()}".Trim();
        if (cityLine.Length > 0)
            lines.Add(cityLine);
        if (!string.IsNullOrWhiteSpace(country))
            lines.Add(country.Trim());
        return lines;
    }

    /// <summary>Swiss format, e.g. CHF 1'234.50.</summary>
    public static string FormatAmount(decimal amount) =>
        "CHF "
        + amount.ToString(
            "#,##0.00",
            new NumberFormatInfo { NumberGroupSeparator = "'", NumberDecimalSeparator = "." }
        );

    private static IContainer HeaderCell(IContainer c) =>
        c.BorderBottom(1).BorderColor(Colors.Black).PaddingVertical(2, Unit.Millimetre);

    private static IContainer BodyCell(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(2, Unit.Millimetre);

    private static IContainer TotalCell(IContainer c) => c.PaddingVertical(2, Unit.Millimetre);

    /// <summary>
    /// The payment part, pinned to the bottom of the page it lands on. When less than 105 mm is left on the
    /// current page it is deferred to the next page.
    /// </summary>
    private sealed class PaymentPartComponent(byte[] png) : IDynamicComponent<int>
    {
        /// <summary>Points by which the free space may fall short of the payment part height and still count as fitting.</summary>
        private const float FitTolerance = 0.5f;

        /// <summary>Points left unused so that float rounding never overflows the page.</summary>
        private const float RoundingReserve = 0.1f;

        public int State { get; set; }

        public DynamicComponentComposeResult Compose(DynamicContext context)
        {
            var height = QrBillSlip.HeightMm * (72f / 25.4f);
            if (context.AvailableSize.Height < height - FitTolerance)
                return new DynamicComponentComposeResult
                {
                    // Fill the rest of this page so that the payment part starts on the next one.
                    Content = context.CreateElement(e =>
                        e.Height(Math.Max(0, context.AvailableSize.Height - RoundingReserve))
                    ),
                    HasMoreContent = true,
                };

            return new DynamicComponentComposeResult
            {
                Content = context.CreateElement(e =>
                    e.Column(c =>
                    {
                        // A spacer pushes the image to the bottom; the reserve avoids float rounding overflow.
                        c.Item()
                            .Height(
                                Math.Max(0, context.AvailableSize.Height - height - RoundingReserve)
                            );
                        c.Item().Height(QrBillSlip.HeightMm, Unit.Millimetre).Image(png).FitArea();
                    })
                ),
                HasMoreContent = false,
            };
        }
    }

    private static byte[] LoadLogo()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames().Single(n => n.EndsWith("logo.png"));
        using var stream = asm.GetManifestResourceStream(name)!;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
