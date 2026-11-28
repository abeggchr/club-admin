using backend.Invoicing;

namespace backend.Tests.Invoicing;

public class InvoiceStatusRulesTests
{
    private static readonly DateTime CreatedAt = new(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = new(2026, 6, 20, 9, 30, 0, DateTimeKind.Utc);

    private static Invoice Created() =>
        new() { StatusChanges = [new InvoiceStatusChange { Id = 1, Status = InvoiceStatus.Created, Timestamp = CreatedAt }] };

    private static Invoice Sent()
    {
        var invoice = Created();
        InvoiceStatusRules.Send(invoice, "per Email", CreatedAt.AddDays(1));
        return invoice;
    }

    private static Invoice Paid()
    {
        var invoice = Created();
        InvoiceStatusRules.MarkPaid(invoice, 7, new DateOnly(2026, 6, 5));
        return invoice;
    }

    private static Invoice Cancelled()
    {
        var invoice = Created();
        InvoiceStatusRules.Cancel(invoice, CreatedAt.AddDays(1));
        return invoice;
    }

    private static Invoice Of(string status) =>
        status switch
        {
            "Created" => Created(),
            "Sent" => Sent(),
            "Paid" => Paid(),
            _ => Cancelled(),
        };

    [Fact]
    public void An_invoice_without_status_changes_is_open_as_Created()
    {
        var invoice = new Invoice();

        Assert.Equal(InvoiceStatus.Created, InvoiceStatusRules.Current(invoice));
        Assert.True(InvoiceStatusRules.IsOpen(invoice));
    }

    [Theory]
    [InlineData("Created", InvoiceStatus.Created, true)]
    [InlineData("Sent", InvoiceStatus.Sent, true)]
    [InlineData("Paid", InvoiceStatus.Paid, false)]
    [InlineData("Cancelled", InvoiceStatus.Cancelled, false)]
    public void Only_a_Created_or_Sent_invoice_is_open(string status, InvoiceStatus current, bool open)
    {
        var invoice = Of(status);

        Assert.Equal(current, InvoiceStatusRules.Current(invoice));
        Assert.Equal(open, InvoiceStatusRules.IsOpen(invoice));
    }

    [Fact]
    public void A_paid_invoice_is_not_open() => Assert.False(InvoiceStatusRules.IsOpen(Paid()));

    [Fact]
    public void Sending_appends_a_Sent_change_with_its_remark_and_timestamp()
    {
        var invoice = Created();

        Assert.Null(InvoiceStatusRules.Send(invoice, "per Post (123)", Later));

        var sent = invoice.StatusChanges.Last();
        Assert.Equal(InvoiceStatus.Sent, sent.Status);
        Assert.Equal("per Post (123)", sent.Remark);
        Assert.Equal(Later, sent.Timestamp);
        Assert.Equal(InvoiceStatus.Sent, InvoiceStatusRules.Current(invoice));
    }

    [Fact]
    public void A_Sent_invoice_can_be_sent_again()
    {
        var invoice = Sent();

        Assert.Null(InvoiceStatusRules.ValidateSend(invoice));
        Assert.Null(InvoiceStatusRules.Send(invoice, "per Email", Later));
        Assert.Equal(2, invoice.StatusChanges.Count(s => s.Status == InvoiceStatus.Sent));
    }

    [Theory]
    [InlineData("Paid")]
    [InlineData("Cancelled")]
    public void A_paid_or_cancelled_invoice_cannot_be_sent(string status)
    {
        var invoice = Of(status);
        var rows = invoice.StatusChanges.Count;

        Assert.Equal("Nur offene Rechnungen können versendet werden.", InvoiceStatusRules.ValidateSend(invoice));
        Assert.Equal("Nur offene Rechnungen können versendet werden.", InvoiceStatusRules.Send(invoice, "per Email", Later));
        Assert.Equal(rows, invoice.StatusChanges.Count);
    }

    [Theory]
    [InlineData("Created")]
    [InlineData("Sent")]
    public void A_Created_or_Sent_invoice_can_be_cancelled(string status)
    {
        var invoice = Of(status);

        Assert.Null(InvoiceStatusRules.Cancel(invoice, Later));

        Assert.Equal(InvoiceStatus.Cancelled, InvoiceStatusRules.Current(invoice));
        Assert.Equal(Later, invoice.StatusChanges.Last().Timestamp);
    }

    [Theory]
    [InlineData("Paid")]
    [InlineData("Cancelled")]
    public void A_paid_or_cancelled_invoice_cannot_be_cancelled(string status)
    {
        var invoice = Of(status);
        var rows = invoice.StatusChanges.Count;

        Assert.Equal("Nur offene Rechnungen können storniert werden.", InvoiceStatusRules.Cancel(invoice, Later));
        Assert.Equal(rows, invoice.StatusChanges.Count);
    }

    [Theory]
    [InlineData("Created")]
    [InlineData("Sent")]
    public void Paying_records_Paid_and_the_paying_Transaction(string status)
    {
        var invoice = Of(status);

        Assert.Null(InvoiceStatusRules.MarkPaid(invoice, 42, new DateOnly(2026, 6, 10)));

        Assert.Equal(InvoiceStatus.Paid, InvoiceStatusRules.Current(invoice));
        Assert.Equal(42, invoice.PaidBy);
    }

    [Fact]
    public void The_Paid_timestamp_is_the_transaction_date_in_Zurich()
    {
        var invoice = Created();

        InvoiceStatusRules.MarkPaid(invoice, 42, new DateOnly(2026, 6, 10));

        Assert.Equal(new DateTime(2026, 6, 9, 22, 0, 0, DateTimeKind.Utc), invoice.StatusChanges.Last().Timestamp);
    }

    [Fact]
    public void A_payment_dated_before_the_last_send_keeps_the_transaction_date()
    {
        var invoice = Created();
        InvoiceStatusRules.Send(invoice, "per Email", Later);

        InvoiceStatusRules.MarkPaid(invoice, 42, new DateOnly(2026, 6, 10));

        var paid = invoice.StatusChanges.Single(s => s.Status == InvoiceStatus.Paid);
        Assert.Equal(InvoiceTime.TimestampFor(new DateOnly(2026, 6, 10)), paid.Timestamp);
        Assert.True(paid.Timestamp < Later);
        Assert.Equal(InvoiceStatus.Paid, InvoiceStatusRules.Current(invoice));
    }

    [Fact]
    public void Paying_sets_PaidBy_once()
    {
        var invoice = Created();
        InvoiceStatusRules.MarkPaid(invoice, 42, new DateOnly(2026, 6, 10));

        Assert.Equal("Die Rechnung ist nicht offen.", InvoiceStatusRules.MarkPaid(invoice, 43, new DateOnly(2026, 6, 11)));

        Assert.Equal(42, invoice.PaidBy);
        Assert.Single(invoice.StatusChanges, s => s.Status == InvoiceStatus.Paid);
    }

    [Fact]
    public void A_cancelled_invoice_cannot_be_paid()
    {
        var invoice = Cancelled();

        Assert.Equal("Die Rechnung ist nicht offen.", InvoiceStatusRules.MarkPaid(invoice, 42, new DateOnly(2026, 6, 10)));

        Assert.Null(invoice.PaidBy);
        Assert.Equal(InvoiceStatus.Cancelled, InvoiceStatusRules.Current(invoice));
    }

    [Fact]
    public void A_back_dated_terminal_change_beats_a_later_one()
    {
        var invoice = new Invoice
        {
            StatusChanges =
            [
                new InvoiceStatusChange { Id = 1, Status = InvoiceStatus.Created, Timestamp = CreatedAt },
                new InvoiceStatusChange { Id = 2, Status = InvoiceStatus.Cancelled, Timestamp = CreatedAt.AddDays(4) },
                new InvoiceStatusChange { Id = 3, Status = InvoiceStatus.Sent, Timestamp = Later },
            ],
        };

        Assert.Equal(InvoiceStatus.Cancelled, InvoiceStatusRules.Current(invoice));
        Assert.False(InvoiceStatusRules.IsOpen(invoice));
    }

    [Fact]
    public void Without_a_terminal_change_the_newest_by_timestamp_then_id_decides()
    {
        var invoice = new Invoice
        {
            StatusChanges =
            [
                new InvoiceStatusChange { Id = 3, Status = InvoiceStatus.Sent, Timestamp = Later },
                new InvoiceStatusChange { Id = 2, Status = InvoiceStatus.Created, Timestamp = Later },
                new InvoiceStatusChange { Id = 1, Status = InvoiceStatus.Created, Timestamp = CreatedAt },
            ],
        };

        Assert.Equal(InvoiceStatus.Sent, InvoiceStatusRules.Current(invoice));
    }
}
