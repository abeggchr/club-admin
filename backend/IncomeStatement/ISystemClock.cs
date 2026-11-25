namespace backend.IncomeStatement;

/// <summary>The calendar date of "today", read from outside the calculator and the controller.</summary>
public interface ISystemClock
{
    DateOnly Today();
}

/// <summary>Today's calendar date in Europe/Zurich, derived from the current UTC instant.</summary>
public sealed class SystemClock : ISystemClock
{
    public DateOnly Today() =>
        DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(
                DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich")
            )
        );
}

/// <summary>A fixed clock for endpoint tests, so the period end is deterministic.</summary>
public sealed class FixedClock(DateOnly today) : ISystemClock
{
    public DateOnly Today() => today;
}
