namespace backend.Invoicing;

/// <summary>
/// Invoice: belongs to one Person, has one or more lines and status changes. Immutable after creation;
/// no template reference, no copied person data and no stored total.
/// </summary>
public class Invoice
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    /// <summary>Swiss QR-bill Creditor Reference (RF8008), stored at creation; '' for Invoices created before it was stored.</summary>
    public string Reference { get; set; } = "";
    /// <summary>The id of the Transaction that first covered the total; plain column, no foreign key (set by the import or when a Transaction is saved as paying it).</summary>
    public int? PaidBy { get; set; }
    public List<InvoiceLine> Lines { get; set; } = [];
    public List<InvoiceStatusChange> StatusChanges { get; set; } = [];
}

/// <summary>Invoice Line.</summary>
public class InvoiceLine
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public string Text { get; set; } = "";
    public decimal Amount { get; set; }
    /// <summary>The Chart of Accounts Account the amount will be booked to; a booking hint, not an Entry (ADR 0004).</summary>
    public int AccountNumber { get; set; }
    /// <summary>Zero-based position within the Invoice.</summary>
    public int Position { get; set; }
}

public enum InvoiceStatus
{
    Created,
    /// <summary>Paid: terminal, recorded when a Transaction's signed amount covers the total; the paying Transaction's id is in <see cref="Invoice.PaidBy"/>.</summary>
    Paid,
    /// <summary>Cancelled (German: storniert): terminal, set by hand on an Invoice that is not paid; the Invoice is kept.</summary>
    Cancelled,
}

/// <summary>One lifecycle step of an Invoice. The Invoice Date is the date of the Created row.</summary>
public class InvoiceStatusChange
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public InvoiceStatus Status { get; set; }
    /// <summary>UTC; shown in Europe/Zurich (see <see cref="InvoiceTime"/>).</summary>
    public DateTime Timestamp { get; set; }
    /// <summary>Remark (German: Bemerkung): optional plain-text comment on the status change.</summary>
    public string? Remark { get; set; }
}
