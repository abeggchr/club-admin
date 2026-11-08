using System.IO.Compression;
using backend.Accounting;

namespace backend.Tests.Accounting;

public class BankImportTests
{
    private const string ClubIban = "CH9300762011623852957";

    private const string OtherIban = "CH9300762011623852951";

    private static readonly Settings Settings = new()
    {
        OpeningBalance = 0m,
        StartDate = new DateOnly(2026, 1, 1),
        Iban = ClubIban,
    };

    [Fact]
    public void A_credit_entry_maps_to_a_positive_transaction_with_the_booking_date()
    {
        var zip = Zip(("20260210.xml", CamtFile("20260210", ClubIban, TxDtlsNtry("100.00", "CRDT", "2026-02-10", "TX1", ustrd: "Jahresbeitrag"))));

        var transaction = Assert.Single(Import(zip).Transactions);
        Assert.Equal(new DateOnly(2026, 2, 10), transaction.Date);
        Assert.Equal(100.00m, transaction.Amount);
        Assert.Equal("Jahresbeitrag", transaction.Text);
        Assert.Equal("TX1", transaction.BankReference);
    }

    [Fact]
    public void A_debit_entry_maps_to_a_negative_transaction()
    {
        var zip = Zip(("20260210.xml", CamtFile("20260210", ClubIban, TxDtlsNtry("45.20", "DBIT", "2026-02-10", "TX1"))));

        var transaction = Assert.Single(Import(zip).Transactions);
        Assert.Equal(-45.20m, transaction.Amount);
    }

    [Fact]
    public void A_fee_entry_without_TxDtls_is_an_outgoing_transaction_with_the_AddtlNtryInf_as_text()
    {
        var zip = Zip(("20260210.xml", CamtFile("20260210", ClubIban, PlainNtry("12.50", "DBIT", "2026-02-10", "FEE1", addtl: "Zahlungsverkehrspreise"))));

        var transaction = Assert.Single(Import(zip).Transactions);
        Assert.Equal(-12.50m, transaction.Amount);
        Assert.Equal("Zahlungsverkehrspreise", transaction.Text);
        Assert.Equal("FEE1", transaction.BankReference);
    }

    [Fact]
    public void The_Ustrd_values_of_the_transaction_are_joined_with_spaces()
    {
        var ntry = """
            <Ntry>
              <Amt Ccy="CHF">50.00</Amt>
              <CdtDbtInd>CRDT</CdtDbtInd>
              <Sts>BOOK</Sts>
              <BookgDt><Dt>2026-02-10</Dt></BookgDt>
              <TxDtls>
                <Refs><AcctSvcrRef>TXT</AcctSvcrRef></Refs>
                <Amt Ccy="CHF">50.00</Amt>
                <Ustrd>Einschreibung</Ustrd>
                <Ustrd>2026</Ustrd>
              </TxDtls>
            </Ntry>
            """;
        var zip = Zip(("20260210.xml", CamtFile("20260210", ClubIban, ntry)));

        Assert.Equal("Einschreibung 2026", Assert.Single(Import(zip).Transactions).Text);
    }

    [Fact]
    public void Without_Ustrd_the_AddtlNtryInf_is_used_as_text()
    {
        var zip = Zip(("20260210.xml", CamtFile("20260210", ClubIban, TxDtlsNtry("50.00", "CRDT", "2026-02-10", "TX1", addtl: "Zahlungsverkehrspreise"))));

        Assert.Equal("Zahlungsverkehrspreise", Assert.Single(Import(zip).Transactions).Text);
    }

    [Fact]
    public void A_file_with_a_different_IBAN_is_an_error_naming_the_file()
    {
        var zip = Zip(("20260210.xml", CamtFile("20260210", OtherIban, TxDtlsNtry("100.00", "CRDT", "2026-02-10", "TX1"))));

        var result = Import(zip);
        var error = Assert.Single(result.Errors);
        Assert.Equal("20260210.xml", error.File);
        Assert.Contains("Klub-IBAN", error.Message);
        Assert.Empty(result.Transactions);
    }

    [Fact]
    public void The_file_IBAN_is_compared_without_spaces_and_case()
    {
        var zip = Zip(("20260210.xml", CamtFile("20260210", "ch93 0076 2011 6238 5295 7", TxDtlsNtry("100.00", "CRDT", "2026-02-10", "TX1"))));

        var result = Import(zip);
        Assert.Single(result.Transactions);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void A_transaction_before_the_StartDate_is_an_error()
    {
        var zip = Zip(("20251231.xml", CamtFile("20251231", ClubIban, TxDtlsNtry("100.00", "CRDT", "2025-12-31", "TX1"))));

        var result = Import(zip);
        var error = Assert.Single(result.Errors);
        Assert.Equal("20251231.xml", error.File);
        Assert.Contains("vor dem Anfangssaldo", error.Message);
    }

    [Fact]
    public void A_zero_amount_is_an_error()
    {
        var zip = Zip(("20260210.xml", CamtFile("20260210", ClubIban, TxDtlsNtry("0.00", "CRDT", "2026-02-10", "TX1"))));

        var result = Import(zip);
        var error = Assert.Single(result.Errors);
        Assert.Equal("20260210.xml", error.File);
        Assert.Contains("darf nicht 0", error.Message);
    }

    [Fact]
    public void A_reference_that_already_exists_is_skipped_and_counted()
    {
        var zip = Zip(("20260210.xml", CamtFile("20260210", ClubIban, TxDtlsNtry("100.00", "CRDT", "2026-02-10", "TX1"))));

        var result = Import(zip, ["TX1"]);
        Assert.Empty(result.Transactions);
        Assert.Equal(1, result.SkippedDuplicates);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void A_reference_appearing_twice_in_one_upload_is_imported_only_once()
    {
        var file = CamtFile(
            "20260210",
            ClubIban,
            TxDtlsNtry("100.00", "CRDT", "2026-02-10", "TX1"),
            TxDtlsNtry("50.00", "CRDT", "2026-02-11", "TX1")
        );
        var zip = Zip(("20260210.xml", file));

        var result = Import(zip);
        var transaction = Assert.Single(result.Transactions);
        Assert.Equal("TX1", transaction.BankReference);
        Assert.Equal(1, result.SkippedDuplicates);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void An_error_in_one_file_prevents_anything_from_being_saved()
    {
        var good = CamtFile("20260210", ClubIban, TxDtlsNtry("100.00", "CRDT", "2026-02-10", "TX1"));
        var bad = CamtFile("20260211", OtherIban, TxDtlsNtry("50.00", "CRDT", "2026-02-11", "TX2"));
        var zip = Zip(("a.xml", good), ("b.xml", bad));

        var result = Import(zip);
        var error = Assert.Single(result.Errors);
        Assert.Equal("b.xml", error.File);
        Assert.Empty(result.Transactions);
    }

    [Fact]
    public void An_entry_with_several_TxDtls_is_an_error()
    {
        var ntry = """
            <Ntry>
              <Amt Ccy="CHF">150.00</Amt>
              <CdtDbtInd>DBIT</CdtDbtInd>
              <Sts>BOOK</Sts>
              <BookgDt><Dt>2026-02-10</Dt></BookgDt>
              <AcctSvcrRef>BATCH1</AcctSvcrRef>
              <TxDtls>
                <Refs><AcctSvcrRef>B1</AcctSvcrRef></Refs>
                <Amt Ccy="CHF">100.00</Amt>
              </TxDtls>
              <TxDtls>
                <Refs><AcctSvcrRef>B2</AcctSvcrRef></Refs>
                <Amt Ccy="CHF">50.00</Amt>
              </TxDtls>
            </Ntry>
            """;
        var zip = Zip(("20260210.xml", CamtFile("20260210", ClubIban, ntry)));

        var error = Assert.Single(Import(zip).Errors);
        Assert.Contains("noch nicht unterstützt", error.Message);
    }

    [Fact]
    public void Non_xml_entries_are_ignored()
    {
        var zip = Zip(
            ("readme.txt", "Kein XML"),
            ("20260210.xml", CamtFile("20260210", ClubIban, TxDtlsNtry("100.00", "CRDT", "2026-02-10", "TX1")))
        );

        var result = Import(zip);
        Assert.Single(result.Transactions);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void A_zip_without_xml_entries_is_an_error()
    {
        var zip = Zip(("readme.txt", "Kein XML"));

        var error = Assert.Single(Import(zip).Errors);
        Assert.Null(error.File);
        Assert.Contains("CAMT.053", error.Message);
    }

    private static BankImportResult Import(byte[] zip, string[]? existingReferences = null) =>
        BankImport.Import(zip, new ImportSnapshot(Settings, existingReferences ?? []));

    private static byte[] Zip(params (string Name, string Content)[] files)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in files)
            {
                var entry = archive.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(content);
            }
        }
        return stream.ToArray();
    }

    private static string CamtFile(string name, string iban, params string[] ntries) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <Stmt xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.08">
          <Id>{name}</Id>
          <CreDtTm>2026-02-11T00:00:00</CreDtTm>
          <Acct>
            <Id>
              <IBAN>{iban}</IBAN>
            </Id>
          </Acct>
          {string.Join("\n", ntries)}
        </Stmt>
        """;

    /// <summary>One Ntry with exactly one TxDtls, the shape the ZKB eBanking sends.</summary>
    private static string TxDtlsNtry(string amount, string direction, string date, string txRef, string? ustrd = null, string? addtl = null) => $"""
        <Ntry>
          <Amt Ccy="CHF">{amount}</Amt>
          <CdtDbtInd>{direction}</CdtDbtInd>
          <Sts>BOOK</Sts>
          <BookgDt><Dt>{date}</Dt></BookgDt>
          <TxDtls>
            <Refs><AcctSvcrRef>{txRef}</AcctSvcrRef></Refs>
            <Amt Ccy="CHF">{amount}</Amt>
            {(ustrd is null ? "" : $"<Ustrd>{ustrd}</Ustrd>")}
          </TxDtls>
          {(addtl is null ? "" : $"<AddtlNtryInf>{addtl}</AddtlNtryInf>")}
        </Ntry>
        """;

    /// <summary>One Ntry without TxDtls, e.g. the fee entry "Zahlungsverkehrspreise".</summary>
    private static string PlainNtry(string amount, string direction, string date, string entryRef, string? ustrd = null, string? addtl = null) => $"""
        <Ntry>
          <Amt Ccy="CHF">{amount}</Amt>
          <CdtDbtInd>{direction}</CdtDbtInd>
          <Sts>BOOK</Sts>
          <BookgDt><Dt>{date}</Dt></BookgDt>
          <AcctSvcrRef>{entryRef}</AcctSvcrRef>
          {(ustrd is null ? "" : $"<Ustrd>{ustrd}</Ustrd>")}
          {(addtl is null ? "" : $"<AddtlNtryInf>{addtl}</AddtlNtryInf>")}
        </Ntry>
        """;
}
