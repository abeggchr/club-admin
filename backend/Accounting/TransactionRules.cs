namespace backend.Accounting;

/// <summary>A Buchung as submitted with a Bewegung. <paramref name="ExistingAccountNumber"/> is the Konto an already stored Buchung has.</summary>
public record EntryDraft(string? Text, int AccountNumber, decimal Amount, int? ExistingAccountNumber = null);

/// <summary>Pure validation rules for Bewegungen (creating and editing). Return a German error message, or null when valid.</summary>
public static class TransactionRules
{
    public static string? Validate(
        Settings? opening,
        ChartOfAccounts chart,
        DateOnly date,
        decimal amount,
        IReadOnlyList<EntryDraft> entries
    )
    {
        if (ValidateDate(opening, date) is { } dateError)
        {
            return dateError;
        }
        if (ValidateAmount(amount) is { } amountError)
        {
            return amountError;
        }
        foreach (var entry in entries)
        {
            if (EntryRules.ValidateLine(chart, entry.Text, entry.AccountNumber, entry.Amount, entry.ExistingAccountNumber) is { } error)
            {
                return error;
            }
        }
        // A Bewegung with zero Buchungen is not booked yet ("Keine Buchung"); the sum rule applies once Buchungen exist.
        if (entries.Count > 0)
        {
            var sum = entries.Sum(e => e.Amount);
            if (sum != amount)
            {
                return $"Die Summe der Buchungen ({sum:0.00}) stimmt nicht mit dem Betrag der Bewegung ({amount:0.00}) überein.";
            }
        }
        return null;
    }

    private static string? ValidateDate(Settings? opening, DateOnly date)
    {
        if (opening is null)
        {
            return "Bitte zuerst den Anfangssaldo erfassen.";
        }
        if (date < opening.StartDate)
        {
            return $"Das Datum liegt vor dem Anfangssaldo ({opening.StartDate:dd.MM.yyyy}).";
        }
        return null;
    }

    internal static string? ValidateAmount(decimal amount)
    {
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

    /// <summary>The Anfangssaldo date must not be later than the earliest Bewegung.</summary>
    public static string? ValidateOpeningStartDate(DateOnly startDate, DateOnly? earliestTransaction) =>
        earliestTransaction is { } earliest && startDate > earliest
            ? $"Das Startdatum darf nicht nach der frühesten Bewegung ({earliest:dd.MM.yyyy}) liegen."
            : null;
}
