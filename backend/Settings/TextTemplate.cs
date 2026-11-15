namespace backend.Settings;

/// <summary>Text Template: standard subject, text and greetings for an Invoice.</summary>
public class TextTemplate
{
    public int Id { get; set; }
    public string Subject { get; set; } = "";
    public string Text { get; set; } = "";
    public string Greetings { get; set; } = "";
}
