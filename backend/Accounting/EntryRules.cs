namespace backend.Accounting;

/// <summary>Pure validation rules for a single Entry. Return a German error message, or null when valid.</summary>
public static class EntryRules
{
    /// <summary>Everything about an Entry except its date, which belongs to its Transaction.</summary>
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
        // An edited Entry keeps resolving its Account even if that Account was deactivated since.
        if (!account.Active && accountNumber != existingAccountNumber)
        {
            return $"Das Konto {account.Number} {account.Name} ist inaktiv.";
        }
        return TransactionRules.ValidateAmount(amount);
    }
}
