using backend.Accounting;

namespace backend.Tests.Accounting;

public class ExpectedBalanceTests
{
    private static readonly OpeningBalance Opening = new() { Amount = 1234.50m, StartDate = new DateOnly(2026, 1, 1) };

    [Fact]
    public void On_the_opening_date_it_is_the_opening_amount()
    {
        Assert.Equal(1234.50m, AccountingCalculator.ExpectedBalance(Opening, new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void After_the_opening_date_without_entries_it_is_still_the_opening_amount()
    {
        Assert.Equal(1234.50m, AccountingCalculator.ExpectedBalance(Opening, new DateOnly(2026, 6, 30)));
    }

    [Fact]
    public void Before_the_opening_date_it_is_undefined()
    {
        Assert.Null(AccountingCalculator.ExpectedBalance(Opening, new DateOnly(2025, 12, 31)));
    }
}
