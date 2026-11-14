namespace backend.Person;

/// <summary>A Person's id and their Zahler (null when none).</summary>
public record PayerLink(int Id, int? PayerId);

/// <summary>Pure rules for the Zahler of a Person: no self-reference, no chains, no cycles. Return a German error message, or null when valid.</summary>
public static class PayerRules
{
    public static string? ValidateSet(int personId, int? payerId, IReadOnlyCollection<PayerLink> all)
    {
        if (payerId is not { } payer)
        {
            return null;
        }
        if (payer == personId)
        {
            return "Eine Person kann nicht ihr eigener Zahler sein.";
        }
        if (all.FirstOrDefault(p => p.Id == payer) is not { } payerLink)
        {
            return "Der Zahler existiert nicht.";
        }
        if (payerLink.PayerId is not null)
        {
            return "Der Zahler hat selbst einen Zahler.";
        }
        if (all.Any(p => p.PayerId == personId))
        {
            return "Diese Person ist selbst Zahler und kann keinen Zahler haben.";
        }
        return null;
    }

    public static string? ValidateDelete(int personId, IReadOnlyCollection<PayerLink> all) =>
        all.Any(p => p.PayerId == personId)
            ? "Die Person ist Zahler für andere Personen und kann nicht gelöscht werden."
            : null;
}
