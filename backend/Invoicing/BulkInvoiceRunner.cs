namespace backend.Invoicing;

public record BulkInvoiceSummary(int Created, IReadOnlyList<string> Failed, IReadOnlyList<string> Warnings);

/// <summary>
/// Creates the planned Invoices one by one; a failure is reported and never stops the run.
/// Payers are created Z to A, so the newest-first invoice list reads A to Z.
/// </summary>
public static class BulkInvoiceRunner
{
    public static async Task<BulkInvoiceSummary> RunAsync(
        BulkInvoicePlan plan,
        Func<PlannedInvoice, CancellationToken, Task<InvoiceCreationResult>> create,
        CancellationToken ct = default
    )
    {
        var created = 0;
        var failed = new List<string>();
        var ordered = plan
            .Invoices.OrderByDescending(i => i.Recipient.LastName, StringComparer.CurrentCultureIgnoreCase)
            .ThenByDescending(i => i.Recipient.FirstName, StringComparer.CurrentCultureIgnoreCase);
        foreach (var invoice in ordered)
        {
            var who = invoice.Recipient.FullName;
            try
            {
                var result = await create(invoice, ct);
                if (result.Invoice is not null) created++;
                else failed.Add($"{who}: {result.Error ?? "Die Rechnung konnte nicht erstellt werden."}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                failed.Add($"{who}: Die Rechnung konnte nicht erstellt werden.");
            }
        }
        return new BulkInvoiceSummary(created, failed, plan.Warnings);
    }
}
