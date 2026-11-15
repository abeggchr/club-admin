namespace backend.Accounting;

/// <summary>Pure validation for the club settings (Anfangssaldo, Startdatum, IBAN, Klubadresse). Returns a German error message, or null when valid.</summary>
public static class SettingsRules
{
    public static string? Validate(decimal openingBalance, DateOnly startDate, string? iban, string? name, string? address, string? postalCode, string? city, DateOnly? earliestTransaction)
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
        if (Iban.IsQrIban(iban))
        {
            return "Bitte eine normale IBAN statt der QR-IBAN verwenden";
        }
        if (ValidateText("Name", name, 70) is { } nameError)
        {
            return nameError;
        }
        if (ValidateText("Adresse", address, 70) is { } addressError)
        {
            return addressError;
        }
        if (ValidateText("PLZ", postalCode, 16) is { } postalCodeError)
        {
            return postalCodeError;
        }
        if (ValidateText("Ort", city, 35) is { } cityError)
        {
            return cityError;
        }
        return TransactionRules.ValidateOpeningStartDate(startDate, earliestTransaction);
    }

    static string? ValidateText(string label, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return $"{label} ist erforderlich.";
        }
        return value.Trim().Length > maxLength ? $"{label} darf höchstens {maxLength} Zeichen lang sein." : null;
    }
}
