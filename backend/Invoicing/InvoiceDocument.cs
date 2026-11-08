using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace backend.Invoicing;

/// <summary>Everything the PDF shows. The template texts are not persisted anywhere else.</summary>
public record InvoiceDocumentData(
    string Number,
    DateOnly Date,
    string RecipientName,
    string TemplateTitle,
    string TemplateText,
    string TemplateGreetings,
    IReadOnlyList<InvoiceLineDraft> Lines
);

/// <summary>Renders a Rechnung to PDF bytes. Minimal first version; the full layout follows separately.</summary>
public static class InvoiceDocument
{
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
                    page.Margin(20, Unit.Millimetre);
                    page.DefaultTextStyle(t => t.FontSize(11));
                    page.Content().Column(col =>
                    {
                        col.Spacing(8);
                        col.Item().Text(data.RecipientName);
                        col.Item().Text($"Rechnungsdatum: {data.Date:dd.MM.yyyy}");
                        col.Item().Text($"Rechnung Nr. {data.Number}").Bold();
                        col.Item().Text(data.TemplateTitle).Bold();
                        col.Item().Text(data.TemplateText);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn();
                                c.ConstantColumn(90);
                            });
                            foreach (var line in data.Lines)
                            {
                                table.Cell().Text(line.Text);
                                table.Cell().AlignRight().Text(FormatAmount(line.Amount));
                            }
                            table.Cell().Text("Total").Bold();
                            table.Cell().AlignRight().Text(FormatAmount(total)).Bold();
                        });
                        col.Item().Text(data.TemplateGreetings);
                    });
                })
            )
            .GeneratePdf();
    }

    /// <summary>Swiss format, e.g. CHF 1'234.50.</summary>
    public static string FormatAmount(decimal amount) =>
        "CHF " + amount.ToString("#,##0.00", new NumberFormatInfo { NumberGroupSeparator = "'", NumberDecimalSeparator = "." });
}
