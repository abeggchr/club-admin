namespace backend.Invoicing;

/// <summary>Textvorlage: standard title, text and greetings for a Rechnung.</summary>
public class TextTemplate
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string Greetings { get; set; } = "";
}
