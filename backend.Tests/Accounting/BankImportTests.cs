using System.Linq;
using backend.Accounting;

namespace backend.Tests.Accounting;

using Person = backend.Person.Person;

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
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFile(
                    "20260210",
                    ClubIban,
                    TxDtlsNtry("100.00", "CRDT", "2026-02-10", "TX1", ustrd: "Jahresbeitrag")
                )
            )
        );

        var transaction = Assert.Single(Import(zip).Transactions);
        Assert.Equal(new DateOnly(2026, 2, 10), transaction.Date);
        Assert.Equal(100.00m, transaction.Amount);
        Assert.Equal("Jahresbeitrag", transaction.Text);
        Assert.Equal("TX1", transaction.BankReference);
    }

    [Fact]
    public void A_debit_entry_maps_to_a_negative_transaction()
    {
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFile("20260210", ClubIban, TxDtlsNtry("45.20", "DBIT", "2026-02-10", "TX1"))
            )
        );

        var transaction = Assert.Single(Import(zip).Transactions);
        Assert.Equal(-45.20m, transaction.Amount);
    }

    [Fact]
    public void A_fee_entry_without_TxDtls_is_an_outgoing_transaction_with_the_AddtlNtryInf_as_text()
    {
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFile(
                    "20260210",
                    ClubIban,
                    PlainNtry(
                        "12.50",
                        "DBIT",
                        "2026-02-10",
                        "FEE1",
                        addtl: "Zahlungsverkehrspreise"
                    )
                )
            )
        );

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
        var zip = CamtTestFiles.Zip(("20260210.xml", CamtFile("20260210", ClubIban, ntry)));

        Assert.Equal("Einschreibung 2026", Assert.Single(Import(zip).Transactions).Text);
    }

    [Fact]
    public void Without_Ustrd_the_AddtlNtryInf_is_used_as_text()
    {
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFile(
                    "20260210",
                    ClubIban,
                    TxDtlsNtry(
                        "50.00",
                        "CRDT",
                        "2026-02-10",
                        "TX1",
                        addtl: "Zahlungsverkehrspreise"
                    )
                )
            )
        );

        Assert.Equal("Zahlungsverkehrspreise", Assert.Single(Import(zip).Transactions).Text);
    }

    [Fact]
    public void The_counterparty_of_a_credit_is_the_Dbtr_name_in_front_of_the_text()
    {
        var ntry = """
            <Ntry>
              <Amt Ccy="CHF">100.00</Amt>
              <CdtDbtInd>CRDT</CdtDbtInd>
              <Sts>BOOK</Sts>
              <BookgDt><Dt>2026-02-10</Dt></BookgDt>
              <TxDtls>
                <Refs><AcctSvcrRef>TX1</AcctSvcrRef></Refs>
                <Amt Ccy="CHF">100.00</Amt>
                <Dbtr><Pty><Nm>M. Muster</Nm></Pty></Dbtr>
                <Cdtr><Pty><Nm>SCW Zug</Nm></Pty></Cdtr>
                <Ustrd>Jahresbeitrag</Ustrd>
              </TxDtls>
            </Ntry>
            """;
        var zip = CamtTestFiles.Zip(("20260210.xml", CamtFile("20260210", ClubIban, ntry)));

        Assert.Equal("M. Muster – Jahresbeitrag", Assert.Single(Import(zip).Transactions).Text);
    }

    [Fact]
    public void The_counterparty_of_a_debit_is_the_Cdtr_name_in_front_of_the_text()
    {
        var ntry = """
            <Ntry>
              <Amt Ccy="CHF">45.20</Amt>
              <CdtDbtInd>DBIT</CdtDbtInd>
              <Sts>BOOK</Sts>
              <BookgDt><Dt>2026-02-10</Dt></BookgDt>
              <TxDtls>
                <Refs><AcctSvcrRef>TX1</AcctSvcrRef></Refs>
                <Amt Ccy="CHF">45.20</Amt>
                <Dbtr><Pty><Nm>SCW Zug</Nm></Pty></Dbtr>
                <Cdtr><Pty><Nm>Bäckerei Beispiel</Nm></Pty></Cdtr>
                <Ustrd>Miete Februar</Ustrd>
              </TxDtls>
            </Ntry>
            """;
        var zip = CamtTestFiles.Zip(("20260210.xml", CamtFile("20260210", ClubIban, ntry)));

        Assert.Equal(
            "Bäckerei Beispiel – Miete Februar",
            Assert.Single(Import(zip).Transactions).Text
        );
    }

    [Fact]
    public void Without_a_counterparty_the_text_is_unchanged()
    {
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFile(
                    "20260210",
                    ClubIban,
                    TxDtlsNtry("100.00", "CRDT", "2026-02-10", "TX1", ustrd: "Jahresbeitrag")
                )
            )
        );

        Assert.Equal("Jahresbeitrag", Assert.Single(Import(zip).Transactions).Text);
    }

    [Fact]
    public void The_prefixed_text_is_truncated_to_the_500_character_limit()
    {
        var ntry = $"""
            <Ntry>
              <Amt Ccy="CHF">100.00</Amt>
              <CdtDbtInd>CRDT</CdtDbtInd>
              <Sts>BOOK</Sts>
              <BookgDt><Dt>2026-02-10</Dt></BookgDt>
              <TxDtls>
                <Refs><AcctSvcrRef>TX1</AcctSvcrRef></Refs>
                <Amt Ccy="CHF">100.00</Amt>
                <Dbtr><Pty><Nm>Langer Name</Nm></Pty></Dbtr>
                <Ustrd>{new string('a', 490)}</Ustrd>
              </TxDtls>
            </Ntry>
            """;
        var zip = CamtTestFiles.Zip(("20260210.xml", CamtFile("20260210", ClubIban, ntry)));

        var text = Assert.Single(Import(zip).Transactions).Text!;
        Assert.Equal(500, text.Length);
        Assert.StartsWith("Langer Name – ", text);
    }

    [Fact]
    public void The_plain_text_is_truncated_to_the_500_character_limit()
    {
        var ntry = $"""
            <Ntry>
              <Amt Ccy="CHF">100.00</Amt>
              <CdtDbtInd>CRDT</CdtDbtInd>
              <Sts>BOOK</Sts>
              <BookgDt><Dt>2026-02-10</Dt></BookgDt>
              <TxDtls>
                <Refs><AcctSvcrRef>TX1</AcctSvcrRef></Refs>
                <Amt Ccy="CHF">100.00</Amt>
                <Ustrd>{new string('a', 501)}</Ustrd>
              </TxDtls>
            </Ntry>
            """;
        var zip = CamtTestFiles.Zip(("20260210.xml", CamtFile("20260210", ClubIban, ntry)));

        var text = Assert.Single(Import(zip).Transactions).Text!;
        Assert.Equal(500, text.Length);
        Assert.Equal(new string('a', 500), text);
    }

    [Fact]
    public void A_file_with_a_namespace_that_is_not_CAMT_053_is_an_error_naming_the_file()
    {
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFileIn(
                    "urn:iso:std:iso:20022:tech:xsd:camt.054.001.08",
                    "20260210",
                    ClubIban,
                    TxDtlsNtry("100.00", "CRDT", "2026-02-10", "TX1")
                )
            )
        );

        var result = Import(zip);
        var error = Assert.Single(result.Errors);
        Assert.Equal("20260210.xml", error.File);
        Assert.Contains("CAMT.053", error.Message);
        Assert.Empty(result.Transactions);
    }

    [Fact]
    public void A_file_without_a_namespace_is_an_error()
    {
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFileIn(
                    "",
                    "20260210",
                    ClubIban,
                    TxDtlsNtry("100.00", "CRDT", "2026-02-10", "TX1")
                )
            )
        );

        var result = Import(zip);
        var error = Assert.Single(result.Errors);
        Assert.Equal("20260210.xml", error.File);
        Assert.Contains("CAMT.053", error.Message);
        Assert.Empty(result.Transactions);
    }

    [Fact]
    public void A_dot_02_shaped_file_is_imported()
    {
        var ntry = """
            <Ntry>
              <AmtDtls>
                <TxAmt><Amt Ccy="CHF">100.00</Amt></TxAmt>
              </AmtDtls>
              <CdtDbtInd>CRDT</CdtDbtInd>
              <Sts>BOOK</Sts>
              <BookgDt><Dt>2026-02-10</Dt></BookgDt>
              <TxDtls>
                <Refs><AcctSvcrRef>TX1</AcctSvcrRef></Refs>
                <AmtDtls>
                  <TxAmt><Amt Ccy="CHF">100.00</Amt></TxAmt>
                </AmtDtls>
                <Dbtr>
                  <Nm>M. Muster</Nm>
                </Dbtr>
                <Ustrd>Jahresbeitrag</Ustrd>
              </TxDtls>
            </Ntry>
            """;
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFileIn(
                    "urn:iso:std:iso:20022:tech:xsd:camt.053.001.02",
                    "20260210",
                    ClubIban,
                    ntry
                )
            )
        );

        var result = Import(zip);
        var transaction = Assert.Single(result.Transactions);
        Assert.Empty(result.Errors);
        Assert.Equal(100.00m, transaction.Amount);
        // The dot 02 counterparty name (Dbtr/Nm) is put in front of the text, too.
        Assert.Equal("M. Muster – Jahresbeitrag", transaction.Text);
        Assert.Equal("TX1", transaction.BankReference);
    }

    [Fact]
    public void An_entry_without_a_status_is_an_error_naming_the_file()
    {
        var ntry = """
            <Ntry>
              <Amt Ccy="CHF">100.00</Amt>
              <CdtDbtInd>CRDT</CdtDbtInd>
              <BookgDt><Dt>2026-02-10</Dt></BookgDt>
              <AcctSvcrRef>TX1</AcctSvcrRef>
            </Ntry>
            """;
        var zip = CamtTestFiles.Zip(("20260210.xml", CamtFile("20260210", ClubIban, ntry)));

        var result = Import(zip);
        var error = Assert.Single(result.Errors);
        Assert.Equal("20260210.xml", error.File);
        Assert.Contains("Status", error.Message);
        Assert.Empty(result.Transactions);
    }

    [Theory]
    [InlineData("PDNG")]
    [InlineData("POST")]
    [InlineData("CHAR")]
    public void An_entry_with_a_status_other_than_BOOK_is_an_error_naming_the_file(string status)
    {
        var ntry = $"""
            <Ntry>
              <Amt Ccy="CHF">100.00</Amt>
              <CdtDbtInd>CRDT</CdtDbtInd>
              <Sts>{status}</Sts>
              <BookgDt><Dt>2026-02-10</Dt></BookgDt>
              <AcctSvcrRef>TX1</AcctSvcrRef>
            </Ntry>
            """;
        var zip = CamtTestFiles.Zip(("20260210.xml", CamtFile("20260210", ClubIban, ntry)));

        var result = Import(zip);
        var error = Assert.Single(result.Errors);
        Assert.Equal("20260210.xml", error.File);
        Assert.Contains(status, error.Message);
        Assert.Contains("BOOK", error.Message);
        Assert.Empty(result.Transactions);
    }

    [Fact]
    public void A_status_written_as_Sts_Cd_is_read_the_same_way()
    {
        var ntry = """
            <Ntry>
              <Amt Ccy="CHF">100.00</Amt>
              <CdtDbtInd>CRDT</CdtDbtInd>
              <Sts><Cd>BOOK</Cd></Sts>
              <BookgDt><Dt>2026-02-10</Dt></BookgDt>
              <AcctSvcrRef>TX1</AcctSvcrRef>
            </Ntry>
            """;
        var zip = CamtTestFiles.Zip(("20260210.xml", CamtFile("20260210", ClubIban, ntry)));

        var result = Import(zip);
        Assert.Single(result.Transactions);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void An_entry_marked_as_reversal_is_an_error_naming_the_file()
    {
        var ntry = """
            <Ntry>
              <Amt Ccy="CHF">100.00</Amt>
              <CdtDbtInd>CRDT</CdtDbtInd>
              <Sts>BOOK</Sts>
              <RvslInd>true</RvslInd>
              <BookgDt><Dt>2026-02-10</Dt></BookgDt>
              <AcctSvcrRef>TX1</AcctSvcrRef>
            </Ntry>
            """;
        var zip = CamtTestFiles.Zip(("20260210.xml", CamtFile("20260210", ClubIban, ntry)));

        var result = Import(zip);
        var error = Assert.Single(result.Errors);
        Assert.Equal("20260210.xml", error.File);
        Assert.Contains("Storn", error.Message);
        Assert.Empty(result.Transactions);
    }

    [Fact]
    public void A_reversal_marked_inside_the_transaction_details_is_an_error()
    {
        var ntry = """
            <Ntry>
              <Amt Ccy="CHF">100.00</Amt>
              <CdtDbtInd>CRDT</CdtDbtInd>
              <Sts>BOOK</Sts>
              <BookgDt><Dt>2026-02-10</Dt></BookgDt>
              <TxDtls>
                <Refs><AcctSvcrRef>TX1</AcctSvcrRef></Refs>
                <Amt Ccy="CHF">100.00</Amt>
                <RvslInd>true</RvslInd>
              </TxDtls>
            </Ntry>
            """;
        var zip = CamtTestFiles.Zip(("20260210.xml", CamtFile("20260210", ClubIban, ntry)));

        var error = Assert.Single(Import(zip).Errors);
        Assert.Contains("Storn", error.Message);
    }

    [Fact]
    public void A_file_without_the_account_IBAN_is_an_error_naming_the_file()
    {
        var ntry = """
            <Ntry>
              <Amt Ccy="CHF">100.00</Amt>
              <CdtDbtInd>CRDT</CdtDbtInd>
              <Sts>BOOK</Sts>
              <BookgDt><Dt>2026-02-10</Dt></BookgDt>
              <AcctSvcrRef>TX1</AcctSvcrRef>
            </Ntry>
            """;
        var document = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Stmt xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.08">
              <Id>20260210</Id>
              {ntry}
            </Stmt>
            """;
        var zip = CamtTestFiles.Zip(("20260210.xml", document));

        var result = Import(zip);
        var error = Assert.Single(result.Errors);
        Assert.Equal("20260210.xml", error.File);
        Assert.Contains("IBAN", error.Message);
        Assert.Empty(result.Transactions);
    }

    [Fact]
    public void An_entry_without_an_amount_is_an_error_naming_the_file()
    {
        var ntry = """
            <Ntry>
              <CdtDbtInd>CRDT</CdtDbtInd>
              <Sts>BOOK</Sts>
              <BookgDt><Dt>2026-02-10</Dt></BookgDt>
              <AcctSvcrRef>TX1</AcctSvcrRef>
            </Ntry>
            """;
        var zip = CamtTestFiles.Zip(("20260210.xml", CamtFile("20260210", ClubIban, ntry)));

        var result = Import(zip);
        var error = Assert.Single(result.Errors);
        Assert.Equal("20260210.xml", error.File);
        Assert.Contains("Betrag", error.Message);
        Assert.Empty(result.Transactions);
    }

    [Fact]
    public void An_entry_without_a_direction_is_an_error_naming_the_file()
    {
        var ntry = """
            <Ntry>
              <Amt Ccy="CHF">100.00</Amt>
              <Sts>BOOK</Sts>
              <BookgDt><Dt>2026-02-10</Dt></BookgDt>
              <AcctSvcrRef>TX1</AcctSvcrRef>
            </Ntry>
            """;
        var zip = CamtTestFiles.Zip(("20260210.xml", CamtFile("20260210", ClubIban, ntry)));

        var result = Import(zip);
        var error = Assert.Single(result.Errors);
        Assert.Equal("20260210.xml", error.File);
        Assert.Contains("Gutschrift", error.Message);
        Assert.Empty(result.Transactions);
    }

    [Fact]
    public void An_entry_without_a_booking_date_is_an_error_naming_the_file()
    {
        var ntry = """
            <Ntry>
              <Amt Ccy="CHF">100.00</Amt>
              <CdtDbtInd>CRDT</CdtDbtInd>
              <Sts>BOOK</Sts>
              <AcctSvcrRef>TX1</AcctSvcrRef>
            </Ntry>
            """;
        var zip = CamtTestFiles.Zip(("20260210.xml", CamtFile("20260210", ClubIban, ntry)));

        var result = Import(zip);
        var error = Assert.Single(result.Errors);
        Assert.Equal("20260210.xml", error.File);
        Assert.Contains("Buchungsdatum", error.Message);
        Assert.Empty(result.Transactions);
    }

    [Fact]
    public void An_entry_without_a_bank_reference_is_an_error_naming_the_file()
    {
        var ntry = """
            <Ntry>
              <Amt Ccy="CHF">100.00</Amt>
              <CdtDbtInd>CRDT</CdtDbtInd>
              <Sts>BOOK</Sts>
              <BookgDt><Dt>2026-02-10</Dt></BookgDt>
            </Ntry>
            """;
        var zip = CamtTestFiles.Zip(("20260210.xml", CamtFile("20260210", ClubIban, ntry)));

        var result = Import(zip);
        var error = Assert.Single(result.Errors);
        Assert.Equal("20260210.xml", error.File);
        Assert.Contains("Bankreferenz", error.Message);
        Assert.Empty(result.Transactions);
    }

    [Fact]
    public void A_file_with_a_different_IBAN_is_an_error_naming_the_file()
    {
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFile("20260210", OtherIban, TxDtlsNtry("100.00", "CRDT", "2026-02-10", "TX1"))
            )
        );

        var result = Import(zip);
        var error = Assert.Single(result.Errors);
        Assert.Equal("20260210.xml", error.File);
        Assert.Contains("Klub-IBAN", error.Message);
        Assert.Empty(result.Transactions);
    }

    [Fact]
    public void The_file_IBAN_is_compared_without_spaces_and_case()
    {
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFile(
                    "20260210",
                    "ch93 0076 2011 6238 5295 7",
                    TxDtlsNtry("100.00", "CRDT", "2026-02-10", "TX1")
                )
            )
        );

        var result = Import(zip);
        Assert.Single(result.Transactions);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void A_transaction_before_the_StartDate_is_an_error()
    {
        var zip = CamtTestFiles.Zip(
            (
                "20251231.xml",
                CamtFile("20251231", ClubIban, TxDtlsNtry("100.00", "CRDT", "2025-12-31", "TX1"))
            )
        );

        var result = Import(zip);
        var error = Assert.Single(result.Errors);
        Assert.Equal("20251231.xml", error.File);
        Assert.Contains("vor dem Anfangssaldo", error.Message);
    }

    [Fact]
    public void A_zero_amount_is_an_error()
    {
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFile("20260210", ClubIban, TxDtlsNtry("0.00", "CRDT", "2026-02-10", "TX1"))
            )
        );

        var result = Import(zip);
        var error = Assert.Single(result.Errors);
        Assert.Equal("20260210.xml", error.File);
        Assert.Contains("darf nicht 0", error.Message);
    }

    [Fact]
    public void A_reference_that_already_exists_is_skipped_and_counted()
    {
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFile("20260210", ClubIban, TxDtlsNtry("100.00", "CRDT", "2026-02-10", "TX1"))
            )
        );

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
        var zip = CamtTestFiles.Zip(("20260210.xml", file));

        var result = Import(zip);
        var transaction = Assert.Single(result.Transactions);
        Assert.Equal("TX1", transaction.BankReference);
        Assert.Equal(1, result.SkippedDuplicates);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void An_error_in_one_file_prevents_anything_from_being_saved()
    {
        var good = CamtFile(
            "20260210",
            ClubIban,
            TxDtlsNtry("100.00", "CRDT", "2026-02-10", "TX1")
        );
        var bad = CamtFile("20260211", OtherIban, TxDtlsNtry("50.00", "CRDT", "2026-02-11", "TX2"));
        var zip = CamtTestFiles.Zip(("a.xml", good), ("b.xml", bad));

        var result = Import(zip);
        var error = Assert.Single(result.Errors);
        Assert.Equal("b.xml", error.File);
        Assert.Empty(result.Transactions);
    }

    [Fact]
    public void A_batch_entry_uses_the_counterparty_of_its_own_TxDtls_per_transaction()
    {
        var ntry = BatchNtry(
            "150.00",
            "DBIT",
            "2026-02-10",
            ("B1", "100.00", "Mitgliedsbeitrag", "A. Anna"),
            ("B2", "50.00", "Verbandsbeitrag", "B. Beat")
        );
        var zip = CamtTestFiles.Zip(("20260210.xml", CamtFile("20260210", ClubIban, ntry)));

        var result = Import(zip);
        Assert.Empty(result.Errors);
        Assert.Equal("A. Anna – Mitgliedsbeitrag", result.Transactions[0].Text);
        Assert.Equal("B. Beat – Verbandsbeitrag", result.Transactions[1].Text);
    }

    [Fact]
    public void A_batch_entry_becomes_one_transaction_per_TxDtls_with_own_amount_text_and_reference()
    {
        var ntry = BatchNtry(
            "150.00",
            "DBIT",
            "2026-02-10",
            ("B1", "100.00", "Miete Februar", null),
            ("B2", "50.00", "Strom", null)
        );
        var zip = CamtTestFiles.Zip(("20260210.xml", CamtFile("20260210", ClubIban, ntry)));

        var result = Import(zip);
        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Transactions.Count);

        var first = result.Transactions[0];
        Assert.Equal(new DateOnly(2026, 2, 10), first.Date);
        Assert.Equal(-100.00m, first.Amount);
        Assert.Equal("Miete Februar", first.Text);
        Assert.Equal("B1", first.BankReference);

        var second = result.Transactions[1];
        Assert.Equal(-50.00m, second.Amount);
        Assert.Equal("Strom", second.Text);
        Assert.Equal("B2", second.BankReference);
    }

    [Fact]
    public void A_batch_whose_TxDtls_amounts_do_not_add_up_to_the_entry_amount_is_an_error()
    {
        var ntry = BatchNtry(
            "150.00",
            "DBIT",
            "2026-02-10",
            ("B1", "100.00", "Miete Februar", null),
            ("B2", "40.00", "Strom", null)
        );
        var zip = CamtTestFiles.Zip(("20260210.xml", CamtFile("20260210", ClubIban, ntry)));

        var result = Import(zip);
        var error = Assert.Single(result.Errors);
        Assert.Equal("20260210.xml", error.File);
        Assert.Contains("addieren", error.Message);
        Assert.Empty(result.Transactions);
    }

    [Fact]
    public void Re_importing_a_batch_skips_the_parts_that_are_already_stored()
    {
        var ntry = BatchNtry(
            "150.00",
            "DBIT",
            "2026-02-10",
            ("B1", "100.00", "Miete Februar", null),
            ("B2", "50.00", "Strom", null)
        );
        var zip = CamtTestFiles.Zip(("20260210.xml", CamtFile("20260210", ClubIban, ntry)));

        var result = Import(zip, ["B1"]);
        var transaction = Assert.Single(result.Transactions);
        Assert.Equal("B2", transaction.BankReference);
        Assert.Equal(-50.00m, transaction.Amount);
        Assert.Equal(1, result.SkippedDuplicates);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void A_zip_with_more_than_100_xml_files_is_rejected()
    {
        var files = Enumerable
            .Range(1, 101)
            .Select(i =>
                (
                    $"{i:000}.xml",
                    CamtFile(
                        $"{i:000}",
                        ClubIban,
                        TxDtlsNtry("1.00", "CRDT", "2026-02-10", $"TX{i}")
                    )
                )
            )
            .ToArray();

        var result = Import(CamtTestFiles.Zip(files));
        var error = Assert.Single(result.Errors);
        Assert.Null(error.File);
        Assert.Contains("100", error.Message);
        Assert.Empty(result.Transactions);
    }

    [Fact]
    public void A_zip_with_exactly_100_xml_files_is_accepted()
    {
        var files = Enumerable
            .Range(1, 100)
            .Select(i =>
                (
                    $"{i:000}.xml",
                    CamtFile(
                        $"{i:000}",
                        ClubIban,
                        TxDtlsNtry("1.00", "CRDT", "2026-02-10", $"TX{i}")
                    )
                )
            )
            .ToArray();

        var result = Import(CamtTestFiles.Zip(files));
        Assert.Equal(100, result.Transactions.Count);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void A_zip_with_more_than_10_MB_uncompressed_is_rejected()
    {
        var zip = CamtTestFiles.Zip(("big.xml", new string('a', 10 * 1024 * 1024 + 1)));

        var result = Import(zip);
        var error = Assert.Single(result.Errors);
        Assert.Null(error.File);
        Assert.Contains("10 MB", error.Message);
        Assert.Empty(result.Transactions);
    }

    [Fact]
    public void A_file_with_a_DTD_is_rejected_naming_the_file()
    {
        var document = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE Stmt [
              <!ENTITY status "BOOK">
            ]>
            <Stmt xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.08">
              <Id>20260210</Id>
              <Acct>
                <Id>
                  <IBAN>{ClubIban}</IBAN>
                </Id>
              </Acct>
              <Ntry>
                <Amt Ccy="CHF">100.00</Amt>
                <CdtDbtInd>CRDT</CdtDbtInd>
                <Sts>&status;</Sts>
                <BookgDt><Dt>2026-02-10</Dt></BookgDt>
                <AcctSvcrRef>TX1</AcctSvcrRef>
              </Ntry>
            </Stmt>
            """;
        var zip = CamtTestFiles.Zip(("20260210.xml", document));

        var result = Import(zip);
        var error = Assert.Single(result.Errors);
        Assert.Equal("20260210.xml", error.File);
        Assert.Empty(result.Transactions);
    }

    [Fact]
    public void Non_xml_entries_and_directories_are_ignored()
    {
        var zip = CamtTestFiles.Zip(
            ("readme.txt", "Kein XML"),
            ("documents.xml/", ""),
            ("20260210/", ""),
            (
                "20260210.xml",
                CamtFile("20260210", ClubIban, TxDtlsNtry("100.00", "CRDT", "2026-02-10", "TX1"))
            )
        );

        var result = Import(zip);
        Assert.Single(result.Transactions);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Non_xml_entries_are_ignored()
    {
        var zip = CamtTestFiles.Zip(
            ("readme.txt", "Kein XML"),
            (
                "20260210.xml",
                CamtFile("20260210", ClubIban, TxDtlsNtry("100.00", "CRDT", "2026-02-10", "TX1"))
            )
        );

        var result = Import(zip);
        Assert.Single(result.Transactions);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void A_zip_without_xml_entries_is_an_error()
    {
        var zip = CamtTestFiles.Zip(("readme.txt", "Kein XML"));

        var error = Assert.Single(Import(zip).Errors);
        Assert.Null(error.File);
        Assert.Contains("CAMT.053", error.Message);
    }

    [Fact]
    public void A_matching_person_is_linked_to_the_transaction_without_the_name_prefix()
    {
        Person[] persons = [MakePerson(1, "Marianne", "Muster", "Bahnhofstr.", "7", "8004")];
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFile(
                    "20260210",
                    ClubIban,
                    AddressNtry("TX1", "M. Muster", "Bahnhofstrasse", "7", "8004")
                )
            )
        );

        var transaction = Assert.Single(
            BankImport.Import(zip, new ImportSnapshot(Settings, [], persons)).Transactions
        );
        Assert.Equal(1, transaction.PersonId);
        Assert.Equal("Jahresbeitrag", transaction.Text);
    }

    [Fact]
    public void An_unmatched_counterparty_keeps_the_name_prefix()
    {
        Person[] persons = [MakePerson(1, "Marianne", "Muster", "Bahnhofstr.", "7", "8004")];
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFile(
                    "20260210",
                    ClubIban,
                    AddressNtry("TX1", "M. Muster", "Marktgasse", "3", "8001")
                )
            )
        );

        var transaction = Assert.Single(
            BankImport.Import(zip, new ImportSnapshot(Settings, [], persons)).Transactions
        );
        Assert.Null(transaction.PersonId);
        Assert.Equal("M. Muster – Jahresbeitrag", transaction.Text);
    }

    [Fact]
    public void Umlauts_and_street_abbreviations_are_ignored_when_matching()
    {
        Person[] persons = [MakePerson(4, "Günter", "Müller", "Bergstr.", "12", "6300")];
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFile(
                    "20260210",
                    ClubIban,
                    AddressNtry("TX1", "G. Mueller", "Bergstrasse", "12", "6300")
                )
            )
        );

        var transaction = Assert.Single(
            BankImport.Import(zip, new ImportSnapshot(Settings, [], persons)).Transactions
        );
        Assert.Equal(4, transaction.PersonId);
        Assert.Equal("Jahresbeitrag", transaction.Text);
    }

    [Fact]
    public void A_person_without_a_house_number_is_not_matched()
    {
        Person[] persons = [MakePerson(1, "Marianne", "Muster", "Bahnhofstr.", null, "8004")];
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFile(
                    "20260210",
                    ClubIban,
                    AddressNtry("TX1", "M. Muster", "Bahnhofstrasse", "7", "8004")
                )
            )
        );

        var transaction = Assert.Single(
            BankImport.Import(zip, new ImportSnapshot(Settings, [], persons)).Transactions
        );
        Assert.Null(transaction.PersonId);
        Assert.Equal("M. Muster – Jahresbeitrag", transaction.Text);
    }

    [Fact]
    public void A_bank_address_without_a_house_number_is_not_matched()
    {
        Person[] persons = [MakePerson(1, "Marianne", "Muster", "Bahnhofstr.", "7", "8004")];
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFile(
                    "20260210",
                    ClubIban,
                    AddressNtry("TX1", "M. Muster", "Bahnhofstrasse", null, "8004")
                )
            )
        );

        var transaction = Assert.Single(
            BankImport.Import(zip, new ImportSnapshot(Settings, [], persons)).Transactions
        );
        Assert.Null(transaction.PersonId);
        Assert.Equal("M. Muster – Jahresbeitrag", transaction.Text);
    }

    [Fact]
    public void An_address_from_AdrLine_only_is_matched()
    {
        Person[] persons = [MakePerson(1, "Marianne", "Muster", "Bahnhofstr.", "7", "8004")];
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFile(
                    "20260210",
                    ClubIban,
                    AddressNtry("TX1", "M. Muster", null, null, null, "Bahnhofstr. 7", "8004 Zug")
                )
            )
        );

        var transaction = Assert.Single(
            BankImport.Import(zip, new ImportSnapshot(Settings, [], persons)).Transactions
        );
        Assert.Equal(1, transaction.PersonId);
        Assert.Equal("Jahresbeitrag", transaction.Text);
    }

    [Fact]
    public void A_StrtNm_without_a_BldgNb_splits_at_the_last_token_that_starts_with_a_digit()
    {
        Person[] persons = [MakePerson(1, "Marianne", "Muster", "Bahnhofstr.", "7", "8004")];
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFile(
                    "20260210",
                    ClubIban,
                    AddressNtry("TX1", "M. Muster", "Bahnhofstrasse 7", null, "8004")
                )
            )
        );

        var transaction = Assert.Single(
            BankImport.Import(zip, new ImportSnapshot(Settings, [], persons)).Transactions
        );
        Assert.Equal(1, transaction.PersonId);
    }

    [Fact]
    public void A_candidate_whose_first_name_is_in_the_bank_name_ranks_first()
    {
        Person[] persons =
        [
            MakePerson(1, "Hans", "Muster", "Bahnhofstr.", "7", "8004"),
            MakePerson(2, "Lina", "Muster", "Bahnhofstr.", "7", "8004"),
        ];
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFile(
                    "20260210",
                    ClubIban,
                    AddressNtry("TX1", "Lina Muster", "Bahnhofstrasse", "7", "8004")
                )
            )
        );

        var transaction = Assert.Single(
            BankImport.Import(zip, new ImportSnapshot(Settings, [], persons)).Transactions
        );
        Assert.Equal(2, transaction.PersonId);
    }

    [Fact]
    public void Without_a_first_name_hit_the_lowest_person_id_wins()
    {
        Person[] persons =
        [
            MakePerson(1, "Hans", "Muster", "Bahnhofstr.", "7", "8004"),
            MakePerson(2, "Lina", "Muster", "Bahnhofstr.", "7", "8004"),
        ];
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFile(
                    "20260210",
                    ClubIban,
                    AddressNtry("TX1", "Familie Muster", "Bahnhofstrasse", "7", "8004")
                )
            )
        );

        var transaction = Assert.Single(
            BankImport.Import(zip, new ImportSnapshot(Settings, [], persons)).Transactions
        );
        Assert.Equal(1, transaction.PersonId);
    }

    [Fact]
    public void A_former_member_is_matched()
    {
        Person[] persons =
        [
            MakePerson(
                7,
                "Erika",
                "Muster",
                "Bahnhofstr.",
                "7",
                "8004",
                leftOn: new DateOnly(2025, 12, 31)
            ),
        ];
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFile(
                    "20260210",
                    ClubIban,
                    AddressNtry("TX1", "Erika Muster", "Bahnhofstrasse", "7", "8004")
                )
            )
        );

        var transaction = Assert.Single(
            BankImport.Import(zip, new ImportSnapshot(Settings, [], persons)).Transactions
        );
        Assert.Equal(7, transaction.PersonId);
        Assert.Equal("Jahresbeitrag", transaction.Text);
    }

    [Fact]
    public void A_matched_credit_is_stored_with_its_positive_amount()
    {
        Person[] persons = [MakePerson(1, "Marianne", "Muster", "Bahnhofstr.", "7", "8004")];
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFile(
                    "20260210",
                    ClubIban,
                    AddressNtry("TX1", "M. Muster", "Bahnhofstrasse", "7", "8004")
                )
            )
        );

        var transaction = Assert.Single(
            BankImport.Import(zip, new ImportSnapshot(Settings, [], persons)).Transactions
        );
        Assert.Equal(1, transaction.PersonId);
        Assert.Equal(100.00m, transaction.Amount);
    }

    [Fact]
    public void A_plain_ntry_without_txdtls_has_no_person()
    {
        Person[] persons = [MakePerson(1, "Marianne", "Muster", "Bahnhofstr.", "7", "8004")];
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFile(
                    "20260210",
                    ClubIban,
                    PlainNtry("5.00", "DBIT", "2026-02-10", "E1", "Zahlungsverkehrspreise")
                )
            )
        );

        var transaction = Assert.Single(
            BankImport.Import(zip, new ImportSnapshot(Settings, [], persons)).Transactions
        );
        Assert.Null(transaction.PersonId);
        Assert.Equal("Zahlungsverkehrspreise", transaction.Text);
    }

    [Fact]
    public void A_different_PLZ_is_not_matched()
    {
        Person[] persons = [MakePerson(1, "Marianne", "Muster", "Bahnhofstr.", "7", "8004")];
        var zip = CamtTestFiles.Zip(
            (
                "20260210.xml",
                CamtFile(
                    "20260210",
                    ClubIban,
                    AddressNtry("TX1", "M. Muster", "Bahnhofstrasse", "7", "6300")
                )
            )
        );

        var transaction = Assert.Single(
            BankImport.Import(zip, new ImportSnapshot(Settings, [], persons)).Transactions
        );
        Assert.Null(transaction.PersonId);
        Assert.Equal("M. Muster – Jahresbeitrag", transaction.Text);
    }

    private static BankImportResult Import(byte[] zip, string[]? existingReferences = null)
    {
        return BankImport.Import(zip, new ImportSnapshot(Settings, existingReferences ?? [], []));
    }

    private static string CamtFile(string name, string iban, params string[] ntries) =>
        CamtFileIn("urn:iso:std:iso:20022:tech:xsd:camt.053.001.08", name, iban, ntries);

    private static string CamtFileIn(string ns, string name, string iban, params string[] ntries) =>
        $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Stmt xmlns="{ns}">
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
    private static string TxDtlsNtry(
        string amount,
        string direction,
        string date,
        string txRef,
        string? ustrd = null,
        string? addtl = null
    ) =>
        $"""
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

    /// <summary>One Ntry with several TxDtls, the shape of a batch payment: one debit paying several people. Each TxDtls may carry its own Cdtr.</summary>
    private static string BatchNtry(
        string ntryAmount,
        string direction,
        string date,
        params (string Ref, string Amount, string? Ustrd, string? Cdtr)[] details
    )
    {
        var txDtls = string.Join(
            "\n",
            details.Select(d =>
                $"""
                    <TxDtls>
                      <Refs><AcctSvcrRef>{d.Ref}</AcctSvcrRef></Refs>
                      <Amt Ccy="CHF">{d.Amount}</Amt>
                      {(d.Ustrd is null ? "" : $"<Ustrd>{d.Ustrd}</Ustrd>")}
                      {(d.Cdtr is null ? "" : $"<Cdtr><Pty><Nm>{d.Cdtr}</Nm></Pty></Cdtr>")}
                    </TxDtls>
                    """
            )
        );
        return $"""
            <Ntry>
              <Amt Ccy="CHF">{ntryAmount}</Amt>
              <CdtDbtInd>{direction}</CdtDbtInd>
              <Sts>BOOK</Sts>
              <BookgDt><Dt>{date}</Dt></BookgDt>
              {txDtls}
            </Ntry>
            """;
    }

    /// <summary>One Ntry without TxDtls, e.g. the fee entry "Zahlungsverkehrspreise".</summary>
    private static string PlainNtry(
        string amount,
        string direction,
        string date,
        string entryRef,
        string? ustrd = null,
        string? addtl = null
    ) =>
        $"""
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

    /// <summary>One Ntry with one TxDtls whose Dbtr carries the counterparty's name and postal address (the .08 shape).</summary>
    private static string AddressNtry(
        string txRef,
        string name,
        string? street,
        string? building,
        string? postalCode,
        params string[] addressLines
    ) =>
        $"""
            <Ntry>
              <Amt Ccy="CHF">100.00</Amt>
              <CdtDbtInd>CRDT</CdtDbtInd>
              <Sts>BOOK</Sts>
              <BookgDt><Dt>2026-02-10</Dt></BookgDt>
              <TxDtls>
                <Refs><AcctSvcrRef>{txRef}</AcctSvcrRef></Refs>
                <Amt Ccy="CHF">100.00</Amt>
                <Dbtr>
                  <Pty>
                    <Nm>{name}</Nm>
                    <PstA>
                      {(street is null ? "" : $"<StrtNm>{street}</StrtNm>")}
                      {(building is null ? "" : $"<BldgNb>{building}</BldgNb>")}
                      {(postalCode is null ? "" : $"<PstCd>{postalCode}</PstCd>")}
                      {string.Join("\n", addressLines.Select(line => $"<AdrLine>{line}</AdrLine>"))}
                    </PstA>
                  </Pty>
                </Dbtr>
                <Ustrd>Jahresbeitrag</Ustrd>
              </TxDtls>
            </Ntry>
            """;

    /// <summary>A fictional Person for the matching tests.</summary>
    private static Person MakePerson(
        int id,
        string firstName,
        string lastName,
        string? street,
        string? number,
        string? postalCode,
        DateOnly? leftOn = null
    ) =>
        new()
        {
            Id = id,
            FirstName = firstName,
            LastName = lastName,
            Street = street,
            Number = number,
            PostalCode = postalCode,
            City = "Zug",
            LeftOn = leftOn,
        };
}
