namespace backend.Accounting;

/// <summary>
/// Buchung: one line in the Buchungsjournal. The amount is signed from the bank's point of view
/// (money in positive, money out negative). Revenue vs. expense is derived from the Konto's Kategorie.
/// Creation order is the Id order.
/// </summary>
public class Entry
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string Text { get; set; } = "";
    public int AccountNumber { get; set; }
    public decimal Amount { get; set; }
}
