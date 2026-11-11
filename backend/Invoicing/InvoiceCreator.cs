namespace backend.Invoicing;

public record InvoiceCreationResult(Invoice? Invoice, string? Error);

/// <summary>
/// Creates a Rechnung in one DB transaction: insert Invoice, lines and Created status change, obtain the ID,
/// render the PDF, upload it, commit. Any failure rolls back; the lost ID leaves an accepted gap in the numbering.
/// </summary>
public class InvoiceCreator(
    AppDbContext db,
    IInvoiceStorage storage,
    ILogger<InvoiceCreator> logger
)
{
    public async Task<InvoiceCreationResult> CreateAsync(
        backend.Person.Person person,
        TextTemplate template,
        IReadOnlyList<InvoiceLineDraft> lines,
        CancellationToken ct = default
    )
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var now = DateTime.UtcNow;
            var invoice = new Invoice
            {
                PersonId = person.Id,
                Lines = lines
                    .Select(
                        (l, i) =>
                            new InvoiceLine
                            {
                                Text = l.Text!.Trim(),
                                Amount = l.Amount,
                                Position = i,
                            }
                    )
                    .ToList(),
                StatusChanges =
                [
                    new InvoiceStatusChange { Status = InvoiceStatus.Created, Timestamp = now },
                ],
            };
            db.Invoices.Add(invoice);
            await db.SaveChangesAsync(ct);

            var pdf = InvoiceDocument.Render(
                new InvoiceDocumentData(
                    InvoiceRules.FormatNumber(invoice.Id),
                    InvoiceTime.ZurichDate(now),
                    $"{person.FirstName} {person.LastName}",
                    InvoiceDocument.AddressLines(
                        person.Street,
                        person.Number,
                        person.PostalCode,
                        person.City,
                        person.Country
                    ),
                    template.Subject,
                    template.Text,
                    template.Greetings,
                    invoice.Lines.Select(l => new InvoiceLineDraft(l.Text, l.Amount)).ToList()
                )
            );
            await storage.PutAsync(invoice.Id, pdf, ct);
            await tx.CommitAsync(ct);
            return new InvoiceCreationResult(invoice, null);
        }
        catch (OperationCanceledException)
        {
            db.ChangeTracker.Clear();
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Creating a Rechnung failed");
            db.ChangeTracker.Clear();
            return new InvoiceCreationResult(null, "Die Rechnung konnte nicht erstellt werden.");
        }
    }
}
