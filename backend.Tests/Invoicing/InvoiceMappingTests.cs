using backend.Invoicing;
using PersonEntity = backend.Person.Person;
using SettingsEntity = backend.Settings.Settings;

namespace backend.Tests.Invoicing;

public class InvoiceMappingTests
{
    private static PersonEntity Person => new() { Id = 1, FirstName = "Hans", LastName = "Muster" };

    private static Invoice Invoice(string reference) =>
        new()
        {
            Id = 42,
            PersonId = 1,
            Reference = reference,
            Lines = [new InvoiceLine { Text = "Jahresbeitrag", Amount = 100m, AccountNumber = 3400, Position = 0 }],
            StatusChanges =
            [
                new InvoiceStatusChange { Status = InvoiceStatus.Created, Timestamp = new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc) },
            ],
        };

    [Fact]
    public void Detail_shows_the_Gläubigerreferenz()
    {
        var detail = InvoiceMapping.ToInvoiceDetail(Invoice("RF340042"), Person, new SettingsEntity());

        Assert.Equal("RF340042", detail.Reference);
    }

    [Fact]
    public void Detail_shows_the_empty_backfilled_reference()
    {
        var detail = InvoiceMapping.ToInvoiceDetail(Invoice(""), Person, new SettingsEntity());

        Assert.Equal("", detail.Reference);
    }

    [Fact]
    public void List_and_detail_show_the_Bezahlt_status_for_a_paid_Rechnung()
    {
        // The Created change comes first; the Paid change (timestamped with the paying movement's booking
        // date) is the last, so it is the current status of the Invoice.
        var created = new DateTime(2026, 1, 8, 12, 0, 0, DateTimeKind.Utc);
        var paidAt = new DateTime(2026, 2, 9, 23, 0, 0, DateTimeKind.Utc);
        var invoice = Invoice("RF340042");
        invoice.StatusChanges[0].Timestamp = created;
        invoice.StatusChanges.Add(new InvoiceStatusChange { Status = InvoiceStatus.Paid, Timestamp = paidAt });

        var item = InvoiceMapping.ToInvoiceListItem(invoice, Person);
        var detail = InvoiceMapping.ToInvoiceDetail(invoice, Person, new SettingsEntity());

        // The current status is the last status change; both views render the same enum name the
        // frontend maps to "Bezahlt". The detail view shows the status history in Europe/Zurich, so the
        // booking date 2026-02-09 23:00 UTC is shown as 2026-02-10 00:00.
        Assert.Equal("Paid", item.Status);
        Assert.Equal("Paid", detail.Status);
        Assert.Equal(new DateTime(2026, 2, 10, 0, 0, 0), detail.StatusHistory.Last().Timestamp);
    }

    [Fact]
    public void List_and_detail_show_a_back_dated_Paid_status_over_a_later_change()
    {
        var invoice = Invoice("RF340042");
        invoice.StatusChanges.Add(new InvoiceStatusChange { Id = 2, Status = InvoiceStatus.Paid, Timestamp = new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc) });
        invoice.StatusChanges.Add(new InvoiceStatusChange { Id = 3, Status = InvoiceStatus.Created, Timestamp = new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc) });

        Assert.Equal("Paid", InvoiceMapping.ToInvoiceListItem(invoice, Person).Status);
        Assert.Equal("Paid", InvoiceMapping.ToInvoiceDetail(invoice, Person, new SettingsEntity()).Status);
    }

    [Fact]
    public void The_detail_history_shows_the_Remark_of_a_status_change()
    {
        var invoice = Invoice("RF340042");
        invoice.StatusChanges[0].Remark = "per Email";

        var detail = InvoiceMapping.ToInvoiceDetail(invoice, Person, new SettingsEntity());

        Assert.Equal("per Email", detail.StatusHistory.Single().Remark);
    }

    [Fact]
    public void The_detail_shows_the_Paid_by_movement_id()
    {
        var invoice = Invoice("RF340042");
        invoice.PaidBy = 77;

        var detail = InvoiceMapping.ToInvoiceDetail(invoice, Person, new SettingsEntity());

        Assert.Equal(77, detail.PaidBy);
    }

    [Fact]
    public void List_stays_unchanged_and_has_no_reference_or_PaidBy()
    {
        var item = InvoiceMapping.ToInvoiceListItem(Invoice("RF340042"), Person);

        Assert.Equal(42, item.Id);
        Assert.Equal("0042", item.Number);
        Assert.Equal("Created", item.Status);
        // The list view does not show the reference (spec #59: detail view only) or PaidBy
        // (ticket #61: the status is shown, the paying Transaction's id is detail-only).
        Assert.False(
            typeof(InvoiceListItemDto).GetProperties().Any(p => p.Name == "Reference"),
            "InvoiceListItemDto must not gain a Reference property in this change."
        );
        Assert.False(
            typeof(InvoiceListItemDto).GetProperties().Any(p => p.Name == "PaidBy"),
            "InvoiceListItemDto must not gain a PaidBy property in this change."
        );
    }
}
