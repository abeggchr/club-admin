namespace backend.Invoicing;

/// <summary>Textvorlage: standard subject, text and greetings for a Rechnung.</summary>
public class TextTemplate
{
    public int Id { get; set; }
    public string Subject { get; set; } = "";
    public string Text { get; set; } = "";
    public string Greetings { get; set; } = "";
}
