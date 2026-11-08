using System.IO.Compression;
using backend.Accounting;

namespace backend.Tests.Accounting;

/// <summary>
/// Deleting an imported Bewegung removes its BankReference from the stored set, so importing the same file
/// again recreates it (ticket #30). Verified at the BankImport seam: dedup only skips known references.
/// </summary>
public class BankImportReimportTests
{
    private const string ClubIban = "CH9300762011623852957";

    private static readonly Settings Settings = new()
    {
        OpeningBalance = 0m,
        StartDate = new DateOnly(2026, 1, 1),
        Iban = ClubIban,
    };

    [Fact]
    public void A_deleted_imported_transaction_is_recreated_by_reimporting_the_same_file()
    {
        var zip = Zip(("20260210.xml", CamtFile()));

        var imported = Assert.Single(BankImport.Import(zip, new ImportSnapshot(Settings, [], [])).Transactions);
        // The Bewegung was deleted, so its BankReference is no longer stored and the same file imports it again.
        var reimported = Assert.Single(BankImport.Import(zip, new ImportSnapshot(Settings, [], [])).Transactions);

        Assert.Equal(imported.Date, reimported.Date);
        Assert.Equal(imported.Amount, reimported.Amount);
        Assert.Equal(imported.Text, reimported.Text);
        Assert.Equal(imported.BankReference, reimported.BankReference);
    }

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

    private static string CamtFile() => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <Stmt xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.08">
          <Id>20260210</Id>
          <CreDtTm>2026-02-11T00:00:00</CreDtTm>
          <Acct>
            <Id>
              <IBAN>{ClubIban}</IBAN>
            </Id>
          </Acct>
          <Ntry>
            <Amt Ccy="CHF">100.00</Amt>
            <CdtDbtInd>CRDT</CdtDbtInd>
            <Sts>BOOK</Sts>
            <BookgDt><Dt>2026-02-10</Dt></BookgDt>
            <TxDtls>
              <Refs><AcctSvcrRef>TX1</AcctSvcrRef></Refs>
              <Amt Ccy="CHF">100.00</Amt>
              <Ustrd>Jahresbeitrag</Ustrd>
            </TxDtls>
          </Ntry>
        </Stmt>
        """;
}
