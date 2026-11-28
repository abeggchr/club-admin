namespace backend.Person;

/// <summary>A Person's id, their Payer (null when none) and whether they are Main Payer, as stored.</summary>
public record PayerLink(int Id, int? PayerId, bool IsMainPayer);

/// <summary>Pure rules for the Payer and Main Payer of a Person: no self-reference, no chains, no cycles, the flag only without Payer. Return a German error message, or null when valid.</summary>
public static class PayerRules
{
    public static string? ValidateSet(
        int personId,
        int? payerId,
        bool isMainPayer,
        IReadOnlyCollection<PayerLink> all
    )
    {
        if (ValidatePayer(personId, payerId, all) is { } payerError)
        {
            return payerError;
        }
        if (!isMainPayer)
        {
            return null;
        }
        if (payerId is not null)
        {
            return "Eine Person mit Zahler kann nicht Hauptzahler sein.";
        }
        // Only gaining the flag is refused: a Payer's picker is locked, so a flag they already carry is kept.
        var wasMainPayer = all.Any(p => p.Id == personId && p.IsMainPayer);
        if (!wasMainPayer && all.Any(p => p.PayerId == personId))
        {
            return "Diese Person ist selbst Zahler und kann nicht Hauptzahler werden.";
        }
        return null;
    }

    private static string? ValidatePayer(
        int personId,
        int? payerId,
        IReadOnlyCollection<PayerLink> all
    )
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
