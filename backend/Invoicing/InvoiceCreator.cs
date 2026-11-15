using backend.Settings;

namespace backend.Invoicing;

public record InvoiceCreationResult(Invoice? Invoice, string? Error);

/// <summary>
/// Creates an Invoice in one DB transaction: insert Invoice, lines and Created status change, obtain the ID,
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
        QrBillCreditor creditor,
        bool openAmount = false,
        CancellationToken ct = default
    )
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var now = DateTime.UtcNow;
            var debtor = QrBillSlip.DebtorFor(
                person.FirstName,
                person.LastName,
                person.Street,
                person.Number,
                person.PostalCode,
                person.City,
                person.Country
            );
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
                                AccountNumber = l.AccountNumber,
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

            // The Creditor Reference derives from the Invoice Number, so it is known as soon as the ID
            // is. It is persisted in its own save, before the PDF rendering: a rendering failure (e.g.
            // missing native graphics in a headless environment) must never leave an Invoice without its
            // reference.
            var bill = QrBillSlip.Build(
                creditor,
                InvoiceRules.FormatNumber(invoice.Id),
                openAmount ? null : invoice.Lines.Sum(l => l.Amount),
                debtor
            );
            invoice.Reference = bill.Reference;
            // Persist the Creditor Reference in its own save, before the PDF rendering: the reference is
            // known as soon as the Invoice Number is, and a rendering failure (e.g. missing native
            // graphics in a headless environment) must never leave an Invoice without its reference.
            await db.SaveChangesAsync(ct);

            var addressLines = InvoiceDocument.AddressLines(
                person.Street,
                person.Number,
                person.PostalCode,
                person.City,
                person.Country
            );
            var date = InvoiceTime.ZurichDate(now);
            var number = InvoiceRules.FormatNumber(invoice.Id);
            var pdf = InvoiceDocument.Render(
                new InvoiceDocumentData(
                    number,
                    date,
                    person.FullName,
                    addressLines,
                    template.Subject,
                    template.Text,
                    template.Greetings,
                    invoice
                        .Lines.Select(l => new InvoiceLineDraft(l.Text, l.Amount, l.AccountNumber))
                        .ToList(),
                    creditor,
                    debtor,
                    openAmount
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
