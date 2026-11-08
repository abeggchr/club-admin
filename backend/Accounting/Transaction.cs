namespace backend.Accounting;

/// <summary>
/// Bewegung: one money movement on the bank account. Has a date and a signed amount (Einzahlung positive,
/// Auszahlung negative; the direction is derived from the sign) and is split into Buchungen.
/// It may have zero Buchungen, which means it is not booked yet ("Keine Buchung"); once it has at least one,
/// they must sum to its amount. The database enforces neither (see ADR 0003).
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
    /// <summary>
    /// The bank's account server reference (AcctSvcrRef) of an imported booking; set only for imported
    /// Bewegungen, unique. It is how a re-import recognizes a transaction that is already stored.
    /// </summary>
    public string? BankReference { get; set; }
    public List<Entry> Entries { get; set; } = [];
}
