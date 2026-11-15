namespace backend.Invoicing;

/// <summary>
/// Timestamps are stored as UTC (timestamptz) and shown in Europe/Zurich. This is the single place that converts.
/// </summary>
public static class InvoiceTime
{
    private static readonly TimeZoneInfo Zurich = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    /// <summary>Zurich wall-clock time of a UTC timestamp (Kind Unspecified, so it serialises without offset).</summary>
    public static DateTime ToZurich(DateTime utc)
    {
        if (utc.Kind != DateTimeKind.Utc) throw new ArgumentException("Expected a UTC timestamp.", nameof(utc));
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(utc, Zurich), DateTimeKind.Unspecified);
    }

    public static DateOnly ZurichDate(DateTime utc) => DateOnly.FromDateTime(ToZurich(utc));

    /// <summary>Today's calendar date in Europe/Zurich, from a UTC instant.</summary>
    public static DateOnly Today(DateTime utc) => ZurichDate(utc);

    /// <summary>
    /// The UTC instant that represents a Zurich calendar date (that date's midnight in Europe/Zurich).
    /// Used for the Paid status timestamp of a movement, whose BookgDt is a date without time.
    /// </summary>
    public static DateTime TimestampFor(DateOnly zurichDate)
    {
        var midnight = new DateTime(zurichDate.Year, zurichDate.Month, zurichDate.Day, 0, 0, 0, 0, DateTimeKind.Unspecified);
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(midnight, Zurich), DateTimeKind.Utc);
    }
}
