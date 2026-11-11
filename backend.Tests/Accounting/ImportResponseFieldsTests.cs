using backend.Accounting;

namespace backend.Tests.Accounting;

/// <summary>
/// The import success response (ticket #62, spec #44 §7): bookedInvoices = the Rechnungsnummern of the
/// movements that were auto-booked, in import order; unmatched = the count of imported movements (credits
/// and debits) that came in without Buchungen. The endpoint maps these two fields into its response 1:1.
/// </summary>
public class ImportResponseFieldsTests
{
    private const string ClubIban = "CH9300762011623852957";

    private static readonly Settings Settings = new()
    {
        OpeningBalance = 0m,
        StartDate = new DateOnly(2026, 1, 1),
        Iban = ClubIban,
    };

    [Fact]
    public void The_auto_booked_Rechnungsnummern_are_reported_in_import_order()
    {
        var chart = ChartOfAccounts.Parse(
            """
            {
              "income": [
                { "number": 3400, "name": "Beiträge" }
              ],
              "expense": []
            }
            """
        );
        var invoices = new[]
        {
            new ImportInvoice(42, "RF340042", 100m, 0m, false, [new ImportInvoiceLine("Jahresbeitrag", 3400, 100m)]),
            new ImportInvoice(43, "RF340043", 50m, 0m, false, [new ImportInvoiceLine("Beitrag", 3400, 50m)]),
        };
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFile(
                    "20260210",
                    ClubIban,
                    TxDtlsNtryWithReference("100.00", "CRDT", "2026-02-10", "TX1", "RF340042"),
                    TxDtlsNtry("10.00", "DBIT", "2026-02-10", "TX2"),
                    TxDtlsNtryWithReference("50.00", "CRDT", "2026-02-10", "TX3", "RF340043")
                )
            )
        );

        var result = Import(zip, invoices: invoices, chart: chart);
        Assert.Empty(result.Errors);
        Assert.Equal(3, result.Transactions.Count);

        var bookedInvoices = result.Transactions
            .Where(t => t.AutoEntries.Count > 0)
            .Select(t => t.MatchedInvoiceId!.Value)
            .ToList();

        Assert.Equal([42, 43], bookedInvoices);
    }

    [Fact]
    public void The_unmatched_count_counts_imported_movements_without_bookings()
    {
        var chart = ChartOfAccounts.Parse(
            """
            {
              "income": [
                { "number": 3400, "name": "Beiträge" }
              ],
              "expense": []
            }
            """
        );
        var invoices = new[]
        {
            new ImportInvoice(42, "RF340042", 100m, 0m, false, [new ImportInvoiceLine("Jahresbeitrag", 3400, 100m)]),
        };
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFile(
                    "20260210",
                    ClubIban,
                    TxDtlsNtryWithReference("100.00", "CRDT", "2026-02-10", "TX1", "RF340042"),
                    TxDtlsNtry("10.00", "DBIT", "2026-02-10", "TX2"),
                    TxDtlsNtry("20.00", "CRDT", "2026-02-10", "TX3")
                )
            )
        );

        var result = Import(zip, invoices: invoices, chart: chart);
        Assert.Empty(result.Errors);

        var unmatched = result.Transactions.Count(t => t.AutoEntries.Count == 0);

        Assert.Equal(2, unmatched);
    }

    private static BankImportResult Import(
        byte[] zip,
        IReadOnlyCollection<ImportInvoice>? invoices = null,
        ChartOfAccounts? chart = null
    )
    {
        return BankImport.Import(zip, new ImportSnapshot(Settings, [], [], invoices ?? [], chart));
    }

    private static string CamtFile(string name, string iban, params string[] ntries) =>
        $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.08">
                <BkToCstmrStmt>
                    <Stmt>
                      <Id>{name}</Id>
                      <CreDtTm>2026-02-11T00:00:00</CreDtTm>
                      <Acct>
                        <Id>
                          <IBAN>{iban}</IBAN>
                        </Id>
                      </Acct>
                      {string.Join("\n", ntries)}
                    </Stmt>
                </BkToCstmrStmt>
            </Document>
            """;

    /// <summary>One Ntry with one TxDtls carrying a QR-bill CdtrRefInf reference.</summary>
    private static string TxDtlsNtryWithReference(
        string amount,
        string direction,
        string date,
        string txRef,
        string qrReference
    ) =>
        $"""
            <Ntry>
              <Amt Ccy="CHF">{amount}</Amt>
              <CdtDbtInd>{direction}</CdtDbtInd>
              <Sts><Cd>BOOK</Cd></Sts>
              <BookgDt><Dt>{date}</Dt></BookgDt>
              <NtryDtls><TxDtls>
                <Refs><AcctSvcrRef>{txRef}</AcctSvcrRef></Refs>
                <Amt Ccy="CHF">{amount}</Amt>
                <RmtInf>
                  <Strd>
                    <CdtrRefInf>
                      <Ref>{qrReference}</Ref>
                    </CdtrRefInf>
                  </Strd>
                </RmtInf>
              </TxDtls></NtryDtls>
            </Ntry>
            """;

    /// <summary>One Ntry with one TxDtls, without a QR-bill reference.</summary>
    private static string TxDtlsNtry(
        string amount,
        string direction,
        string date,
        string txRef
    ) =>
        $"""
            <Ntry>
              <Amt Ccy="CHF">{amount}</Amt>
              <CdtDbtInd>{direction}</CdtDbtInd>
              <Sts><Cd>BOOK</Cd></Sts>
              <BookgDt><Dt>{date}</Dt></BookgDt>
              <NtryDtls><TxDtls>
                <Refs><AcctSvcrRef>{txRef}</AcctSvcrRef></Refs>
                <Amt Ccy="CHF">{amount}</Amt>
              </TxDtls></NtryDtls>
            </Ntry>
            """;
}
