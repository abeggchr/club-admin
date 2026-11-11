namespace backend.Person;

/// <summary>The membership type of a Person ("Mitgliedschaft" in the UI).</summary>
public enum MembershipType
{
    Unknown,
    Family,
    Single,
    Junior,
    Honorary,
    Free,
    Patron,
}

public static partial class MembershipTypeExtensions
{
    /// <summary>The German label as shown in the UI.</summary>
    public static string Label(this MembershipType type) =>
        type switch
        {
            MembershipType.Unknown => "Unbekannt",
            MembershipType.Family => "Familienmitglied",
            MembershipType.Single => "Einzelmitglied",
            MembershipType.Junior => "Junior:innen",
            MembershipType.Honorary => "Ehrenmitglied",
            MembershipType.Free => "Freimitglied",
            MembershipType.Patron => "Gönner:innen",
            _ => "Unbekannt",
        };

    /// <summary>
    /// Parses the value of the "Mitgliedschaft" column of the member list CSV into a membership
    /// type, applying the importer's mapping ("Folgemitglied" is a Familienmitglied, "Gönner" is
    /// a Gönner:in). An empty value stays Unknown, an unrecognized value throws.
    /// </summary>
    public static MembershipType FromCsvValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return MembershipType.Unknown;
        return value.Trim() switch
        {
            "Familienmitglied" or "Folgemitglied" => MembershipType.Family,
            "Einzelmitglied" => MembershipType.Single,
            "Junior:innen" => MembershipType.Junior,
            "Ehrenmitglied" => MembershipType.Honorary,
            "Freimitglied" => MembershipType.Free,
            "Gönner:innen" or "Gönner" => MembershipType.Patron,
            _ => throw new FormatException($"Unknown membership type '{value}'. Allowed: {string.Join(", ", MembershipTypeExtensions.CsvValues())}."),
        };
    }

    /// <summary>Every accepted value of the "Mitgliedschaft" CSV column, including the importer's mapping.</summary>
    public static string[] CsvValues() =>
        ["Familienmitglied", "Folgemitglied", "Einzelmitglied", "Junior:innen", "Ehrenmitglied", "Freimitglied", "Gönner:innen", "Gönner"];
}
