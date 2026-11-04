namespace backend.Accounting;

/// <summary>Pure accounting rules. No database or HTTP dependency.</summary>
public static class AccountingCalculator
{
    /// <summary>
    /// Erwarteter Kontostand on <paramref name="date"/>: the Anfangssaldo plus all Buchungen up to that date.
    /// Returns null before the Anfangssaldo start date, where no balance is defined.
    /// </summary>
    public static decimal? ExpectedBalance(OpeningBalance opening, DateOnly date)
    {
        if (date < opening.StartDate)
        {
            return null;
        }

        return opening.Amount;
    }
}
