using Microsoft.EntityFrameworkCore;
using PersonEntity = backend.Person.Person;

namespace backend.Invoicing;

public record InvoicePersonDto(int Id, string Name);

public record InvoiceListItemDto(
    int Id,
    string Number,
    DateOnly Date,
    InvoicePersonDto Person,
    decimal Total,
    string Status
);

public record InvoiceLineDto(string Text, decimal Amount, int AccountNumber);

public record InvoiceStatusChangeDto(string Status, DateTime Timestamp);

public record InvoicePersonDetailDto(
    int Id,
    string Name,
    string? Street,
    string? Number,
    string? PostalCode,
    string? City,
    string? Country
);

public record InvoiceDetailDto(
    int Id,
    string Number,
    DateOnly Date,
    InvoicePersonDetailDto Person,
    List<InvoiceLineDto> Lines,
    decimal Total,
    string Status,
    string Reference,
    List<InvoiceStatusChangeDto> StatusHistory
);

public static class InvoiceQueries
{
    /// <summary>Read-only query including everything the list and detail views need.</summary>
    public static IQueryable<Invoice> WithDetails(this IQueryable<Invoice> invoices) =>
        invoices.AsNoTracking().Include(i => i.Lines).Include(i => i.StatusChanges);
}

/// <summary>Maps a Rechnung to its API shapes. Dates and times are shown in Europe/Zurich.</summary>
public static class InvoiceMapping
{
    public static string DisplayName(PersonEntity p) => $"{p.LastName} {p.FirstName}";

    private static IOrderedEnumerable<InvoiceStatusChange> InOrder(Invoice i) =>
        i.StatusChanges.OrderBy(s => s.Timestamp).ThenBy(s => s.Id);

    private static InvoiceStatusChange CurrentStatus(Invoice i) => InOrder(i).Last();

    private static DateOnly InvoiceDate(Invoice i) =>
        InvoiceTime.ZurichDate(
            i.StatusChanges.First(s => s.Status == InvoiceStatus.Created).Timestamp
        );

    public static InvoiceListItemDto ToInvoiceListItem(Invoice i, PersonEntity p) =>
        new(
            i.Id,
            InvoiceRules.FormatNumber(i.Id),
            InvoiceDate(i),
            new InvoicePersonDto(p.Id, DisplayName(p)),
            i.Lines.Sum(l => l.Amount),
            CurrentStatus(i).Status.ToString()
        );

    public static InvoiceDetailDto ToInvoiceDetail(Invoice i, PersonEntity p) =>
        new(
            i.Id,
            InvoiceRules.FormatNumber(i.Id),
            InvoiceDate(i),
            new InvoicePersonDetailDto(
                p.Id,
                DisplayName(p),
                p.Street,
                p.Number,
                p.PostalCode,
                p.City,
                p.Country
            ),
            i.Lines.OrderBy(l => l.Position)
                .Select(l => new InvoiceLineDto(l.Text, l.Amount, l.AccountNumber))
                .ToList(),
            i.Lines.Sum(l => l.Amount),
            CurrentStatus(i).Status.ToString(),
            i.Reference,
            InOrder(i)
                .Select(s => new InvoiceStatusChangeDto(
                    s.Status.ToString(),
                    InvoiceTime.ToZurich(s.Timestamp)
                ))
                .ToList()
        );
}
