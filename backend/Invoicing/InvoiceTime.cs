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
}
