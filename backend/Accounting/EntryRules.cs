namespace backend.Accounting;

/// <summary>Pure validation rules for a single Buchung. Return a German error message, or null when valid.</summary>
public static class EntryRules
{
    /// <summary>Everything about a Buchung except its date, which belongs to its Bewegung.</summary>
    public static string? ValidateLine(
        ChartOfAccounts chart,
        string? text,
        int accountNumber,
        decimal amount,
        int? existingAccountNumber = null
    )
    {
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
        return TransactionRules.ValidateAmount(amount);
    }

    /// <summary>A Buchung on its own (the temporary one-Buchung-per-Bewegung write path).</summary>
    public static string? Validate(
        Settings? opening,
        ChartOfAccounts chart,
        DateOnly date,
        string? text,
        int accountNumber,
        decimal amount,
        int? existingAccountNumber = null
    ) =>
        TransactionRules.ValidateDate(opening, date)
        ?? ValidateLine(chart, text, accountNumber, amount, existingAccountNumber);
}
