namespace backend.Invoicing;

/// <summary>Pure validation for a Textvorlage. Returns a German error message, or null when valid.</summary>
public static class TextTemplateRules
{
    public static string? Validate(string? subject, string? text, string? greetings)
    {
        if (string.IsNullOrWhiteSpace(subject)) return "Der Betreff ist erforderlich.";
        if (string.IsNullOrWhiteSpace(text)) return "Der Text ist erforderlich.";
        if (string.IsNullOrWhiteSpace(greetings)) return "Die Grüsse sind erforderlich.";
        foreach (var field in new[] { subject, text, greetings })
            if (TextTemplatePlaceholders.FindUnknown(field) is { } unknown)
                return $"Unbekannter Platzhalter {{{{{unknown}}}}}. Gültig sind: "
                    + string.Join(", ", TextTemplatePlaceholders.Keys.Select(k => $"{{{{{k}}}}}"))
                    + ".";
        return null;
    }
}
