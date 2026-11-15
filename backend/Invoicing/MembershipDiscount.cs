namespace backend.Invoicing;

/// <summary>Discount: a Person's percentage discount on the Annual Fee, stored until removed.</summary>
public class MembershipDiscount
{
    public int PersonId { get; set; }
    public int Percent { get; set; }
    public string Text { get; set; } = "";
}

/// <summary>Pure validation for a Discount. Returns a German error message, or null when valid.</summary>
public static class MembershipDiscountRules
{
    public static string? Validate(int? personId, int? percent, string? text)
    {
        if (personId is null) return "Die Person ist erforderlich.";
        if (percent is null or < 1 or > 100) return "Der Prozentsatz muss zwischen 1 und 100 liegen.";
        if (string.IsNullOrWhiteSpace(text)) return "Der Text ist erforderlich.";
        return null;
    }
}
