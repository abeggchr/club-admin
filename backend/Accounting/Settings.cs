namespace backend.Accounting;

/// <summary>Anfangssaldo: the bank balance from which the Erwarteter Kontostand is counted. Stored as a single record.</summary>
public class Settings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public decimal OpeningBalance { get; set; }
    public DateOnly StartDate { get; set; }
}
