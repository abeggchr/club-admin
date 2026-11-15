namespace backend.Accounting;

/// <summary>The club's settings, stored as a single record: the title, the Anfangssaldo, its start date and the club's IBAN. The Anfangssaldo, its start date and the IBAN are required; the title is optional.</summary>
public class Settings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    /// <summary>The club's display title, shown in the header. Optional, ≤ 70 characters.</summary>
    public string Title { get; set; } = "";
    public decimal OpeningBalance { get; set; }
    public DateOnly StartDate { get; set; }
    public string Iban { get; set; } = "";

    /// <summary>The club's address as the QR-bill creditor. Name and Address (one line, e.g. "Sportweg 12") ≤ 70, PostalCode ≤ 16, City ≤ 35.</summary>
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string City { get; set; } = "";
}
