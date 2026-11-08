using backend.Invoicing;

namespace backend.Tests.Invoicing;

public class InvoiceTimeTests
{
    [Fact]
    public void Winter_time_is_one_hour_ahead_of_UTC()
    {
        var utc = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 1, 15, 11, 30, 0), InvoiceTime.ToZurich(utc));
    }

    [Fact]
    public void Summer_time_is_two_hours_ahead_of_UTC()
    {
        var utc = new DateTime(2026, 7, 15, 10, 30, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 7, 15, 12, 30, 0), InvoiceTime.ToZurich(utc));
    }

    [Fact]
    public void The_Rechnungsdatum_is_the_next_day_in_Zurich_shortly_before_midnight_UTC()
    {
        var utc = new DateTime(2026, 7, 15, 22, 30, 0, DateTimeKind.Utc);

        Assert.Equal(new DateOnly(2026, 7, 16), InvoiceTime.ZurichDate(utc));
    }

    [Fact]
    public void The_Rechnungsdatum_stays_the_same_day_shortly_after_midnight_UTC_in_winter()
    {
        var utc = new DateTime(2026, 1, 15, 0, 30, 0, DateTimeKind.Utc);

        Assert.Equal(new DateOnly(2026, 1, 15), InvoiceTime.ZurichDate(utc));
    }

    [Fact]
    public void A_timestamp_that_is_not_marked_UTC_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => InvoiceTime.ToZurich(new DateTime(2026, 1, 15, 10, 0, 0)));
    }
}
