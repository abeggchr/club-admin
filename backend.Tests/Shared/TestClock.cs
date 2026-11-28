namespace backend.Tests.Shared;

/// <summary>A <see cref="TimeProvider"/> pinned to a UTC instant that a test sets and moves.</summary>
public sealed class TestClock(DateTime utcNow) : TimeProvider
{
    public DateTime Now { get; set; } = utcNow;

    /// <summary>A clock at noon UTC of a date, which is the same calendar date in Europe/Zurich.</summary>
    public static TestClock On(DateOnly date) => new(date.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc));

    public override DateTimeOffset GetUtcNow() => new(Now);
}
