using System.Text.RegularExpressions;

namespace backend.Invoicing;

/// <summary>The values a Rechnung supplies for the placeholders of a Textvorlage.</summary>
public record PlaceholderValues(string Name, string Address, string Amount, string InvoiceNumber, string InvoiceDate);

/// <summary>Placeholders such as {{name}} in a Textvorlage's Betreff, Text and Grüsse.</summary>
public static partial class TextTemplatePlaceholders
{
    public static readonly IReadOnlyList<string> Keys = ["name", "address", "amount", "invoiceNumber", "invoiceDate"];

    [GeneratedRegex(@"\{\{\s*([^{}]*?)\s*\}\}")]
    private static partial Regex Pattern();

    /// <summary>Returns the first placeholder in <paramref name="text"/> that is not a valid key, or null.</summary>
    public static string? FindUnknown(string? text) =>
        text is null ? null : Pattern().Matches(text).Select(m => m.Groups[1].Value).FirstOrDefault(k => !Keys.Contains(k));

    public static string Resolve(string text, PlaceholderValues v) =>
        Pattern()
            .Replace(
                text,
                m =>
                    m.Groups[1].Value switch
                    {
                        "name" => v.Name,
                        "address" => v.Address,
                        "amount" => v.Amount,
                        "invoiceNumber" => v.InvoiceNumber,
                        "invoiceDate" => v.InvoiceDate,
                        _ => m.Value,
                    }
            );
}
