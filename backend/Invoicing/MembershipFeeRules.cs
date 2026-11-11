using backend.Person;

namespace backend.Invoicing;

/// <summary>One Rechnung the Jahresbeiträge run will create: one per address, one line per paying Person.</summary>
public record PlannedInvoice(backend.Person.Person Recipient, IReadOnlyList<InvoiceLineDraft> Lines);

public record BulkInvoicePlan(IReadOnlyList<PlannedInvoice> Invoices, IReadOnlyList<string> Warnings);

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

    public static BulkInvoicePlan Plan(IEnumerable<backend.Person.Person> persons, DateOnly at)
    {
        var warnings = new List<string>();
        var members = new List<backend.Person.Person>();
        foreach (var p in persons.Where(p => IsActive(p, at)).OrderBy(p => p.Id))
        {
            if (FeeFor(p.Membership) is null)
            {
                warnings.Add($"{Name(p)} hat die Mitgliedschaft Unbekannt und wird nicht verrechnet.");
                continue;
            }
            members.Add(p);
        }

        // Groups keep first-seen order; a person with an incomplete address is a group of their own.
        var groups = new List<List<backend.Person.Person>>();
        var byAddress = new Dictionary<string, List<backend.Person.Person>>();
        foreach (var p in members)
        {
            var key = AddressKey(p);
            if (key is null)
            {
                warnings.Add($"{Name(p)} hat eine unvollständige Adresse und wird einzeln verrechnet.");
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

        var invoices = new List<PlannedInvoice>();
        foreach (var group in groups)
        {
            var payerFamily = group
                .Where(p => p.Membership == MembershipType.Family)
                .OrderBy(Seniority, SeniorityComparer)
                .FirstOrDefault();
            var payers = group
                .Where(p => p.Membership != MembershipType.Family || p == payerFamily)
                .Where(p => FeeFor(p.Membership) > 0)
                .OrderBy(Seniority, SeniorityComparer)
                .ToList();
            if (payers.Count == 0) continue;
            var lines = payers
                .Select(p => new InvoiceLineDraft(
                    $"Jahresbeitrag – {Name(p)} ({p.Membership.Label()})",
                    FeeFor(p.Membership)!.Value,
                    AccountNumber
                ))
                .ToList();
            invoices.Add(new PlannedInvoice(payerFamily is not null ? payerFamily : payers[0], lines));
        }
        return new BulkInvoicePlan(invoices, warnings);
    }

    private static (DateOnly, int) Seniority(backend.Person.Person p) => (p.JoinedOn ?? DateOnly.MaxValue, p.Id);

    private static readonly Comparer<(DateOnly, int)> SeniorityComparer = Comparer<(DateOnly, int)>.Default;

    private static string Name(backend.Person.Person p) => $"{p.FirstName} {p.LastName}";

    private static string? AddressKey(backend.Person.Person p)
    {
        string?[] parts = [p.Street, p.Number, p.PostalCode, p.City, p.Country];
        if (parts.Any(string.IsNullOrWhiteSpace)) return null;
        return string.Join('|', parts.Select(s => s!.Trim().ToLowerInvariant()));
    }
}
