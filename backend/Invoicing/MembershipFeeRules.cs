using backend.Person;

namespace backend.Invoicing;

/// <summary>One Rechnung the Jahresbeiträge run will create: one per address, one line per paying Person.</summary>
public record PlannedInvoice(
    backend.Person.Person Recipient,
    IReadOnlyList<InvoiceLineDraft> Lines
);

public record BulkInvoicePlan(
    IReadOnlyList<PlannedInvoice> Invoices,
    IReadOnlyList<string> Warnings
);

/// <summary>Pure rules deciding who pays which Jahresbeitrag and on which Rechnung.</summary>
public static class MembershipFeeRules
{
    public const int AccountNumber = 3400;

    /// <summary>The yearly fee in CHF, or null when the type is not invoiced (Unbekannt).</summary>
    public static decimal? FeeFor(MembershipType type) =>
        type switch
        {
            MembershipType.Family => 135m,
            MembershipType.Single => 90m,
            MembershipType.Junior => 35m,
            MembershipType.Honorary => 0m,
            MembershipType.Free => 35m,
            MembershipType.Patron => 35m,
            _ => null,
        };

    /// <summary>Active at <paramref name="at"/>: joined on or before it and not left on or before it.</summary>
    public static bool IsActive(backend.Person.Person p, DateOnly at) =>
        p.JoinedOn is { } joined && joined <= at && (p.LeftOn is null || p.LeftOn > at);

    public static BulkInvoicePlan Plan(
        IEnumerable<backend.Person.Person> persons,
        DateOnly at,
        IReadOnlyDictionary<int, MembershipDiscount>? discounts = null
    )
    {
        discounts ??= new Dictionary<int, MembershipDiscount>();
        var warnings = new List<string>();
        var members = ActiveInvoicedMembers(persons, at, warnings);
        var invoices = new List<PlannedInvoice>();
        foreach (var group in GroupByAddress(members, warnings))
        {
            var payerFamily = SelectFamilyPayer(group);
            var ordered = group.OrderBy(Seniority, SeniorityComparer).ToList();
            foreach (
                var p in ordered.Where(p =>
                    FeeOf(p, payerFamily) == 0 && discounts.ContainsKey(p.Id)
                )
            )
                warnings.Add($"{p.FullName} zahlt nichts; die Ermässigung wird ignoriert.");
            var lines = ordered
                .Select(p => BuildLine(p, FeeOf(p, payerFamily), discounts.GetValueOrDefault(p.Id)))
                .ToList();
            if (lines.Sum(l => l.Amount) <= 0)
                continue;
            invoices.Add(new PlannedInvoice(payerFamily ?? ordered[0], lines));
        }
        return new BulkInvoicePlan(invoices, warnings);
    }

    private static List<backend.Person.Person> ActiveInvoicedMembers(
        IEnumerable<backend.Person.Person> persons,
        DateOnly at,
        List<string> warnings
    )
    {
        var members = new List<backend.Person.Person>();
        foreach (var p in persons.Where(p => IsActive(p, at)).OrderBy(p => p.Id))
        {
            if (FeeFor(p.Membership) is null)
            {
                warnings.Add(
                    $"{p.FullName} hat die Mitgliedschaft Unbekannt und wird nicht verrechnet."
                );
                continue;
            }
            members.Add(p);
        }
        return members;
    }

    /// <summary>Groups keep first-seen order; a person with an incomplete address is a group of their own.</summary>
    private static List<List<backend.Person.Person>> GroupByAddress(
        List<backend.Person.Person> members,
        List<string> warnings
    )
    {
        var groups = new List<List<backend.Person.Person>>();
        var byAddress = new Dictionary<string, List<backend.Person.Person>>();
        foreach (var p in members)
        {
            var key = AddressKey(p);
            if (key is null)
            {
                warnings.Add(
                    $"{p.FullName} hat eine unvollständige Adresse und wird einzeln verrechnet."
                );
                groups.Add([p]);
            }
            else if (byAddress.TryGetValue(key, out var group))
            {
                group.Add(p);
            }
            else
            {
                group = [p];
                byAddress[key] = group;
                groups.Add(group);
            }
        }
        return groups;
    }

    /// <summary>
    /// The Familie member of an address who pays the Familie fee once: a flagged main payer first,
    /// then the longest-standing member.
    /// </summary>
    private static backend.Person.Person? SelectFamilyPayer(List<backend.Person.Person> group) =>
        group
            .Where(p => p.Membership == MembershipType.Family)
            .OrderByDescending(p => p.IsMainPayer)
            .ThenBy(Seniority, SeniorityComparer)
            .FirstOrDefault();

    /// <summary>The fee a Person pays: a Familie member other than the payer pays nothing.</summary>
    private static decimal FeeOf(backend.Person.Person p, backend.Person.Person? payerFamily) =>
        p.Membership == MembershipType.Family && p != payerFamily
            ? 0m
            : FeeFor(p.Membership)!.Value;

    private static InvoiceLineDraft BuildLine(
        backend.Person.Person p,
        decimal fee,
        MembershipDiscount? discount
    )
    {
        var text = $"{p.FullName} ({p.Membership.Label()})";
        var amount = fee;
        if (discount is not null && fee > 0)
        {
            amount =
                Math.Round(
                    amount * (100 - discount.Percent) / 100m * 20m,
                    MidpointRounding.AwayFromZero
                ) / 20m;
            text += $" – {discount.Percent}% {discount.Text}";
        }
        return new InvoiceLineDraft(text, amount, AccountNumber);
    }

    private static (DateOnly, int) Seniority(backend.Person.Person p) =>
        (p.JoinedOn ?? DateOnly.MaxValue, p.Id);

    private static readonly Comparer<(DateOnly, int)> SeniorityComparer = Comparer<(
        DateOnly,
        int
    )>.Default;

    private static string? AddressKey(backend.Person.Person p)
    {
        string?[] parts = [p.Street, p.Number, p.PostalCode, p.City, p.Country];
        if (parts.Any(string.IsNullOrWhiteSpace))
            return null;
        return string.Join('|', parts.Select(s => s!.Trim().ToLowerInvariant()));
    }
}
