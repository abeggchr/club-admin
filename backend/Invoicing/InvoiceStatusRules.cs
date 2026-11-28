namespace backend.Invoicing;

/// <summary>
/// Pure rules for an Invoice's status: the current Invoice Status and the transitions Sent, Cancelled
/// and Paid. The only code that appends a status change after creation or sets PaidBy. Operations
/// return a German error, or null once applied to the Invoice (the caller saves).
/// </summary>
public static class InvoiceStatusRules
{
    /// <summary>The current Invoice Status, or Created for an Invoice without any status change yet.</summary>
    public static InvoiceStatus Current(Invoice invoice)
    {
        // A Paid or Cancelled change wins whatever the timestamps: Paid carries the Transaction's date
        // and can predate a later Sent.
        var ordered = invoice.StatusChanges.OrderBy(s => s.Timestamp).ThenBy(s => s.Id).ToList();
        var current =
            ordered.LastOrDefault(s => s.Status is InvoiceStatus.Paid or InvoiceStatus.Cancelled)
            ?? ordered.LastOrDefault();
        return current?.Status ?? InvoiceStatus.Created;
    }

    /// <summary>Open: the current Invoice Status is Created or Sent, neither Paid nor Cancelled.</summary>
    public static bool IsOpen(Invoice invoice) => Current(invoice) is InvoiceStatus.Created or InvoiceStatus.Sent;

    /// <summary>Sending needs an open Invoice; checked before the Delivery Channel is called.</summary>
    public static string? ValidateSend(Invoice invoice) =>
        IsOpen(invoice) ? null : "Nur offene Rechnungen können versendet werden.";

    /// <summary>Records a Sent status change with the Delivery Channel's Remark; sending again adds another.</summary>
    public static string? Send(Invoice invoice, string? remark, DateTime timestamp)
    {
        if (ValidateSend(invoice) is { } error)
        {
            return error;
        }
        invoice.StatusChanges.Add(
            new InvoiceStatusChange { Status = InvoiceStatus.Sent, Timestamp = timestamp, Remark = remark }
        );
        return null;
    }

    /// <summary>Records a Cancelled status change; allowed only while the Invoice is Created or Sent.</summary>
    public static string? Cancel(Invoice invoice, DateTime timestamp)
    {
        if (!IsOpen(invoice))
        {
            return "Nur offene Rechnungen können storniert werden.";
        }
        invoice.StatusChanges.Add(new InvoiceStatusChange { Status = InvoiceStatus.Cancelled, Timestamp = timestamp });
        return null;
    }

    /// <summary>Paying needs an open Invoice; checked before the paying Transaction is saved.</summary>
    public static string? ValidatePay(Invoice invoice) => IsOpen(invoice) ? null : "Die Rechnung ist nicht offen.";

    /// <summary>
    /// Records the Paid status change, timestamped with the Transaction's date, and sets PaidBy to the
    /// Transaction. Paid is terminal, so an Invoice that is no longer open cannot be paid again.
    /// </summary>
    public static string? MarkPaid(Invoice invoice, int transactionId, DateOnly transactionDate)
    {
        if (ValidatePay(invoice) is { } error)
        {
            return error;
        }
        invoice.StatusChanges.Add(
            new InvoiceStatusChange { Status = InvoiceStatus.Paid, Timestamp = InvoiceTime.TimestampFor(transactionDate) }
        );
        invoice.PaidBy = transactionId;
        return null;
    }
}
