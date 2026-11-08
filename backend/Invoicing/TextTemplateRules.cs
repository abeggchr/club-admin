namespace backend.Invoicing;

/// <summary>Pure validation for a Textvorlage. Returns a German error message, or null when valid.</summary>
public static class TextTemplateRules
{
    public static string? Validate(string? title, string? text, string? greetings)
    {
        if (string.IsNullOrWhiteSpace(title)) return "Der Titel ist erforderlich.";
        if (string.IsNullOrWhiteSpace(text)) return "Der Text ist erforderlich.";
        if (string.IsNullOrWhiteSpace(greetings)) return "Die Grüsse sind erforderlich.";
        return null;
    }
}
