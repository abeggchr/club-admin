namespace backend.Settings;

/// <summary>The club's settings, stored as a single record: the Name, the Opening Balance, its start date, the IBAN and the Club Address. All are required.</summary>
public class Settings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public decimal OpeningBalance { get; set; }
    public DateOnly StartDate { get; set; }
    public string Iban { get; set; } = "";

    /// <summary>The club's address as the QR-bill creditor. Name and Address (one line, e.g. "Sportweg 12") ≤ 70, PostalCode ≤ 16, City ≤ 35.</summary>
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string City { get; set; } = "";

    /// <summary>The optional SMTP group (email channel): all null, or Host, Port, Security and FromAddress set. The password is stored in plain text and never returned (ADR 0007).</summary>
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; }
    public SmtpSecurity? SmtpSecurity { get; set; }
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public string? SmtpFromAddress { get; set; }
    public string? SmtpFromName { get; set; }

    /// <summary>The optional ePost API key (post channel). Stored in plain text and never returned, like the SMTP password (ADR 0007).</summary>
    public string? EPostApiKey { get; set; }
}
