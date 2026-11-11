namespace backend.Tests.Accounting;

/// <summary>
/// Shared CAMT.053 XML helpers for the bank import tests: one statement file plus the Ntry shapes the
/// import tests build (one TxDtls, a TxDtls with a QR-bill reference, a batch, a plain entry without
/// TxDtls, and a counterparty entry with a postal address).
/// </summary>
public static class CamtTestHelpers
{
    /// <summary>A CAMT.053 statement file with the given IBAN and Ntries.</summary>
    public static string CamtFile(string name, string iban, params string[] ntries) =>
        CamtFileIn("urn:iso:std:iso:20022:tech:xsd:camt.053.001.08", name, iban, ntries);

    /// <summary>A CAMT.053 statement file in an arbitrary namespace (the .10 namespace tests rename it in memory).</summary>
    public static string CamtFileIn(string ns, string name, string iban, params string[] ntries) =>
        $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Document xmlns="{ns}">
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

    /// <summary>One Ntry with exactly one TxDtls, the shape the ZKB eBanking sends.</summary>
    public static string TxDtlsNtry(
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
              <Sts><Cd>BOOK</Cd></Sts>
              <BookgDt><Dt>{date}</Dt></BookgDt>
              <NtryDtls><TxDtls>
                <Refs><AcctSvcrRef>{txRef}</AcctSvcrRef></Refs>
                <Amt Ccy="CHF">{amount}</Amt>
                {(ustrd is null ? "" : $"<RmtInf><Ustrd>{ustrd}</Ustrd></RmtInf>")}
              </TxDtls></NtryDtls>
              {(addtl is null ? "" : $"<AddtlNtryInf>{addtl}</AddtlNtryInf>")}
            </Ntry>
            """;

    /// <summary>One Ntry with exactly one TxDtls, with a QR-bill CdtrRefInf reference.</summary>
    public static string TxDtlsNtryWithReference(
        string amount,
        string direction,
        string date,
        string txRef,
        string qrReference,
        string? ustrd = null
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
                  {(ustrd is null ? "" : $"<Ustrd>{ustrd}</Ustrd>")}
                  <Strd>
                    <CdtrRefInf>
                      <Ref>{qrReference}</Ref>
                    </CdtrRefInf>
                  </Strd>
                </RmtInf>
              </TxDtls></NtryDtls>
            </Ntry>
            """;

    /// <summary>One Ntry with several TxDtls, the shape of a batch payment: one debit paying several people. Each TxDtls may carry its own Cdtr.</summary>
    public static string BatchNtry(
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
                    <NtryDtls><TxDtls>
                      <Refs><AcctSvcrRef>{d.Ref}</AcctSvcrRef></Refs>
                      <Amt Ccy="CHF">{d.Amount}</Amt>
                      {(d.Cdtr is null ? "" : $"<RltdPties><Cdtr><Pty><Nm>{d.Cdtr}</Nm></Pty></Cdtr></RltdPties>")}
                      {(d.Ustrd is null ? "" : $"<RmtInf><Ustrd>{d.Ustrd}</Ustrd></RmtInf>")}
                    </TxDtls></NtryDtls>
                    """
            )
        );
        return $"""
            <Ntry>
              <Amt Ccy="CHF">{ntryAmount}</Amt>
              <CdtDbtInd>{direction}</CdtDbtInd>
              <Sts><Cd>BOOK</Cd></Sts>
              <BookgDt><Dt>{date}</Dt></BookgDt>
              {txDtls}
            </Ntry>
            """;
    }

    /// <summary>One Ntry without TxDtls, e.g. the fee entry "Zahlungsverkehrspreise".</summary>
    public static string PlainNtry(
        string amount,
        string direction,
        string date,
        string entryRef,
        string? addtl = null
    ) =>
        $"""
            <Ntry>
              <Amt Ccy="CHF">{amount}</Amt>
              <CdtDbtInd>{direction}</CdtDbtInd>
              <Sts><Cd>BOOK</Cd></Sts>
              <BookgDt><Dt>{date}</Dt></BookgDt>
              <AcctSvcrRef>{entryRef}</AcctSvcrRef>
              {(addtl is null ? "" : $"<AddtlNtryInf>{addtl}</AddtlNtryInf>")}
            </Ntry>
            """;

    /// <summary>One Ntry with one TxDtls whose Dbtr carries the counterparty's name and postal address (the .08 shape).</summary>
    public static string AddressNtry(
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
              <Sts><Cd>BOOK</Cd></Sts>
              <BookgDt><Dt>2026-02-10</Dt></BookgDt>
              <NtryDtls><TxDtls>
                <Refs><AcctSvcrRef>{txRef}</AcctSvcrRef></Refs>
                <Amt Ccy="CHF">100.00</Amt>
                <RltdPties>
                <Dbtr>
                  <Pty>
                    <Nm>{name}</Nm>
                    <PstlAdr>
                      {(street is null ? "" : $"<StrtNm>{street}</StrtNm>")}
                      {(building is null ? "" : $"<BldgNb>{building}</BldgNb>")}
                      {(postalCode is null ? "" : $"<PstCd>{postalCode}</PstCd>")}
                      {string.Join("\n", addressLines.Select(line => $"<AdrLine>{line}</AdrLine>"))}
                    </PstlAdr>
                  </Pty>
                </Dbtr>
                </RltdPties>
                <RmtInf>
                <Ustrd>Jahresbeitrag</Ustrd>
                </RmtInf>
              </TxDtls></NtryDtls>
            </Ntry>
            """;
}
