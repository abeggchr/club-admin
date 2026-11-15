using backend.Accounting;

namespace backend.Tests.Accounting;

public class ReceiptRulesTests
{
    private const int TenMb = 10 * 1024 * 1024;

    // A PDF of the given size: the magic bytes, padded with zeros.
    private static byte[] PdfOfSize(int size)
    {
        var bytes = new byte[size];
        "%PDF-"u8.CopyTo(bytes);
        return bytes;
    }

    [Fact]
    public void A_PDF_is_accepted() => Assert.Null(ReceiptRules.Validate("%PDF-1.7 x"u8.ToArray(), 0));

    [Fact]
    public void An_empty_file_is_rejected() =>
        Assert.Contains("leer", ReceiptRules.Validate([], 0));

    [Fact]
    public void A_file_that_is_not_a_PDF_is_rejected() =>
        Assert.Contains("PDF", ReceiptRules.Validate("GIF89a"u8.ToArray(), 0));

    [Fact]
    public void A_PDF_like_prefix_without_the_dash_is_rejected() =>
        Assert.Contains("PDF", ReceiptRules.Validate("%PDF1.4"u8.ToArray(), 0));

    [Fact]
    public void A_file_of_exactly_10_MB_is_accepted() =>
        Assert.Null(ReceiptRules.Validate(PdfOfSize(TenMb), 0));

    [Fact]
    public void A_file_over_10_MB_is_rejected() =>
        Assert.Contains("10 MB", ReceiptRules.Validate(PdfOfSize(TenMb + 1), 0));

    [Fact]
    public void The_20th_Receipt_is_accepted() =>
        Assert.Null(ReceiptRules.Validate(PdfOfSize(10), 19));

    [Fact]
    public void The_21st_Receipt_is_rejected() =>
        Assert.Contains("20", ReceiptRules.Validate(PdfOfSize(10), 20));
}
