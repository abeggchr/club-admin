using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace backend.Accounting;

/// <summary>
/// The state the bank import validates against: the club settings (Klub-IBAN, Startdatum) and the
/// BankReferences already stored. Persons will join this snapshot with Person matching.
/// </summary>
public record ImportSnapshot(Settings Settings, IReadOnlyCollection<string> ExistingBankReferences);

/// <summary>One imported Bewegung to save: the bank's booking date, the signed amount, the text and the BankReference. It has no Buchungen yet.</summary>
public record ImportedTransaction(DateOnly Date, decimal Amount, string? Text, string BankReference);

/// <summary>One problem found in one file; File is null for a problem of the zip itself.</summary>
public record ImportError(string? File, string Message);

/// <summary>The outcome of the bank import: the new Bewegungen plus the skipped duplicates, or the per-file errors. When Errors is non-empty, nothing may be saved.</summary>
public record BankImportResult(List<ImportedTransaction> Transactions, int SkippedDuplicates, List<ImportError> Errors);

/// <summary>
/// The pure bank import: reads every CAMT.053 file of an eBanking zip entirely in memory and creates one
/// Bewegung per simple entry (an entry without TxDtls, or with exactly one). It does no database access,
/// uses only the .NET standard library's zip and XML support, and never uses entry names as file paths.
/// Batch entries, Person matching and zip safety limits come in follow-up tickets.
/// </summary>
public static class BankImport
{
    /// <summary>Every CAMT.053 version shares this namespace prefix; only namespaces starting with it are accepted.</summary>
    private const string Camt053NamespacePrefix = "urn:iso:std:iso:20022:tech:xsd:camt.053";

    /// <summary>Safety limits for the eBanking zip: at most this many XML files...</summary>
    private const int MaxXmlFiles = 100;

    /// <summary>...and about this many bytes in total, uncompressed.</summary>
    private const long MaxUncompressedBytes = 10 * 1024 * 1024;

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
            return new BankImportResult([], 0, [new ImportError(null, "Die ZIP-Datei ist nicht gültig.")]);
        }

        using (archive)
        {
            // Directories and non-.xml entries of the eBanking zip are ignored.
            var xmlEntries = archive.Entries
                .Where(e => !e.FullName.EndsWith('/') && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (xmlEntries.Count == 0)
            {
                return new BankImportResult([], 0, [new ImportError(null, "Keine CAMT.053-Datei in der ZIP gefunden.")]);
            }
            if (xmlEntries.Count > MaxXmlFiles)
            {
                return new BankImportResult(
                    [],
                    0,
                    [new ImportError(null, $"Die ZIP-Datei enthält {xmlEntries.Count} XML-Dateien, aber es werden maximal {MaxXmlFiles} akzeptiert.")]
                );
            }
            if (archive.Entries.Sum(e => e.Length) > MaxUncompressedBytes)
            {
                return new BankImportResult(
                    [],
                    0,
                    [new ImportError(null, $"Die ZIP-Datei ist nach dem Entpacken zu gross (maximal {MaxUncompressedBytes / 1024 / 1024} MB).")]
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
            context.Errors.Add(new ImportError(file, "Die Datei ist keine CAMT.053-Datei (Stmt fehlt)."));
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
            context.Errors.Add(new ImportError(file, "Es fehlt die Konto-IBAN (Stmt/Acct/Id/IBAN)."));
            return;
        }
        var clubIban = Iban.Normalize(context.Snapshot.Settings.Iban)!;
        if (fileIban != clubIban)
        {
            context.Errors.Add(
                new ImportError(file, $"Die IBAN {fileIban} in der Datei stimmt nicht mit der Klub-IBAN {clubIban} überein.")
            );
            return;
        }

        var ntries = LocalAll(stmt, "Ntry");
        if (ntries.Count == 0)
        {
            context.Errors.Add(new ImportError(file, "Die Datei enthält keine Buchungseinträge (Ntry)."));
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
        if (txDtls.Count > 1)
        {
            context.Errors.Add(new ImportError(file, "Einträge mit mehreren Transaktionen (TxDtls) werden noch nicht unterstützt."));
            return;
        }
        // The simple entry: exactly one TxDtls, or none (then the entry itself is the transaction).
        var transaction = txDtls.Count == 1 ? txDtls[0] : ntry;

        var status = StatusOf(ntry);
        if (string.IsNullOrWhiteSpace(status))
        {
            context.Errors.Add(new ImportError(file, "Es fehlt der Status (Sts)."));
            return;
        }
        if (status != "BOOK")
        {
            context.Errors.Add(
                new ImportError(file, $"Der Eintrag hat den Status {status}, aber nur abschließende Buchungen (BOOK) werden importiert.")
            );
            return;
        }
        if (ReversalIndicated(ntry, transaction))
        {
            context.Errors.Add(new ImportError(file, "Der Eintrag ist eine Stornierung (RvslInd) und wird nicht importiert."));
            return;
        }

        var direction = Local(ntry, "CdtDbtInd")?.Value?.Trim();
        if (direction is null)
        {
            context.Errors.Add(new ImportError(file, "Es fehlt die Gutschrift-/Lastschriftkennzeichnung (CdtDbtInd)."));
            return;
        }
        if (direction is not ("CRDT" or "DBIT"))
        {
            context.Errors.Add(new ImportError(file, $"Ungültige Gutschrift-/Lastschriftkennzeichnung: {direction}"));
            return;
        }

        var ntryAmount = AmountOf(ntry);
        if (ntryAmount is null)
        {
            context.Errors.Add(new ImportError(file, "Es fehlt der Betrag (Amt)."));
            return;
        }
        var amountElement = ReferenceEquals(transaction, ntry) ? ntryAmount : AmountOf(transaction);
        if (!decimal.TryParse(amountElement?.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            context.Errors.Add(
                new ImportError(file, amountElement is null ? "Es fehlt der Betrag (Amt)." : $"Ungültiger Betrag: {amountElement.Value}")
            );
            return;
        }

        var dateElement = Local(Local(ntry, "BookgDt"), "Dt");
        if (!DateOnly.TryParse(dateElement?.Value, CultureInfo.InvariantCulture, out var date))
        {
            context.Errors.Add(
                new ImportError(file, dateElement is null ? "Es fehlt das Buchungsdatum (BookgDt/Dt)." : $"Ungültiges Buchungsdatum: {dateElement.Value}")
            );
            return;
        }

        var bankReference = (Local(Local(transaction, "Refs"), "AcctSvcrRef")?.Value ?? Local(ntry, "AcctSvcrRef")?.Value)?.Trim();
        if (string.IsNullOrEmpty(bankReference))
        {
            context.Errors.Add(new ImportError(file, "Es fehlt die Bankreferenz (AcctSvcrRef)."));
            return;
        }

        var signedAmount = direction == "CRDT" ? amount : -amount;
        // An imported Bewegung has no Buchungen, so the Kontoplan is not needed for the date and amount rules.
        if (TransactionRules.Validate(context.Snapshot.Settings, chart: null!, date, signedAmount, []) is { } error)
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
        context.Transactions.Add(
            new ImportedTransaction(
                date,
                signedAmount,
                ustrd.Count > 0 ? string.Join(' ', ustrd) : (string.IsNullOrEmpty(addtlNtryInf) ? null : addtlNtryInf),
                bankReference
            )
        );
    }

    private sealed class ImportContext(ImportSnapshot snapshot)
    {
        public ImportSnapshot Snapshot { get; } = snapshot;
        public HashSet<string> SeenReferences { get; } = snapshot.ExistingBankReferences.ToHashSet();
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
    private static bool ReversalIndicated(XElement ntry, XElement transaction) =>
        (Local(ntry, "RvslInd")?.Value?.Trim() ?? Local(transaction, "RvslInd")?.Value?.Trim()) is "true" or "1";

    /// <summary>The amount element per CAMT version: a direct <c>Amt</c> (.08), or the .02 shape <c>AmtDtls/TxAmt/Amt</c>.</summary>
    private static XElement? AmountOf(XElement? parent) =>
        Local(parent, "Amt") ?? Local(Local(Local(parent, "AmtDtls"), "TxAmt"), "Amt");

    /// <summary>Finds the first child by local name; the namespace is ignored, as CAMT versions differ in it.</summary>
    private static XElement? Local(XElement? parent, string localName) =>
        parent?.Elements().FirstOrDefault(e => e.Name.LocalName == localName);

    private static List<XElement> LocalAll(XElement? parent, string localName) =>
        parent?.Elements().Where(e => e.Name.LocalName == localName).ToList() ?? [];
}
