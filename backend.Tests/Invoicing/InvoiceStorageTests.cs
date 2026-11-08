using backend.Invoicing;

namespace backend.Tests.Invoicing;

public class InvoiceStorageTests
{
    [Fact]
    public async Task A_stored_PDF_can_be_read_back_by_its_Rechnungsnummer()
    {
        IInvoiceStorage storage = new InMemoryInvoiceStorage();
        byte[] pdf = [1, 2, 3];

        await storage.PutAsync(42, pdf);

        Assert.Equal(pdf, await storage.GetAsync(42));
    }

    [Fact]
    public async Task Reading_a_missing_PDF_returns_null()
    {
        IInvoiceStorage storage = new InMemoryInvoiceStorage();

        Assert.Null(await storage.GetAsync(7));
    }

    [Theory]
    [InlineData(42, "invoices/0042.pdf")]
    [InlineData(1, "invoices/0001.pdf")]
    [InlineData(12345, "invoices/12345.pdf")]
    public void The_object_key_uses_the_number_with_at_least_four_digits(int number, string key)
    {
        Assert.Equal(key, InvoiceStorageKey.For(number));
    }
}
