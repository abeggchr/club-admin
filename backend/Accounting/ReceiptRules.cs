namespace backend.Accounting;

/// <summary>Pure validation rules for uploading a Receipt. Return a German error message, or null when valid.</summary>
public static class ReceiptRules
{
    public const int MaxBytes = 10 * 1024 * 1024;
    public const int MaxPerTransaction = 20;

    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();

    /// <summary>
    /// <paramref name="existingCount"/> is the number of Receipts the Transaction already has. The content is
    /// checked for the PDF magic bytes, not only the content type, which a client can claim freely.
    /// </summary>
    public static string? Validate(byte[] content, int existingCount)
    {
        if (content.Length == 0)
        {
            return "Die Datei ist leer.";
        }
        if (content.Length > MaxBytes)
        {
            return "Die Datei ist grösser als 10 MB.";
        }
        if (!content.AsSpan().StartsWith(PdfMagic))
        {
            return "Die Datei ist kein PDF.";
        }
        if (existingCount >= MaxPerTransaction)
        {
            return "Eine Bewegung kann höchstens 20 Belege haben.";
        }
        return null;
    }
}
