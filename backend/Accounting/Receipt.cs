namespace backend.Accounting;

/// <summary>
/// Receipt (German: Beleg): a PDF attached to a Transaction, e.g. a supplier's receipt or a payment confirmation.
/// The file lives in object storage under the Receipt's Id (see <see cref="IReceiptStorage"/>); the row only
/// records which Transaction it belongs to and when it was uploaded.
/// </summary>
public class Receipt
{
    public int Id { get; set; }
    public int TransactionId { get; set; }
    public DateTime UploadedAt { get; set; }
}
