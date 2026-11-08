namespace backend.Accounting;

/// <summary>Pure validation for the club settings (Anfangssaldo, Startdatum, IBAN). Returns a German error message, or null when valid.</summary>
public static class SettingsRules
{
    public static string? Validate(decimal openingBalance, DateOnly startDate, string? iban, DateOnly? earliestTransaction)
    {
        if (openingBalance != decimal.Round(openingBalance, 2))
        {
            return "Der Betrag darf höchstens zwei Dezimalstellen haben.";
        }
        if (startDate == default)
        {
            return "Das Startdatum ist erforderlich.";
        }
        if (Iban.Validate(iban) is { } ibanError)
        {
            return ibanError;
        }
        return TransactionRules.ValidateOpeningStartDate(startDate, earliestTransaction);
    }
}
