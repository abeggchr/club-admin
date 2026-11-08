namespace backend.Accounting;

/// <summary>
/// Buchung: one line of a Bewegung. The amount is signed from the bank's point of view
/// (money in positive, money out negative). Revenue vs. expense is derived from the Konto's Kategorie.
/// A Buchung has no date of its own; it takes the date of its Bewegung. Creation order is the Id order.
/// </summary>
public class Entry
{
    public int Id { get; set; }
    public int TransactionId { get; set; }
    public Transaction Transaction { get; set; } = null!;
    public string Text { get; set; } = "";
    public int AccountNumber { get; set; }
    public decimal Amount { get; set; }

    /// <summary>The date of the Bewegung. Needs <see cref="Transaction"/> to be loaded.</summary>
    public DateOnly Date => Transaction.Date;
}
