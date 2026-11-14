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
                CamtTestHelpers.CamtFile(
                    "20260210",
                    ClubIban,
                    CamtTestHelpers.TxDtlsNtryWithReference("100.00", "CRDT", "2026-02-10", "TX1", "RF340042"),
                    CamtTestHelpers.TxDtlsNtry("10.00", "DBIT", "2026-02-10", "TX2"),
                    CamtTestHelpers.TxDtlsNtryWithReference("50.00", "CRDT", "2026-02-10", "TX3", "RF340043")
                )
            )
        );

        var result = Import(zip, invoices: invoices, chart: chart);
        Assert.Empty(result.Errors);
        Assert.Equal(3, result.Transactions.Count);

        Assert.Equal([42, 43], BankImport.BookedInvoiceIds(result));
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
                CamtTestHelpers.CamtFile(
                    "20260210",
                    ClubIban,
                    CamtTestHelpers.TxDtlsNtryWithReference("100.00", "CRDT", "2026-02-10", "TX1", "RF340042"),
                    CamtTestHelpers.TxDtlsNtry("10.00", "DBIT", "2026-02-10", "TX2"),
                    CamtTestHelpers.TxDtlsNtry("20.00", "CRDT", "2026-02-10", "TX3")
                )
            )
        );

        var result = Import(zip, invoices: invoices, chart: chart);
        Assert.Empty(result.Errors);

        Assert.Equal(2, BankImport.UnmatchedCount(result));
    }

    private static BankImportResult Import(
        byte[] zip,
        IReadOnlyCollection<ImportInvoice>? invoices = null,
        ChartOfAccounts? chart = null
    )
    {
        return BankImport.Import(zip, new ImportSnapshot(Settings, [], [], invoices ?? [], chart));
    }
}
