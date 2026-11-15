namespace backend.Settings;

/// <summary>Pure validation for a Textvorlage. Returns a German error message, or null when valid.</summary>
public static class TextTemplateRules
{
    public static string? Validate(string? subject, string? text, string? greetings)
    {
        if (string.IsNullOrWhiteSpace(subject))
            return "Der Betreff ist erforderlich.";
        if (string.IsNullOrWhiteSpace(text))
            return "Der Text ist erforderlich.";
        if (string.IsNullOrWhiteSpace(greetings))
            return "Die Grüsse sind erforderlich.";
        return null;
    }
}
