namespace backend.Accounting;

/// <summary>
/// Bewegung: one money movement on the bank account. Has a date and a signed amount (Einzahlung positive,
/// Auszahlung negative; the direction is derived from the sign) and is split into one or more Buchungen.
/// The database does not enforce that it has Buchungen or that they sum to its amount (see ADR 0003).
/// Creation order is the Id order.
/// </summary>
public class Transaction
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    /// <summary>Optional free text describing this Bewegung.</summary>
    public string? Text { get; set; }
    /// <summary>The optional Person this Bewegung is linked to.</summary>
    public int? PersonId { get; set; }
    public List<Entry> Entries { get; set; } = [];
}
