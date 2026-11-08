using backend.Accounting;

namespace backend.Tests.Accounting;

public class AccountUsageRulesTests
{
    [Fact]
    public void A_recorded_account_no_entry_uses_any_more_is_unused() =>
        Assert.Equal([3400], AccountUsageRules.Unused([3400, 6010], [6010, 6010]));

    [Fact]
    public void An_account_still_used_by_some_entry_is_kept() =>
        Assert.Empty(AccountUsageRules.Unused([3400, 6010], [3400, 6010, 3400]));

    [Fact]
    public void With_no_entries_every_record_is_unused() =>
        Assert.Equal([3400, 6010], AccountUsageRules.Unused([6010, 3400], []));

    [Fact]
    public void A_used_account_without_record_is_unrecorded() =>
        Assert.Equal([3500], AccountUsageRules.Unrecorded([3400], [3400, 3500, 3500]));
}
