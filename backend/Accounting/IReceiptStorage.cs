namespace backend.Accounting;

/// <summary>
/// Stores the Receipt PDFs, addressed by Receipt Id. Separate from the Invoice storage, because Invoice PDFs
/// are never deleted and Receipts must be.
/// </summary>
public interface IReceiptStorage
{
    Task PutAsync(int id, byte[] pdf, CancellationToken ct = default);

    /// <summary>Returns the stored PDF, or null when none exists for the id.</summary>
    Task<byte[]?> GetAsync(int id, CancellationToken ct = default);

    /// <summary>Deletes the stored PDF; deleting a missing one is not an error.</summary>
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public static class ReceiptStorageKey
{
    public static string For(int id) => $"receipts/{id}.pdf";
}
