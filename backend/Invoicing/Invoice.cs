namespace backend.Invoicing;

/// <summary>
/// Rechnung: belongs to one Person, has one or more lines and status changes. Immutable after creation;
/// no template reference, no copied person data and no stored total.
/// </summary>
public class Invoice
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public List<InvoiceLine> Lines { get; set; } = [];
    public List<InvoiceStatusChange> StatusChanges { get; set; } = [];
}

/// <summary>Rechnungsposition.</summary>
public class InvoiceLine
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public string Text { get; set; } = "";
    public decimal Amount { get; set; }
    /// <summary>Zero-based position within the Rechnung.</summary>
    public int Position { get; set; }
}

public enum InvoiceStatus
{
    Created,
}

/// <summary>One lifecycle step of a Rechnung. The Rechnungsdatum is the date of the Created row.</summary>
public class InvoiceStatusChange
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public InvoiceStatus Status { get; set; }
    /// <summary>UTC; shown in Europe/Zurich (see <see cref="InvoiceTime"/>).</summary>
    public DateTime Timestamp { get; set; }
}
