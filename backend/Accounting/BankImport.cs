using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace backend.Accounting;

using Person = backend.Person.Person;

/// <summary>
/// The state the bank import validates against: the club settings (Klub-IBAN, Startdatum), the
/// BankReferences already stored, and all Persons (including former members) for counterparty matching.
/// </summary>
public record ImportSnapshot(
    Settings Settings,
    IReadOnlyCollection<string> ExistingBankReferences,
    IReadOnlyCollection<Person> Persons
);

/// <summary>One imported Bewegung to save: the bank's booking date, the signed amount, the text, the BankReference and, when the counterparty matched, the PersonId.</summary>
public record ImportedTransaction(
    DateOnly Date,
    decimal Amount,
    string? Text,
    string BankReference,
    int? PersonId
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
/// Bewegung per transaction: an entry without TxDtls, or one of its TxDtls for a batch entry. It does no
/// database access, uses only the .NET standard library's zip and XML support, and never uses entry names
/// as file paths. The counterparty of each transaction is linked to the matching Person when the bank
/// address matches; the parser and the matcher are internal details of this module.
/// </summary>
public static class BankImport
{
    /// <summary>Every CAMT.053 version shares this namespace prefix; only namespaces starting with it are accepted.</summary>
    private const string Camt053NamespacePrefix = "urn:iso:std:iso:20022:tech:xsd:camt.053";

    /// <summary>Safety limits for the eBanking zip: at most this many XML files...</summary>
    private const int MaxXmlFiles = 100;

    /// <summary>...and about this many bytes in total, uncompressed.</summary>
    private const long MaxUncompressedBytes = 10 * 1024 * 1024;

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
                    [new ImportError(null, "Keine CAMT.053-Datei in der ZIP gefunden.")]
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

    private static void ParseStatement(XDocument document, string file, ImportContext context)
    {
        var stmt = document.Root;
        if (stmt is null || stmt.Name.LocalName != "Stmt")
        {
            context.Errors.Add(
                new ImportError(file, "Die Datei ist keine CAMT.053-Datei (Stmt fehlt).")
            );
            return;
        }

        var ns = stmt.Name.NamespaceName;
        if (!ns.StartsWith(Camt053NamespacePrefix, StringComparison.Ordinal))
        {
            context.Errors.Add(
                new ImportError(
                    file,
                    string.IsNullOrEmpty(ns)
                        ? "Die Datei ist keine CAMT.053-Datei (Namespace fehlt)."
                        : $"Die Datei ist keine CAMT.053-Datei (Namespace {ns} ist kein CAMT.053-Namespace)."
                )
            );
            return;
        }

        var fileIban = Iban.Normalize(Local(Local(Local(stmt, "Acct"), "Id"), "IBAN")?.Value);
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

        var ntries = LocalAll(stmt, "Ntry");
        if (ntries.Count == 0)
        {
            context.Errors.Add(new ImportError(file, "Die Datei enthält keine Bewegungen (Ntry)."));
            return;
        }
        foreach (var ntry in ntries)
        {
            ParseEntry(ntry, file, context);
        }
    }

    private static void ParseEntry(XElement ntry, string file, ImportContext context)
    {
        var txDtls = LocalAll(ntry, "TxDtls");

        var status = StatusOf(ntry);
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
        if (ReversalIndicated(ntry))
        {
            context.Errors.Add(
                new ImportError(
                    file,
                    "Der Eintrag ist eine Stornierung (RvslInd) und wird nicht importiert."
                )
            );
            return;
        }

        var direction = Local(ntry, "CdtDbtInd")?.Value?.Trim();
        if (direction is null)
        {
            context.Errors.Add(
                new ImportError(
                    file,
                    "Es fehlt die Gutschrift-/Lastschriftkennzeichnung (CdtDbtInd)."
                )
            );
            return;
        }
        if (direction is not ("CRDT" or "DBIT"))
        {
            context.Errors.Add(
                new ImportError(
                    file,
                    $"Ungültige Gutschrift-/Lastschriftkennzeichnung: {direction}"
                )
            );
            return;
        }

        var ntryAmountElement = AmountOf(ntry);
        if (ntryAmountElement is null)
        {
            context.Errors.Add(new ImportError(file, "Es fehlt der Betrag (Amt)."));
            return;
        }

        var dateElement = Local(Local(ntry, "BookgDt"), "Dt");
        if (!DateOnly.TryParse(dateElement?.Value, CultureInfo.InvariantCulture, out var date))
        {
            context.Errors.Add(
                new ImportError(
                    file,
                    dateElement is null
                        ? "Es fehlt das Buchungsdatum (BookgDt/Dt)."
                        : $"Ungültiges Buchungsdatum: {dateElement.Value}"
                )
            );
            return;
        }

        if (txDtls.Count > 1)
        {
            // A batch entry: one Bewegung per TxDtls; the sign and the booking date come from the entry.
            var txAmounts = new decimal[txDtls.Count];
            for (var i = 0; i < txDtls.Count; i++)
            {
                var txAmountElement = AmountOf(txDtls[i]);
                if (
                    !decimal.TryParse(
                        txAmountElement?.Value,
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out var txAmount
                    )
                )
                {
                    context.Errors.Add(
                        new ImportError(
                            file,
                            txAmountElement is null
                                ? "Es fehlt der Betrag (Amt)."
                                : $"Ungültiger Betrag: {txAmountElement.Value}"
                        )
                    );
                    return;
                }
                txAmounts[i] = txAmount;
            }

            if (
                !decimal.TryParse(
                    ntryAmountElement.Value,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var ntryAmount
                )
            )
            {
                context.Errors.Add(
                    new ImportError(file, $"Ungültiger Betrag: {ntryAmountElement.Value}")
                );
                return;
            }
            if (txAmounts.Sum() != ntryAmount)
            {
                context.Errors.Add(
                    new ImportError(
                        file,
                        $"Die Beträge der einzelnen Bewegungen addieren sich nicht auf den Bewegungsbetrag der Gruppe (Ntry/Amt): {txAmounts.Sum().ToString("0.00", CultureInfo.InvariantCulture)} statt {ntryAmount.ToString("0.00", CultureInfo.InvariantCulture)}."
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
        var transaction = txDtls.Count == 1 ? txDtls[0] : ntry;
        var amountElement = ReferenceEquals(transaction, ntry)
            ? ntryAmountElement
            : AmountOf(transaction);
        if (
            !decimal.TryParse(
                amountElement?.Value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var amount
            )
        )
        {
            context.Errors.Add(
                new ImportError(
                    file,
                    amountElement is null
                        ? "Es fehlt der Betrag (Amt)."
                        : $"Ungültiger Betrag: {amountElement.Value}"
                )
            );
            return;
        }

        ParseTransaction(ntry, transaction, amount, direction, date, file, context);
    }

    /// <summary>
    /// One Bewegung for one transaction: for a simple entry the entry itself, for a batch entry one of its
    /// TxDtls. The transaction's own amount, Ustrd text and TxDtls/Refs/AcctSvcrRef are used; the sign comes
    /// from the entry's CdtDbtInd.
    /// </summary>
    private static void ParseTransaction(
        XElement ntry,
        XElement transaction,
        decimal amount,
        string direction,
        DateOnly date,
        string file,
        ImportContext context
    )
    {
        if (ReversalIndicated(transaction))
        {
            context.Errors.Add(
                new ImportError(
                    file,
                    "Der Eintrag ist eine Stornierung (RvslInd) und wird nicht importiert."
                )
            );
            return;
        }

        var bankReference = (
            Local(Local(transaction, "Refs"), "AcctSvcrRef")?.Value
            ?? Local(ntry, "AcctSvcrRef")?.Value
        )?.Trim();
        if (string.IsNullOrEmpty(bankReference))
        {
            context.Errors.Add(new ImportError(file, "Es fehlt die Bankreferenz (AcctSvcrRef)."));
            return;
        }

        var signedAmount = direction == "CRDT" ? amount : -amount;
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

        var ustrd = LocalAll(transaction, "Ustrd")
            .Select(e => e.Value.Trim())
            .Where(v => v.Length > 0)
            .ToList();
        var addtlNtryInf = Local(ntry, "AddtlNtryInf")?.Value?.Trim();
        var text =
            ustrd.Count > 0
                ? string.Join(' ', ustrd)
                : (string.IsNullOrEmpty(addtlNtryInf) ? null : addtlNtryInf);
        var counterpartyParty = CounterpartyPartyOf(transaction, direction);
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
        context.Transactions.Add(
            new ImportedTransaction(date, signedAmount, text, bankReference, person?.Id)
        );
    }

    /// <summary>The counterparty party of a transaction: the Dbtr (payer) for a credit, the Cdtr (payee) for a debit.</summary>
    private static XElement? CounterpartyPartyOf(XElement transaction, string direction) =>
        direction == "CRDT" ? Local(transaction, "Dbtr") : Local(transaction, "Cdtr");

    /// <summary>
    /// The counterparty name of a party, read in both known shapes (<c>Nm</c> in .02, <c>Pty/Nm</c> in .08).
    /// </summary>
    private static string? CounterpartyNameOf(XElement? party)
    {
        var name = PartyLocal(party, "Nm")?.Value?.Trim();
        return string.IsNullOrEmpty(name) ? null : name;
    }

    /// <summary>The party's child element in both known shapes: directly (.02) or under <c>Pty</c> (.08).</summary>
    private static XElement? PartyLocal(XElement? party, string localName) =>
        Local(party, localName) ?? Local(Local(party, "Pty"), localName);

    /// <summary>The normalised PLZ, street and house number of a bank counterparty's postal address.</summary>
    private sealed record BankAddress(string PostalCode, string Street, string Number);

    /// <summary>
    /// The postal address (PstA, directly or under Pty) of a party, reduced to what matching needs: the PLZ,
    /// the street and the house number, all normalised. Street and number come from StrtNm plus BldgNb; without
    /// a BldgNb, StrtNm is split at the last token that starts with a digit; otherwise the first AdrLine that
    /// looks like street plus number is used. The PLZ comes from PstCd, or else from an AdrLine of the form
    /// "&lt;PLZ&gt; &lt;Ort&gt;". Null when any of the three is missing on the bank side.
    /// </summary>
    private static BankAddress? BankAddressOf(XElement? party)
    {
        var address = PartyLocal(party, "PstA");
        if (address is null)
        {
            return null;
        }

        var streetName = Local(address, "StrtNm")?.Value?.Trim();
        var buildingNumber = Local(address, "BldgNb")?.Value?.Trim();
        var addressLines = LocalAll(address, "AdrLine")
            .Select(l => l.Value.Trim())
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

        var postalCode = Local(address, "PstCd")?.Value?.Trim();
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

    private sealed class ImportContext(ImportSnapshot snapshot)
    {
        public ImportSnapshot Snapshot { get; } = snapshot;
        public HashSet<string> SeenReferences { get; } =
            snapshot.ExistingBankReferences.ToHashSet();
        public List<ImportedTransaction> Transactions { get; } = [];
        public List<ImportError> Errors { get; } = [];
        public int SkippedDuplicates { get; set; }
    }

    /// <summary>The entry status per CAMT version: <c>Sts</c> as text (.02), or as <c>Sts/Cd</c> (.08).</summary>
    private static string? StatusOf(XElement ntry)
    {
        var sts = Local(ntry, "Sts");
        return (Local(sts, "Cd")?.Value ?? sts?.Value)?.Trim();
    }

    /// <summary>A reversal can be marked on the entry or on the transaction details.</summary>
    private static bool ReversalIndicated(XElement element) =>
        Local(element, "RvslInd")?.Value?.Trim() is "true" or "1";

    /// <summary>The amount element per CAMT version: a direct <c>Amt</c> (.08), or the .02 shape <c>AmtDtls/TxAmt/Amt</c>.</summary>
    private static XElement? AmountOf(XElement? parent) =>
        Local(parent, "Amt") ?? Local(Local(Local(parent, "AmtDtls"), "TxAmt"), "Amt");

    /// <summary>Finds the first child by local name; the namespace is ignored, as CAMT versions differ in it.</summary>
    private static XElement? Local(XElement? parent, string localName) =>
        parent?.Elements().FirstOrDefault(e => e.Name.LocalName == localName);

    private static List<XElement> LocalAll(XElement? parent, string localName) =>
        parent?.Elements().Where(e => e.Name.LocalName == localName).ToList() ?? [];
}
