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

    /// <summary>The club's email address: the From of SMTP mails and the Reply-To of ePost mails (ADR 0008). Required once an email backend is set up.</summary>
    public string? FromAddress { get; set; }

    /// <summary>The optional SMTP group (email channel): all null, or Host, Port and Security set. The password is stored in plain text and never returned (ADR 0007).</summary>
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; }
    public SmtpSecurity? SmtpSecurity { get; set; }
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }

    /// <summary>The optional ePost API key (post channel, and email channel when no SMTP group is set). Stored in plain text and never returned, like the SMTP password (ADR 0007).</summary>
    public string? EPostApiKey { get; set; }
}
