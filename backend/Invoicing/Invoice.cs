namespace backend.Invoicing;

/// <summary>
/// Rechnung: belongs to one Person, has one or more lines and status changes. Immutable after creation;
/// no template reference, no copied person data and no stored total.
/// </summary>
public class Invoice
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    /// <summary>Swiss QR-bill Gläubigerreferenz (RF8008), stored at creation; '' for Rechnungen created before it was stored.</summary>
    public string Reference { get; set; } = "";
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
    /// <summary>The Kontoplan account the amount will be booked to; a booking hint, not a Buchung (ADR 0004).</summary>
    public int AccountNumber { get; set; }
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
