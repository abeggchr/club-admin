namespace backend.Invoicing;

/// <summary>Stores the rendered Invoice PDFs, addressed by Invoice Number.</summary>
public interface IInvoiceStorage
{
    Task PutAsync(int number, byte[] pdf, CancellationToken ct = default);

    /// <summary>Returns the stored PDF, or null when none exists for the number.</summary>
    Task<byte[]?> GetAsync(int number, CancellationToken ct = default);
}

public static class InvoiceStorageKey
{
    public static string For(int number) => $"invoices/{InvoiceRules.FormatNumber(number)}.pdf";
}
