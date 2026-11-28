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

public static class ReceiptStorageExtensions
{
    /// <summary>Deletes the stored PDFs, ignoring failures: an object that stays behind is an unreferenced orphan, which is harmless and accepted.</summary>
    public static async Task DeleteBestEffortAsync(this IReceiptStorage storage, IEnumerable<int> ids)
    {
        foreach (var id in ids)
        {
            try
            {
                await storage.DeleteAsync(id);
            }
            catch
            {
                // See above.
            }
        }
    }
}
