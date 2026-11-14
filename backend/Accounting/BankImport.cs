using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace backend.Accounting;

using Camt = Iso20022.Contracts.Camt05300110;
using Person = backend.Person.Person;
using backend.Invoicing;

/// <summary>A position of an open Rechnung as seen by the import: its Kontoplan hint and amount.</summary>
public record ImportInvoiceLine(string Text, int AccountNumber, decimal Amount);

/// <summary>An open Rechnung as seen by the import: the QR-bill reference, the total, the positions, how much is already paid, and whether it is already Bezahlt (terminal: it never matches again).</summary>
public record ImportInvoice(
    int Id,
    string Reference,
    decimal Total,
    decimal ExistingPaid,
    bool AlreadyPaid,
    IReadOnlyList<ImportInvoiceLine> Lines
);

/// <summary>A Buchung the import proposes for a matched Bewegung: the position text, the Kontoplan account and the signed amount.</summary>
public record ImportEntry(string Text, int AccountNumber, decimal Amount);

/// <summary>
/// The state the bank import validates against: the club settings (Klub-IBAN, Startdatum), the
/// BankReferences already stored, all Persons (including former members) for counterparty matching, the
/// open Rechnungen and their positions (for matching a payment to its Rechnung), and the Kontoplan
/// (to resolve the Konten the auto-entries would book to).
/// </summary>
public record ImportSnapshot(
    Settings Settings,
    IReadOnlyCollection<string> ExistingBankReferences,
    IReadOnlyCollection<Person> Persons,
    IReadOnlyCollection<ImportInvoice> Invoices,
    ChartOfAccounts? Chart
)
{
}

/// <summary>One imported Bewegung to save: the bank's booking date, the signed amount, the text, the BankReference and, when the counterparty matched, the PersonId. A matched Bewegung additionally carries the Rechnung it covers, the Buchungen the import proposes for it, — when this Bewegung overpays, the excess booked to Konto 3600 (Spenden) — and, when this Bewegung is the first to cover the total, the UTC timestamp of the Paid status change (the movement's booking date in Zurich time).</summary>
public record ImportedTransaction(
    DateOnly Date,
    decimal Amount,
    string? Text,
    string BankReference,
    int? PersonId,
    int? MatchedInvoiceId,
    IReadOnlyList<ImportEntry> AutoEntries,
    decimal? OverpaymentAmount,
    DateTime? PaidAt
);

/// <summary>One problem found in one file; File is null for a problem of the zip itself.</summary>
public record ImportError(string? File, string Message);

/// <summary>The outcome of the bank import: the new Bewegungen plus the skipped duplicates, or the per-file errors. When Errors is non-empty, nothing may be saved.</summary>
public record BankImportResult(
    List<ImportedTransaction> Transactions,
    int SkippedDuplicates,
    List<ImportError> Errors
);

/// <summary>
/// The pure bank import: reads every CAMT.053 file of an eBanking zip entirely in memory and creates one
/// Bewegung per transaction: an entry without TxDtls, or one of its TxDtls for a batch entry.
/// It does no database access, parses the CAMT.053 files with the Iso20022 library's contracts, and never uses
/// entry names as file paths. The counterparty of each transaction is linked to the matching Person when the bank
/// address matches; the parser and the matcher are internal details of this module.
/// </summary>
public static class BankImport
{
    public static readonly XNamespace Namespace = "urn:iso:std:iso:20022:tech:xsd:camt.053.001.08";

    /// <summary>The namespace of the contracts the Iso20022 library provides.</summary>
    private static readonly XNamespace LibraryNamespace =
        "urn:iso:std:iso:20022:tech:xsd:camt.053.001.10";

    private static readonly XmlSerializer Serializer = new(typeof(Camt.Document));

    /// <summary>Safety limits for the eBanking zip: at most this many XML files...</summary>
    private const int MaxXmlFiles = 100;

    /// <summary>...and about this many bytes in total, uncompressed.</summary>
    private const long MaxUncompressedBytes = 10 * 1024 * 1024;

    /// <summary>The Kontoplan account the overpayment of a matched credit is booked to (Spenden).</summary>
    private const int OverpaymentAccount = 3600;

    /// <summary>The maximum length of a Bewegung text, the same limit as the Text column.</summary>
    private const int MaxTextLength = 500;

    /// <summary>The minimum similarity for a street or house number to count as nearly identical.</summary>
    private const double MinimumSimilarity = 0.9;

    /// <summary>DTDs are prohibited, which also disables external entities.</summary>
    private static readonly XmlReaderSettings XmlReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
    };

    public static BankImportResult Import(byte[] zip, ImportSnapshot snapshot)
    {
        var context = new ImportContext(snapshot);

        ZipArchive archive;
        try
        {
            archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        }
        catch (InvalidDataException)
        {
            return new BankImportResult(
                [],
                0,
                [new ImportError(null, "Die ZIP-Datei ist nicht gültig.")]
            );
        }

        using (archive)
        {
            // Directories and non-.xml entries of the eBanking zip are ignored.
            var xmlEntries = archive
                .Entries.Where(e =>
                    !e.FullName.EndsWith('/')
                    && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                )
                .ToList();
            if (xmlEntries.Count == 0)
            {
                return new BankImportResult(
                    [],
                    0,
                    [new ImportError(null, "Keine Datei in der ZIP gefunden.")]
                );
            }
            if (xmlEntries.Count > MaxXmlFiles)
            {
                return new BankImportResult(
                    [],
                    0,
                    [
                        new ImportError(
                            null,
                            $"Die ZIP-Datei enthält {xmlEntries.Count} XML-Dateien, aber es werden maximal {MaxXmlFiles} akzeptiert."
                        ),
                    ]
                );
            }
            if (archive.Entries.Sum(e => e.Length) > MaxUncompressedBytes)
            {
                return new BankImportResult(
                    [],
                    0,
                    [
                        new ImportError(
                            null,
                            $"Die ZIP-Datei ist nach dem Entpacken zu gross (maximal {MaxUncompressedBytes / 1024 / 1024} MB)."
                        ),
                    ]
                );
            }

            foreach (var entry in xmlEntries)
            {
                XDocument document;
                try
                {
                    using var stream = entry.Open();
                    using var reader = XmlReader.Create(stream, XmlReaderSettings);
                    document = XDocument.Load(reader);
                }
                catch (Exception e) when (e is XmlException or InvalidDataException)
                {
                    context.Errors.Add(
                        new ImportError(
                            entry.FullName,
                            e.Message.Contains("DTD", StringComparison.OrdinalIgnoreCase)
                                ? "Die Datei enthält eine DTD und wird abgelehnt."
                                : $"Die Datei konnte nicht als XML gelesen werden: {e.Message}"
                        )
                    );
                    continue;
                }
                ParseStatement(document, entry.FullName, context);
            }
        }

        return context.Errors.Count > 0
            ? new BankImportResult([], 0, context.Errors)
            : new BankImportResult(context.Transactions, context.SkippedDuplicates, []);
    }


    /// <summary>
    /// Reads one CAMT.053 file into the library's contracts. The library has no camt.053.001.08 contracts, only
    /// camt.053.001.10, which has the same shape for everything read here; so a .08 file is renamed to the .10
    /// namespace in memory and then deserialized.
    /// </summary>
    private static Camt.Document? ReadDocument(XDocument xml, string file, ImportContext context)
    {
        var root = xml.Root;
        if (root == null)
        {
            context.Errors.Add(new ImportError(file, "Hat keine root Dokument"));
            return null;
        }

        var ns = root.Name.Namespace;
        if (ns != Namespace && ns != LibraryNamespace)
        {
            context.Errors.Add(
                new ImportError(
                    file,
                    string.IsNullOrEmpty(ns.NamespaceName)
                        ? "Die Datei ist keine CAMT.053-Datei (Namespace fehlt)."
                        : $"Die Datei ist keine CAMT.053-Datei (Namespace {ns.NamespaceName} ist kein CAMT.053-Namespace)."
                )
            );
            return null;
        }
        foreach (var element in root.DescendantsAndSelf().Where(e => e.Name.Namespace == Namespace))
        {
            element.Name = LibraryNamespace + element.Name.LocalName;
        }

        // CdtDbtInd is a plain enum in the contracts, so an absent one would silently read as CRDT.
        if (
            root.Descendants(LibraryNamespace + "Ntry")
                .Any(ntry => ntry.Element(LibraryNamespace + "CdtDbtInd") is null)
        )
        {
            context.Errors.Add(
                new ImportError(
                    file,
                    "Es fehlt die Gutschrift-/Lastschriftkennzeichnung (CdtDbtInd)."
                )
            );
            return null;
        }

        try
        {
            return (Camt.Document?)Serializer.Deserialize(xml.CreateReader());
        }
        catch (InvalidOperationException e)
        {
            context.Errors.Add(
                new ImportError(
                    file,
                    $"Die Datei ist keine gültige CAMT.053-Datei: {e.InnerException?.Message ?? e.Message}"
                )
            );
            return null;
        }
    }

    private static void ParseStatement(XDocument xml, string file, ImportContext context)
    {
        var document = ReadDocument(xml, file, context);
        if (document is null)
        {
            return;
        }

        var stmts = document.BkToCstmrStmt?.Stmt ?? [];
        if (stmts.Length != 1)
        {
            context.Errors.Add(new ImportError(file, "Erwartet 1 Stmt, but was " + stmts.Length));
            return;
        }
        var stmt = stmts[0];

        var fileIban = Iban.Normalize(stmt.Acct?.Id?.Item as string);
        if (string.IsNullOrEmpty(fileIban))
        {
            context.Errors.Add(
                new ImportError(file, "Es fehlt die Konto-IBAN (Stmt/Acct/Id/IBAN).")
            );
            return;
        }
        var clubIban = Iban.Normalize(context.Snapshot.Settings.Iban)!;
        if (fileIban != clubIban)
        {
            context.Errors.Add(
                new ImportError(
                    file,
                    $"Die IBAN {fileIban} in der Datei stimmt nicht mit der Klub-IBAN {clubIban} überein."
                )
            );
            return;
        }

        var ntries = stmt.Ntry ?? [];
        if (ntries.Length == 0)
        {
            context.Errors.Add(new ImportError(file, "Die Datei enthält keine Bewegungen (Ntry)."));
            return;
        }
        foreach (var ntry in ntries)
        {
            ParseEntry(ntry, file, context);
        }
    }

    private static void ParseEntry(Camt.ReportEntry12 ntry, string file, ImportContext context)
    {
        var txDtls = (ntry.NtryDtls ?? []).SelectMany(d => d.TxDtls ?? []).ToList();

        var status = ntry.Sts?.Item?.Trim();
        if (string.IsNullOrWhiteSpace(status))
        {
            context.Errors.Add(new ImportError(file, "Es fehlt der Status (Sts)."));
            return;
        }
        if (status != "BOOK")
        {
            context.Errors.Add(
                new ImportError(
                    file,
                    $"Der Eintrag hat den Status {status}, aber nur abschließende Bewegungen (BOOK) werden importiert."
                )
            );
            return;
        }
        if (ntry.RvslInd)
        {
            context.Errors.Add(
                new ImportError(
                    file,
                    "Der Eintrag ist eine Stornierung (RvslInd) und wird nicht importiert."
                )
            );
            return;
        }

        var direction = ntry.CdtDbtInd;

        if (ntry.Amt is null)
        {
            context.Errors.Add(new ImportError(file, "Es fehlt der Betrag (Amt)."));
            return;
        }

        if (ntry.BookgDt is null)
        {
            context.Errors.Add(new ImportError(file, "Es fehlt das Buchungsdatum (BookgDt/Dt)."));
            return;
        }
        var date = DateOnly.FromDateTime(ntry.BookgDt.Item);

        if (txDtls.Count > 1)
        {
            // A batch entry: one Bewegung per TxDtls; the sign and the booking date come from the entry.
            var txAmounts = new decimal[txDtls.Count];
            for (var i = 0; i < txDtls.Count; i++)
            {
                var txAmount = AmountOf(txDtls[i]);
                if (txAmount is null)
                {
                    context.Errors.Add(new ImportError(file, "Es fehlt der Betrag (Amt)."));
                    return;
                }
                txAmounts[i] = txAmount.Value;
            }

            if (txAmounts.Sum() != ntry.Amt.Value)
            {
                context.Errors.Add(
                    new ImportError(
                        file,
                        $"Die Beträge der einzelnen Bewegungen addieren sich nicht auf den Bewegungsbetrag der Gruppe (Ntry/Amt): {txAmounts.Sum().ToString("0.00", CultureInfo.InvariantCulture)} statt {ntry.Amt.Value.ToString("0.00", CultureInfo.InvariantCulture)}."
                    )
                );
                return;
            }

            for (var i = 0; i < txDtls.Count; i++)
            {
                ParseTransaction(ntry, txDtls[i], txAmounts[i], direction, date, file, context);
            }
            return;
        }

        // The simple entry: exactly one TxDtls, or none (then the entry itself is the transaction).
        var transaction = txDtls.Count == 1 ? txDtls[0] : null;
        var amount = transaction is null ? ntry.Amt.Value : AmountOf(transaction);
        if (amount is null)
        {
            context.Errors.Add(new ImportError(file, "Es fehlt der Betrag (Amt)."));
            return;
        }

        ParseTransaction(ntry, transaction, amount.Value, direction, date, file, context);
    }

    /// <summary>
    /// One Bewegung for one transaction: for a simple entry the entry itself (transaction is null when it has
    /// no TxDtls), for a batch entry one of its TxDtls. The transaction's own amount, Ustrd text and
    /// TxDtls/Refs/AcctSvcrRef are used; the sign comes from the entry's CdtDbtInd.
    /// </summary>
    private static void ParseTransaction(
        Camt.ReportEntry12 ntry,
        Camt.EntryTransaction12? transaction,
        decimal amount,
        Camt.CreditDebitCode direction,
        DateOnly date,
        string file,
        ImportContext context
    )
    {
        var bankReference = (transaction?.Refs?.AcctSvcrRef ?? ntry.AcctSvcrRef)?.Trim();
        if (string.IsNullOrEmpty(bankReference))
        {
            context.Errors.Add(new ImportError(file, "Es fehlt die Bankreferenz (AcctSvcrRef)."));
            return;
        }

        var credit = direction == Camt.CreditDebitCode.CRDT;
        var signedAmount = credit ? amount : -amount;
        // An imported Bewegung has no Buchungen, so only the date and amount rules apply.
        if (TransactionRules.Validate(context.Snapshot.Settings, date, signedAmount) is { } error)
        {
            context.Errors.Add(new ImportError(file, error));
            return;
        }

        if (!context.SeenReferences.Add(bankReference))
        {
            context.SkippedDuplicates++;
            return;
        }

        var ustrd = (transaction?.RmtInf?.Ustrd ?? [])
            .Select(v => v.Trim())
            .Where(v => v.Length > 0)
            .ToList();
        var addtlNtryInf = ntry.AddtlNtryInf?.Trim();
        var text =
            ustrd.Count > 0
                ? string.Join(' ', ustrd)
                : (string.IsNullOrEmpty(addtlNtryInf) ? null : addtlNtryInf);
        var counterpartyParty = CounterpartyPartyOf(transaction, credit);
        var counterparty = CounterpartyNameOf(counterpartyParty);
        var person = MatchPerson(
            counterparty,
            BankAddressOf(counterpartyParty),
            context.Snapshot.Persons
        );
        // A matched counterparty is linked to its Person and the text is kept as-is; otherwise the name goes in front.
        if (person is null && text is not null && counterparty is not null)
        {
            text = $"{counterparty} – {text}";
        }
        // The Text column is varchar(500), so the final text must fit it, prefixed or not.
        text = text is null ? null : (text.Length > MaxTextLength ? text[..MaxTextLength] : text);

        // Try to match the payment to its open Rechnung and auto-book it.
        var match = MatchInvoice(transaction, signedAmount, credit, date, context.Snapshot);
        context.Transactions.Add(
            new ImportedTransaction(
                date,
                signedAmount,
                text,
                bankReference,
                person?.Id,
                match?.InvoiceId,
                match?.Entries ?? [],
                match?.OverpaymentAmount,
                match?.PaidAt
            )
        );
    }

    /// <summary>The amount of a transaction: its own Amt, or else AmtDtls/TxAmt/Amt.</summary>
    private static decimal? AmountOf(Camt.EntryTransaction12 transaction) =>
        (transaction.Amt ?? transaction.AmtDtls?.TxAmt?.Amt)?.Value;

    /// <summary>The counterparty party of a transaction: the Dbtr (payer) for a credit, the Cdtr (payee) for a debit.</summary>
    private static Camt.PartyIdentification135? CounterpartyPartyOf(
        Camt.EntryTransaction12? transaction,
        bool credit
    ) => (credit ? transaction?.RltdPties?.Dbtr : transaction?.RltdPties?.Cdtr)?.Item as Camt.PartyIdentification135;

    private static string? CounterpartyNameOf(Camt.PartyIdentification135? party)
    {
        var name = party?.Nm?.Trim();
        return string.IsNullOrEmpty(name) ? null : name;
    }

    /// <summary>The normalised PLZ, street and house number of a bank counterparty's postal address.</summary>
    private sealed record BankAddress(string PostalCode, string Street, string Number);

    /// <summary>
    /// The postal address (PstlAdr) of a party, reduced to what matching needs: the PLZ,
    /// the street and the house number, all normalised. Street and number come from StrtNm plus BldgNb; without
    /// a BldgNb, StrtNm is split at the last token that starts with a digit; otherwise the first AdrLine that
    /// looks like street plus number is used. The PLZ comes from PstCd, or else from an AdrLine of the form
    /// "&lt;PLZ&gt; &lt;Ort&gt;". Null when any of the three is missing on the bank side.
    /// </summary>
    private static BankAddress? BankAddressOf(Camt.PartyIdentification135? party)
    {
        var address = party?.PstlAdr;
        if (address is null)
        {
            return null;
        }

        var streetName = address.StrtNm?.Trim();
        var buildingNumber = address.BldgNb?.Trim();
        var addressLines = (address.AdrLine ?? [])
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        string? street = null;
        string? number = null;
        if (!string.IsNullOrEmpty(streetName) && !string.IsNullOrEmpty(buildingNumber))
        {
            street = streetName;
            number = buildingNumber;
        }
        else if (!string.IsNullOrEmpty(streetName))
        {
            var tokens = streetName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var splitAt = IndexOfLastDigitToken(tokens);
            if (splitAt > 0)
            {
                street = string.Join(' ', tokens[..splitAt]);
                number = tokens[splitAt];
            }
        }
        if (street is null)
        {
            foreach (var line in addressLines)
            {
                var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (
                    tokens.Length >= 2
                    && char.IsDigit(tokens[^1][0])
                    && tokens[..^1].Any(t => !char.IsDigit(t[0]))
                )
                {
                    street = string.Join(' ', tokens[..^1]);
                    number = tokens[^1];
                    break;
                }
            }
        }

        var postalCode = address.PstCd?.Trim();
        if (string.IsNullOrEmpty(postalCode))
        {
            postalCode = PostalCodeFromAddressLines(addressLines);
        }

        if (
            string.IsNullOrEmpty(street)
            || string.IsNullOrEmpty(number)
            || string.IsNullOrEmpty(postalCode)
        )
        {
            return null;
        }

        return new BankAddress(Normalize(postalCode), NormalizeStreet(street), Normalize(number));
    }

    /// <summary>The index of the last token that starts with a digit, or -1 when there is none.</summary>
    private static int IndexOfLastDigitToken(string[] tokens)
    {
        for (var i = tokens.Length - 1; i >= 0; i--)
        {
            if (char.IsDigit(tokens[i][0]))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>The PLZ of the first AdrLine of the form "&lt;PLZ&gt; &lt;Ort&gt;", or null when there is none.</summary>
    private static string? PostalCodeFromAddressLines(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (
                tokens.Length >= 2
                && tokens[0].Length is >= 4 and <= 5
                && tokens[0].All(char.IsDigit)
            )
            {
                return tokens[0];
            }
        }
        return null;
    }

    /// <summary>
    /// The Person whose address matches the counterparty's bank address: the PLZ is equal, the street and the
    /// house number are nearly identical (similarity at least 0.9) and the normalised last name appears in the
    /// normalised bank name. All Persons are candidates, including former members; a candidate whose first name
    /// is also in the bank name ranks first, and within the same rank the lowest PersonId wins.
    /// </summary>
    private static Person? MatchPerson(
        string? bankName,
        BankAddress? bankAddress,
        IReadOnlyCollection<Person> persons
    )
    {
        if (bankAddress is null)
        {
            return null;
        }

        var normalizedBankName = bankName is null ? null : Normalize(bankName);
        var best = (Person?)null;
        var bestFirstHit = false;
        foreach (var person in persons)
        {
            if (!Matches(person, bankAddress, normalizedBankName))
            {
                continue;
            }

            var firstHit =
                !string.IsNullOrEmpty(person.FirstName)
                && normalizedBankName is not null
                && normalizedBankName.Contains(
                    Normalize(person.FirstName),
                    StringComparison.Ordinal
                );
            if (
                best is null
                || (firstHit && !bestFirstHit)
                || (firstHit == bestFirstHit && person.Id < best.Id)
            )
            {
                best = person;
                bestFirstHit = firstHit;
            }
        }
        return best;
    }

    private static bool Matches(Person person, BankAddress address, string? bankName)
    {
        if (
            string.IsNullOrEmpty(person.PostalCode)
            || string.IsNullOrEmpty(person.Street)
            || string.IsNullOrEmpty(person.Number)
        )
        {
            return false;
        }

        var lastName = Normalize(person.LastName);
        if (
            lastName.Length == 0
            || bankName is null
            || !bankName.Contains(lastName, StringComparison.Ordinal)
        )
        {
            return false;
        }

        return Normalize(person.PostalCode) == address.PostalCode
            && Similarity(NormalizeStreet(person.Street), address.Street) >= MinimumSimilarity
            && Similarity(Normalize(person.Number), address.Number) >= MinimumSimilarity;
    }

    /// <summary>The similarity of two strings: 1 minus the Levenshtein distance divided by the length of the longer string; two empty strings are identical.</summary>
    private static double Similarity(string a, string b)
    {
        var longer = Math.Max(a.Length, b.Length);
        return longer == 0 ? 1 : 1 - (double)LevenshteinDistance(a, b) / longer;
    }

    /// <summary>The Levenshtein distance by dynamic programming over one row; address parts are short strings.</summary>
    private static int LevenshteinDistance(string a, string b)
    {
        var distance = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            distance[j] = j;
        }
        for (var i = 1; i <= a.Length; i++)
        {
            var previous = distance[0];
            distance[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var current = distance[j];
                distance[j] = Math.Min(
                    distance[j] + 1,
                    Math.Min(distance[j - 1] + 1, previous + (a[i - 1] == b[j - 1] ? 0 : 1))
                );
                previous = current;
            }
        }
        return distance[b.Length];
    }

    /// <summary>The whitespace characters collapsed when normalising.</summary>
    private static readonly char[] Whitespace = [' ', '\t', '\n', '\r', '\f', '\v'];

    /// <summary>
    /// Normalises both sides of the match: lower case, trimmed, whitespace collapsed, accents stripped (which
    /// folds the umlauts), ß becomes ss, and the umlaut spellings ae/oe/ue become the single letter.
    /// </summary>
    private static string Normalize(string value)
    {
        var collapsed = string.Join(
            ' ',
            value.ToLowerInvariant().Split(Whitespace, StringSplitOptions.RemoveEmptyEntries)
        );
        var withoutAccents = new string(
            collapsed
                .Normalize(NormalizationForm.FormD)
                .Where(c => char.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray()
        );
        return withoutAccents
            .Replace("ß", "ss")
            .Replace("ae", "a")
            .Replace("oe", "o")
            .Replace("ue", "u");
    }

    /// <summary>Normalises a street name and expands the street abbreviations as whole tokens or word endings.</summary>
    private static string NormalizeStreet(string value) =>
        string.Join(' ', Normalize(value).Split(' ').Select(ExpandStreetAbbreviation));

    private static string ExpandStreetAbbreviation(string token)
    {
        if (token is "str." or "str")
        {
            return "strasse";
        }
        if (token.EndsWith("str.", StringComparison.Ordinal))
        {
            return token[..^4] + "strasse";
        }
        if (token.EndsWith("str", StringComparison.Ordinal))
        {
            return token[..^3] + "strasse";
        }
        if (token is "g.")
        {
            return "gasse";
        }
        if (token.EndsWith("g.", StringComparison.Ordinal))
        {
            return token[..^2] + "gasse";
        }
        if (token is "pl.")
        {
            return "platz";
        }
        if (token.EndsWith("pl.", StringComparison.Ordinal))
        {
            return token[..^3] + "platz";
        }
        if (token is "wg.")
        {
            return "weg";
        }
        if (token.EndsWith("wg.", StringComparison.Ordinal))
        {
            return token[..^3] + "weg";
        }
        return token;
    }

    /// <summary>The first CdtrRefInf/Ref of the transaction's RmtInf/Strd, or null when absent.</summary>
    private static string? QrBillReferenceOf(Camt.EntryTransaction12? transaction)
    {
        var strd = transaction?.RmtInf?.Strd;
        if (strd is { Length: > 0 })
        {
            var refValue = strd[0].CdtrRefInf?.Ref?.Trim();
            if (!string.IsNullOrEmpty(refValue))
            {
                return refValue;
            }
        }
        return null;
    }

    /// <summary>
    /// Tries to match a payment to its open Rechnung and build the auto-entries. Returns null (no match) when:
    /// there is no QR-bill reference, no matching Rechnung, the Rechnung is already paid, the credit is an
    /// underpayment, the debit's magnitude differs from the outstanding amount, a line's Konto is missing or
    /// inactive, an overpayment Buchung is needed but Konto 3600 is missing or inactive, or the Buchungen
    /// do not sum to the movement's amount. An overpaying credit additionally books the excess to Konto 3600.
    /// A credit that completes the total returns the PaidAt timestamp
    /// (the booking date as a UTC instant) for the Paid status change; a debit never does.
    /// </summary>
    private static (
        int InvoiceId,
        IReadOnlyList<ImportEntry> Entries,
        decimal? OverpaymentAmount,
        DateTime? PaidAt
    )? MatchInvoice(
        Camt.EntryTransaction12? transaction,
        decimal signedAmount,
        bool credit,
        DateOnly date,
        ImportSnapshot snapshot
    )
    {
        var reference = QrBillReferenceOf(transaction);
        if (string.IsNullOrEmpty(reference))
        {
            return null;
        }

        var chart = snapshot.Chart;
        if (chart is null)
        {
            return null;
        }

        // A reference collision: two Rechnungen with the same non-empty reference → no match.
        var matches = snapshot.Invoices.Where(inv => inv.Reference == reference).ToList();
        if (matches.Count == 0 || matches.Count > 1)
        {
            return null;
        }
        var invoice = matches[0];

        // Once Bezahlt, always Bezahlt: no further match, so a re-import never re-books or re-sets PaidBy.
        if (invoice.AlreadyPaid)
        {
            return null;
        }

        var outstanding = invoice.Total - invoice.ExistingPaid;
        // A credit must cover at least the outstanding amount (an underpayment does not match); a debit
        // must exactly equal it (a debit whose magnitude exceeds the outstanding amount does not match).
        if (credit ? signedAmount < outstanding : signedAmount != -outstanding)
        {
            return null;
        }

        var entries = new List<ImportEntry>();
        foreach (var line in invoice.Lines)
        {
            // A missing or inactive line Konto → no match.
            var account = chart.Find(line.AccountNumber);
            if (account is null || !account.Active)
            {
                return null;
            }
            // The position's amount is multiplied by the movement's sign: credits positive, debits negative.
            var sign = signedAmount >= 0 ? 1m : -1m;
            entries.Add(new ImportEntry(line.Text, line.AccountNumber, line.Amount * sign));
        }

        // Overpayment (spec §3/§5): an overpaying credit books the excess to Konto 3600 (Spenden).
        decimal? overpaymentAmount = null;
        var excess = signedAmount - outstanding;
        if (excess > 0)
        {
            var overpaymentAccount = chart.Find(OverpaymentAccount);
            if (overpaymentAccount is null || !overpaymentAccount.Active)
            {
                return null;
            }
            overpaymentAmount = excess;
            entries.Add(
                new ImportEntry(
                    $"Rechnung {InvoiceRules.FormatNumber(invoice.Id)}",
                    OverpaymentAccount,
                    excess
                )
            );
        }

        // The Buchungen must sum to the movement's amount.
        if (entries.Sum(e => e.Amount) != signedAmount)
        {
            return null;
        }

        // Only a credit that completes the total exactly records the Paid status change, timestamped with the
        // movement's booking date (Zurich midnight as a UTC instant); an overpayment does not set Paid.
        var covered = credit && invoice.ExistingPaid >= 0 && signedAmount == outstanding;
        DateTime? paidAt = covered ? InvoiceTime.TimestampFor(date) : null;

        return (invoice.Id, entries.AsReadOnly(), overpaymentAmount, paidAt);
    }

    /// <summary>The Rechnungsnummern of the movements that were auto-booked, in import order (spec #44 §7, the response's bookedInvoices).</summary>
    public static List<int> BookedInvoiceIds(BankImportResult result) =>
        result.Transactions
            .Where(t => t.MatchedInvoiceId is not null)
            .Select(t => t.MatchedInvoiceId!.Value)
            .ToList();

    /// <summary>The count of imported movements (credits and debits) without Buchungen (spec #44 §7, the response's unmatched).</summary>
    public static int UnmatchedCount(BankImportResult result) =>
        result.Transactions.Count(t => t.AutoEntries.Count == 0);

    private sealed class ImportContext(ImportSnapshot snapshot)
    {
        public ImportSnapshot Snapshot { get; } = snapshot;
        public HashSet<string> SeenReferences { get; } =
            snapshot.ExistingBankReferences.ToHashSet();
        public List<ImportedTransaction> Transactions { get; } = [];
        public List<ImportError> Errors { get; } = [];
        public int SkippedDuplicates { get; set; }
    }
}
