namespace backend.Accounting;

/// <summary>The club's bank account IBAN: a Swiss (CH) or Liechtenstein (LI) IBAN of 21 characters with a valid mod-97 checksum.</summary>
public static class Iban
{
    public const string InvalidError = "Ungültige IBAN";

    /// <summary>Normalises before validation and storage: removes spaces and upper cases.</summary>
    public static string? Normalize(string? raw) =>
        raw is null ? null : new string(raw.Where(c => !char.IsWhiteSpace(c)).Select(char.ToUpperInvariant).ToArray());

    /// <summary>Returns an error message when the IBAN is not a CH/LI IBAN of 21 characters with a valid checksum, or null when valid.</summary>
    public static string? Validate(string? raw)
    {
        var iban = Normalize(raw);
        if (iban is null || iban.Length != 21 || iban[..2] is not ("CH" or "LI"))
        {
            return InvalidError;
        }
        for (var i = 2; i < iban.Length; i++)
        {
            if (!char.IsDigit(iban[i]))
            {
                return InvalidError;
            }
        }
        return Mod97(iban) == 1 ? null : InvalidError;
    }

    /// <summary>ISO 7064 mod 97-10: move the first four characters to the end, letters become digits (A=10 … Z=35), the remainder must be 1.</summary>
    static int Mod97(string iban)
    {
        var rearranged = iban[4..] + iban[..4];
        var value = 0L;
        foreach (var c in rearranged)
        {
            value = c is >= 'A' and <= 'Z' ? (value * 100 + (c - 'A' + 10)) % 97 : (value * 10 + (c - '0')) % 97;
        }
        return (int)value;
    }
}
