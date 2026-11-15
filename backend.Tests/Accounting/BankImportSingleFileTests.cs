using System.Text;
using backend.Accounting;

namespace backend.Tests.Accounting;

public class BankImportSingleFileTests
{
    private const string ClubIban = "CH9300762011623852957";

    private static readonly backend.Settings.Settings Settings = new()
    {
        OpeningBalance = 0m,
        StartDate = new DateOnly(2026, 1, 1),
        Iban = ClubIban,
    };

    private static string Statement(string reference = "TX1") =>
        CamtTestHelpers.CamtFile(
            "20260210",
            ClubIban,
            CamtTestHelpers.TxDtlsNtry(
                "100.00",
                "CRDT",
                "2026-02-10",
                reference,
                ustrd: "Jahresbeitrag"
            )
        );

    private static BankImportResult Import(byte[] content, string fileName = "statement.xml") =>
        BankImport.Import(
            content,
            new ImportSnapshot(Settings, [], [], [], null),
            fileName
        );

    private static BankImportResult Import(
        string content,
        string fileName = "statement.xml",
        string[]? existingReferences = null
    ) =>
        BankImport.Import(
            Encoding.UTF8.GetBytes(content),
            new ImportSnapshot(Settings, existingReferences ?? [], [], [], null),
            fileName
        );

    [Fact]
    public void A_single_xml_file_imports_like_a_zip_entry()
    {
        var transaction = Assert.Single(Import(Statement()).Transactions);

        Assert.Equal(new DateOnly(2026, 2, 10), transaction.Date);
        Assert.Equal(100.00m, transaction.Amount);
        Assert.Equal("Jahresbeitrag", transaction.Text);
        Assert.Equal("TX1", transaction.BankReference);
    }

    [Fact]
    public void A_reference_that_is_already_stored_is_skipped()
    {
        var result = Import(Statement(), existingReferences: ["TX1"]);

        Assert.Empty(result.Errors);
        Assert.Empty(result.Transactions);
        Assert.Equal(1, result.SkippedDuplicates);
    }

    [Fact]
    public void A_file_with_a_DTD_is_rejected_naming_the_file()
    {
        var document = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE Stmt [
              <!ENTITY status "BOOK">
            ]>
            <Stmt xmlns="{BankImport.Namespace}"><Id>1</Id></Stmt>
            """;

        var result = Import(document, "mine.xml");

        var error = Assert.Single(result.Errors);
        Assert.Equal("mine.xml", error.File);
        Assert.Contains("DTD", error.Message);
        Assert.Empty(result.Transactions);
    }

    [Fact]
    public void A_file_in_another_namespace_is_rejected_naming_the_file()
    {
        var document = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Document xmlns="urn:example:other"><Stmt/></Document>
            """;

        var result = Import(document, "mine.xml");

        var error = Assert.Single(result.Errors);
        Assert.Equal("mine.xml", error.File);
        Assert.Contains("CAMT.053", error.Message);
        Assert.Empty(result.Transactions);
    }

    [Fact]
    public void Content_that_is_neither_a_zip_nor_XML_is_an_error_naming_the_file()
    {
        var result = Import("this is not xml", "mine.xml");

        var error = Assert.Single(result.Errors);
        Assert.Equal("mine.xml", error.File);
        Assert.Contains("XML", error.Message);
        Assert.Empty(result.Transactions);
    }

    [Fact]
    public void Binary_content_is_an_error_naming_the_file()
    {
        var result = Import([0xFF, 0xFE, 0x00, 0x01, 0x80], "mine.xml");

        var error = Assert.Single(result.Errors);
        Assert.Equal("mine.xml", error.File);
        Assert.Empty(result.Transactions);
    }

    [Fact]
    public void Content_starting_with_the_zip_signature_that_is_no_zip_is_a_zip_error()
    {
        var result = Import("PK this is no zip", "mine.zip");

        var error = Assert.Single(result.Errors);
        Assert.Null(error.File);
        Assert.Contains("ZIP", error.Message);
    }

    [Fact]
    public void A_file_over_ten_megabytes_is_rejected_naming_the_file()
    {
        var result = Import(new byte[10 * 1024 * 1024 + 1], "mine.xml");

        var error = Assert.Single(result.Errors);
        Assert.Equal("mine.xml", error.File);
        Assert.Contains("10 MB", error.Message);
    }
}
