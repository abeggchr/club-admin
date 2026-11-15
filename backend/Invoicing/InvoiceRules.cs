using backend.Accounting;

namespace backend.Invoicing;

/// <summary>One Rechnungsposition as supplied by the client.</summary>
public record InvoiceLineDraft(string? Text, decimal Amount, int AccountNumber);

/// <summary>The dashboard's Rechnungen: how many are open and what their full totals sum to.</summary>
public record InvoiceSummary(int OpenCount, decimal OpenAmount);

/// <summary>Pure rules for creating a Rechnung. Errors are German messages, or null when valid.</summary>
public static class InvoiceRules
{
    public static string? Validate(
        int? personId,
        int? templateId,
        IReadOnlyList<InvoiceLineDraft> lines,
        ChartOfAccounts chart
    )
    {
        if (personId is null) return "Die Person ist erforderlich.";
        if (templateId is null) return "Die Textvorlage ist erforderlich.";
        if (lines.Count == 0) return "Mindestens eine Rechnungsposition ist erforderlich.";
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line.Text)) return "Jede Rechnungsposition braucht einen Text.";
            if (line.Amount == 0) return "Der Betrag einer Rechnungsposition darf nicht 0 sein.";
            if (line.Amount != decimal.Round(line.Amount, 2))
                return "Der Betrag darf höchstens zwei Dezimalstellen haben.";
            var account = chart.Find(line.AccountNumber);
            if (account is null)
            {
                return $"Das Konto {line.AccountNumber} existiert nicht.";
            }
            if (!account.Active)
            {
                return $"Das Konto {account.Number} {account.Name} ist inaktiv.";
            }
        }
        if (Total(lines) <= 0) return "Die Summe der Rechnungspositionen muss grösser als 0 sein.";
        return null;
    }

    /// <summary>The Rechnungsnummer: the ID with at least four digits.</summary>
    public static string FormatNumber(int id) => id.ToString("D4");

    public static decimal Total(IEnumerable<InvoiceLineDraft> lines) => lines.Sum(l => l.Amount);

    /// <summary>
    /// The Rechnungsstatus a Rechnung currently has: the newest status change, or Created for a
    /// Rechnung without any status change yet.
    /// </summary>
    public static InvoiceStatus LatestStatus(Invoice invoice) =>
        invoice.StatusChanges.Count == 0
            ? InvoiceStatus.Created
            : invoice.StatusChanges
                .OrderBy(s => s.Timestamp)
                .ThenBy(s => s.Id)
                .Last()
                .Status;

    /// <summary>Open: the latest Rechnungsstatus is not Bezahlt.</summary>
    public static bool IsOpen(Invoice invoice) => LatestStatus(invoice) != InvoiceStatus.Paid;

    /// <summary>
    /// The dashboard's Rechnungen: how many are open and what their full totals sum to. Partial
    /// payments are not subtracted, since they are not tracked against a Rechnung.
    /// </summary>
    public static InvoiceSummary Summarize(IEnumerable<Invoice> invoices) =>
        invoices
            .Where(IsOpen)
            .Select(i => i.Lines.Sum(l => l.Amount))
            .Aggregate(
                new InvoiceSummary(0, 0m),
                (summary, total) => summary with
                {
                    OpenCount = summary.OpenCount + 1,
                    OpenAmount = summary.OpenAmount + total,
                }
            );
}
