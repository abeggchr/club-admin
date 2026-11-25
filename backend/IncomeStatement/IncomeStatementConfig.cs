using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace backend.IncomeStatement;

/// <summary>
/// The layout of the Income Statement (Erfolgsrechnung), read from a checked-in YAML file.
/// Key order in the file is preserved and drives the sections, groups and lines of the report.
/// </summary>
public class IncomeStatementConfig
{
    public IncomeStatementConfig(IReadOnlyList<Section> sections) => Sections = sections;

    /// <summary>The sections in file order: "A. Ertrag", "B. Aufwand".</summary>
    public IReadOnlyList<Section> Sections { get; }

    public static IncomeStatementConfig Load(string path)
    {
        string yaml;
        try
        {
            yaml = File.ReadAllText(path);
        }
        catch (IOException e)
        {
            throw new IncomeStatementConfigException(
                $"Erfolgsrechnung-Konfiguration '{path}' kann nicht gelesen werden: {e.Message}",
                e
            );
        }
        return Parse(yaml);
    }

    /// <summary>Parses the YAML file. Structure errors abort startup with a German message.</summary>
    public static IncomeStatementConfig Parse(string yaml)
    {
        YamlStream stream;
        try
        {
            stream = new YamlStream();
            using var reader = new StringReader(yaml);
            stream.Load(reader);
        }
        catch (YamlException e)
        {
            throw new IncomeStatementConfigException(
                $"Erfolgsrechnung-Konfiguration ist kein gültiges YAML: {e.Message}",
                e
            );
        }

        if (stream.Documents.Count == 0)
        {
            throw new IncomeStatementConfigException(
                "Erfolgsrechnung-Konfiguration muss die oberste Taste 'Erfolgsrechnung' enthalten."
            );
        }

        YamlNode? root;
        try
        {
            root = stream.Documents[0].RootNode;
        }
        catch (YamlException e)
        {
            throw new IncomeStatementConfigException(
                $"Erfolgsrechnung-Konfiguration ist kein gültiges YAML: {e.Message}",
                e
            );
        }
        if (root is not YamlMappingNode rootMapping)
        {
            throw new IncomeStatementConfigException(
                "Erfolgsrechnung-Konfiguration muss die oberste Taste 'Erfolgsrechnung' enthalten."
            );
        }

        YamlMappingNode? file = null;
        foreach (var entry in rootMapping.Children)
        {
            if (entry.Key is YamlScalarNode key && key.Value == "Erfolgsrechnung")
            {
                file = entry.Value as YamlMappingNode;
                break;
            }
        }
        if (file is null)
        {
            throw new IncomeStatementConfigException(
                "Erfolgsrechnung-Konfiguration muss die oberste Taste 'Erfolgsrechnung' enthalten."
            );
        }

        var sections = new List<Section>();
        foreach (var entry in file.Children)
        {
            if (entry.Key is not YamlScalarNode label || entry.Value is not YamlMappingNode groups)
            {
                throw new IncomeStatementConfigException(
                    "Jeder Abschnitt unter 'Erfolgsrechnung' muss eine Taste und Gruppen enthalten."
                );
            }
            var groupList = new List<Group>();
            foreach (var groupEntry in groups.Children)
            {
                if (groupEntry.Key is not YamlScalarNode groupLabel || groupEntry.Value is not YamlMappingNode lines)
                {
                    throw new IncomeStatementConfigException(
                        $"Jede Gruppe des Abschnitts '{label.Value}' muss eine Taste und Konten enthalten."
                    );
                }
                var lineList = new List<Line>();
                foreach (var lineEntry in lines.Children)
                {
                    if (lineEntry.Key is not YamlScalarNode lineLabel)
                    {
                        throw new IncomeStatementConfigException(
                            $"Jede Zeile der Gruppe '{groupLabel.Value}' muss eine Taste sein."
                        );
                    }
                    if (lineEntry.Value is YamlScalarNode reference)
                    {
                        lineList.Add(ToLine(lineLabel.Value, reference.Value));
                    }
                    else
                    {
                        throw new IncomeStatementConfigException(
                            $"Die Zeile '{lineLabel.Value}' braucht eine Kontonummer oder einen Bereich 'von-bis'."
                        );
                    }
                }
                groupList.Add(new Group(groupLabel.Value, lineList));
            }
            sections.Add(new Section(label.Value, groupList));
        }

        return new IncomeStatementConfig(sections);
    }

    /// <summary>Parses a single account number or a "from-to" range.</summary>
    private static Line ToLine(string label, string reference)
    {
        // YamlDotNet decodes plain (unquoted) scalars to numbers; a quoted number stays a string.
        if (reference.Length > 0 && reference.All(c => c >= '0' && c <= '9') && int.TryParse(reference, out var number))
        {
            return number > 0 ? new Line(label, number) : throw InvalidReference(label, reference);
        }

        if (TryParseRange(reference, out var range))
        {
            return new Line(label, range.Value.from, range.Value.to);
        }

        throw InvalidReference(label, reference);
    }

    private static IncomeStatementConfigException InvalidReference(string label, string reference) =>
        new($"Die Kontonummer '{reference}' von '{label}' ist ungültig.");

    /// <summary>Parses a "from-to" range; both numbers must be positive, and the text unquoted.</summary>
    private static bool TryParseRange(string reference, out (int from, int to)? range)
    {
        range = null;
        var dash = reference.IndexOf('-');
        if (dash <= 0 || reference.IndexOf('-', dash + 1) >= 0)
        {
            return false;
        }
        if (!TryPositive(reference[..dash], out var from) || !TryPositive(reference[(dash + 1)..], out var to))
        {
            return false;
        }
        range = (from, to);
        return true;
    }

    private static bool TryPositive(string text, out int number)
    {
        number = 0;
        if (text.Length == 0 || text[0] is < '0' or > '9')
        {
            return false;
        }
        foreach (var c in text)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }
        return int.TryParse(text, out number) && number > 0;
    }
}

public class IncomeStatementConfigException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>A report section ("A. Ertrag" or "B. Aufwand"), in file order.</summary>
public record Section(string Label, IReadOnlyList<Group> Groups);

/// <summary>A numbered group ("I.", "II.", …) with its lines, in file order.</summary>
public record Group(string Label, IReadOnlyList<Line> Lines);

/// <summary>
/// One configured line. A single account has From == To; a range covers [From, To] inclusive.
/// A range may include numbers that are not in the Chart of Accounts.
/// </summary>
public record Line(string Label, int From, int? To = null)
{
    public bool IsRange => To is not null;

    public int Last => To ?? From;
}
