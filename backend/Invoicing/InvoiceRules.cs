using backend.Accounting;

namespace backend.Invoicing;

/// <summary>One Invoice Line as supplied by the client.</summary>
public record InvoiceLineDraft(string? Text, decimal Amount, int AccountNumber);

/// <summary>The dashboard's Invoices: how many are open and what their full totals sum to.</summary>
public record InvoiceSummary(int OpenCount, decimal OpenAmount);

/// <summary>Pure rules for creating an Invoice. Errors are German messages, or null when valid.</summary>
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

    /// <summary>The Invoice Number: the ID with at least four digits.</summary>
    public static string FormatNumber(int id) => id.ToString("D4");

    public static decimal Total(IEnumerable<InvoiceLineDraft> lines) => lines.Sum(l => l.Amount);

    /// <summary>
    /// The status change that is current: a Paid or Cancelled one wins whatever the timestamps (Paid is
    /// timestamped with the booking date and can predate a later Sent); otherwise the newest by
    /// timestamp, then id.
    /// </summary>
    public static InvoiceStatusChange? CurrentChange(Invoice invoice)
    {
        var ordered = invoice.StatusChanges.OrderBy(s => s.Timestamp).ThenBy(s => s.Id).ToList();
        return ordered.LastOrDefault(s => s.Status is InvoiceStatus.Paid or InvoiceStatus.Cancelled)
            ?? ordered.LastOrDefault();
    }

    /// <summary>The current Invoice Status, or Created for an Invoice without any status change yet.</summary>
    public static InvoiceStatus LatestStatus(Invoice invoice) =>
        CurrentChange(invoice)?.Status ?? InvoiceStatus.Created;

    /// <summary>Open: the latest Invoice Status is neither Paid nor Cancelled.</summary>
    public static bool IsOpen(Invoice invoice) =>
        LatestStatus(invoice) is not (InvoiceStatus.Paid or InvoiceStatus.Cancelled);

    /// <summary>Cancelling is allowed while the Invoice is open (Created or Sent).</summary>
    public static string? ValidateCancel(InvoiceStatus status) =>
        status is InvoiceStatus.Created or InvoiceStatus.Sent
            ? null
            : "Nur offene Rechnungen können storniert werden.";

    /// <summary>Sending needs an open Invoice (Created or Sent); an email also needs a subject and a text, a letter does not.</summary>
    public static string? ValidateSend(InvoiceStatus status, string? subject, string? text, bool requiresMessage = true)
    {
        if (status is not (InvoiceStatus.Created or InvoiceStatus.Sent))
        {
            return "Nur offene Rechnungen können versendet werden.";
        }
        if (!requiresMessage)
        {
            return null;
        }
        if (string.IsNullOrWhiteSpace(subject))
        {
            return "Der Betreff ist erforderlich.";
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Der Text ist erforderlich.";
        }
        return null;
    }

    /// <summary>
    /// The dashboard's Invoices: how many are open and what their full totals sum to. Partial
    /// payments are not subtracted, since they are not tracked against an Invoice.
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
