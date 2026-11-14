using backend.Invoicing;
using backend.Person;

namespace backend.Person;

/// <summary>The recipients of the club news mailing list: every active Person plus every flagged Person.</summary>
public static class ClubNewsRecipientRules
{
    /// <summary>
    /// Active (Austritt in the future or none) at <paramref name="at"/>, or flagged with
    /// "Newsletter": the Jahresbeiträge activity rule does not apply, so a Person who never
    /// joined but is flagged still receives the club news.
    /// </summary>
    public static bool ReceivesNews(Person p, DateOnly at) =>
        p.ReceivesClubNews || MembershipFeeRules.IsActive(p, at);

    /// <summary>Every recipient, in Id order (the order the Personen appear in the table).</summary>
    public static IEnumerable<Person> Recipients(IEnumerable<Person> persons, DateOnly at) =>
        persons.Where(p => ReceivesNews(p, at)).OrderBy(p => p.Id);
}

/// <summary>
/// Builds the Versandliste: one row per postal address, "Strasse Hausnummer", "PLZ Ort" and the
/// stored Land. All recipients at one complete address (Strasse, Hausnummer, PLZ, Ort) share a row
/// with their names, one per line; a recipient with an incomplete address gets a row of their own
/// with whatever address parts exist. No warning is emitted for incomplete addresses.
/// </summary>
public static class MailingListRules
{
    public record Row(string Recipients, string Address, string Ort, string Land);

    public static IReadOnlyList<Row> BuildRows(IEnumerable<Person> recipients)
    {
        var recipientList = recipients.ToList();
        // Rows in order of first appearance: 0 = own row (Person), 1 = group row (index into groups).
        var entries = new List<(int kind, object data)>();
        var groups = new List<List<Person>>();
        var groupIndex = new Dictionary<string, int>();
        foreach (var p in recipientList)
        {
            var key = AddressKey(p);
            if (key is null)
            {
                entries.Add((0, p));
            }
            else
            {
                if (!groupIndex.ContainsKey(key))
                {
                    groupIndex[key] = groups.Count;
                    groups.Add(new List<Person>());
                    entries.Add((1, groups.Count - 1));
                }
                groups[groupIndex[key]].Add(p);
            }
        }
        return entries
            .Select(e => e.kind == 0 ? OwnRow((Person)e.data) : GroupRow(groups[(int)e.data]))
            .ToList();
    }

    private static Row GroupRow(List<Person> group)
    {
        var first = group[0];
        return new Row(
            string.Join('\n', group.Select(p => p.FullName)),
            JoinAddress(first.Street, first.Number),
            JoinOrt(first.PostalCode, first.City),
            first.Country?.Trim() ?? ""
        );
    }

    private static Row OwnRow(Person p) => new(
        p.FullName,
        JoinAddress(p.Street, p.Number),
        JoinOrt(p.PostalCode, p.City),
        p.Country?.Trim() ?? ""
    );

    private static string JoinAddress(string? street, string? number)
    {
        var parts = new[] { street, number }.Where(s => !string.IsNullOrWhiteSpace(s));
        return string.Join(' ', parts.Select(s => s!.Trim()));
    }

    private static string JoinOrt(string? postalCode, string? city)
    {
        var parts = new[] { postalCode, city }.Where(s => !string.IsNullOrWhiteSpace(s));
        return string.Join(' ', parts.Select(s => s!.Trim()));
    }

    /// <summary>
    /// The grouping key: all four address parts present, trimmed, case-insensitive; null when
    /// any part is missing (such a recipient is a row of their own). The Land is deliberately
    /// not part of the key: the Jahresbeiträge run groups by the postal address only as well.
    /// </summary>
    private static string? AddressKey(Person p)
    {
        string?[] parts = [p.Street, p.Number, p.PostalCode, p.City];
        if (parts.Any(string.IsNullOrWhiteSpace))
            return null;
        return string.Join('|', parts.Select(s => s!.Trim().ToLowerInvariant()));
    }
}

/// <summary>
/// The Versandliste CSV: ";"-separated and CRLF, like the Personen export, so it opens in the
/// local German Excel. A name row is one cell with one name per line. The string is BOM-free;
/// the endpoint prepends the UTF-8 BOM.
/// </summary>
public static class MailingListCsv
{
    public static string Build(IEnumerable<MailingListRules.Row> rows)
    {
        var lines = new List<string> { string.Join(';', Header) };
        foreach (var row in rows)
        {
            lines.Add(string.Join(';', RecipientsCell(row.Recipients), Cell(row.Address), Cell(row.Ort), Cell(row.Land)));
        }
        return string.Join("\r\n", lines);
    }

    private static readonly string[] Header = ["Empfänger", "Adresse", "Ort", "Land"];

    // A name row contains one name per line; the cell must be quoted so the CSV keeps its shape.
    private static string RecipientsCell(string recipients) => Quote(recipients);

    private static string Cell(string value)
    {
        var s = value ?? "";
        return NeedsQuoting(s) ? Quote(s) : s;
    }

    private static string Quote(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";

    private static bool NeedsQuoting(string s)
    {
        foreach (var c in s)
        {
            if (c == '"' || c == ';' || c == '\n' || c == '\r')
                return true;
        }
        return false;
    }
}
