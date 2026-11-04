using System.Globalization;

namespace backend.Person;

public static class PersonCsvImporter
{
    private static readonly string[] DateFormats = ["dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd"];

    /// <summary>Parses a CSV with the German column headers of the member list (e.g. "Vorname", "PLZ").</summary>
    public static List<Person> Parse(TextReader reader)
    {
        var rows = ReadRows(reader.ReadToEnd());
        if (rows.Count == 0)
            throw new FormatException("The CSV file is empty.");

        var headers = rows[0].Select(h => h.Trim()).ToList();
        string[] required = ["Vorname", "Nachname"];
        var missing = required.Where(h => !headers.Contains(h)).ToArray();
        if (missing.Length > 0)
            throw new FormatException($"Missing column(s): {string.Join(", ", missing)}.");

        var persons = new List<Person>();
        for (var i = 1; i < rows.Count; i++)
        {
            var fields = rows[i];
            var row = i + 1;
            string? Get(string header)
            {
                var index = headers.IndexOf(header);
                return
                    index >= 0 && index < fields.Count && !string.IsNullOrWhiteSpace(fields[index])
                    ? fields[index].Trim()
                    : null;
            }

            DateOnly? GetDate(string header)
            {
                var v = Get(header);
                if (v is null)
                    return null;
                if (
                    DateOnly.TryParseExact(
                        v,
                        DateFormats,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var d
                    )
                )
                    return d;
                throw new FormatException($"Row {row}: invalid date '{v}' in column '{header}'.");
            }

            var firstName = Get("Vorname");
            var lastName = Get("Nachname");
            if (firstName is null && lastName is null)
                continue; // blank line
            if (firstName is null || lastName is null)
                throw new FormatException($"Row {row}: first and last name are required.");

            persons.Add(
                new Person
                {
                    FirstName = firstName,
                    LastName = lastName,
                    DateOfBirth = GetDate("Geburtstag"),
                    JoinedOn = GetDate("Eintritt"),
                    LeftOn = GetDate("Austritt"),
                    Email = Get("E-Mail"),
                    MobilePhone = Get("Mobilfunknummer"),
                    LandlinePhone = Get("Festnetznummer"),
                    Address = Get("Adresse"),
                    PostalCode = Get("PLZ"),
                    City = Get("Ort"),
                    Country = Get("Land"),
                    Notes = Get("Notizen"),
                }
            );
        }

        return persons;
    }

    /// <summary>RFC 4180 reader; the delimiter (comma, semicolon or tab) is detected from the header line.</summary>
    private static List<List<string>> ReadRows(string text)
    {
        text = text.TrimStart('\uFEFF');
        var headerLine = text.Split('\n', 2)[0];
        var delimiter = new[] { ',', ';', '\t' }.MaxBy(c => headerLine.Count(x => x == c));

        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new System.Text.StringBuilder();
        var quoted = false;

        void EndRow()
        {
            row.Add(field.ToString());
            field.Clear();
            if (row.Any(f => f.Length > 0))
                rows.Add(row);
            row = [];
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (c == '"')
                    quoted = false;
                else
                    field.Append(c);
            }
            else if (c == '"')
                quoted = true;
            else if (c == delimiter)
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (c == '\n')
                EndRow();
            else if (c != '\r')
                field.Append(c);
        }
        if (field.Length > 0 || row.Count > 0)
            EndRow();
        return rows;
    }
}
