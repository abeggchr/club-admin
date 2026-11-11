namespace backend.Invoicing;

public record BulkInvoiceSummary(int Created, IReadOnlyList<string> Failed, IReadOnlyList<string> Warnings);

/// <summary>Creates the planned Rechnungen one by one; a failure is reported and never stops the run.</summary>
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
        foreach (var invoice in plan.Invoices)
        {
            var who = $"{invoice.Recipient.FirstName} {invoice.Recipient.LastName}";
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
