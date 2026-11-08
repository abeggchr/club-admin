namespace backend.Accounting;

/// <summary>The club's settings, stored as a single record: the Anfangssaldo, its start date and the club's IBAN. All are required.</summary>
public class Settings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public decimal OpeningBalance { get; set; }
    public DateOnly StartDate { get; set; }
    public string Iban { get; set; } = "";
}
