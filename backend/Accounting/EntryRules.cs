namespace backend.Accounting;

/// <summary>Pure validation rules for Buchungen (creating and editing). Return a German error message, or null when valid.</summary>
public static class EntryRules
{
    public static string? Validate(
        Settings? opening,
        ChartOfAccounts chart,
        DateOnly date,
        string? text,
        int accountNumber,
        decimal amount,
        int? existingAccountNumber = null
    )
    {
        if (opening is null)
        {
            return "Bitte zuerst den Anfangssaldo erfassen.";
        }
        if (date < opening.StartDate)
        {
            return $"Das Datum liegt vor dem Anfangssaldo ({opening.StartDate:dd.MM.yyyy}).";
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Der Text ist erforderlich.";
        }
        var account = chart.Find(accountNumber);
        if (account is null)
        {
            return $"Das Konto {accountNumber} existiert nicht.";
        }
        // An edited Buchung keeps resolving its Konto even if that Konto was deactivated since.
        if (!account.Active && accountNumber != existingAccountNumber)
        {
            return $"Das Konto {account.Number} {account.Name} ist inaktiv.";
        }
        if (amount != decimal.Round(amount, 2))
        {
            return "Der Betrag darf höchstens zwei Dezimalstellen haben.";
        }
        if (amount == 0m)
        {
            return "Der Betrag darf nicht 0 sein.";
        }
        return null;
    }

    /// <summary>The Anfangssaldo date must not be later than the earliest Buchung.</summary>
    public static string? ValidateOpeningStartDate(DateOnly startDate, DateOnly? earliestEntry) =>
        earliestEntry is { } earliest && startDate > earliest
            ? $"Das Startdatum darf nicht nach der frühesten Buchung ({earliest:dd.MM.yyyy}) liegen."
            : null;
}
