using backend.Invoicing;

namespace backend.Person;

/// <summary>One Mitgliedschaft of the dashboard summary with its count of active Personen.</summary>
public record MembershipSummary(MembershipType Membership, int Count);

public record PersonSummary(int Total, IReadOnlyList<MembershipSummary> ByMembership);

/// <summary>Pure rules for the dashboard: which Personen count as active and how they are tallied per Mitgliedschaft.</summary>
public static class PersonSummaryRules
{
    /// <summary>
    /// The dashboard's active Personen: <see cref="MembershipFeeRules.IsActive"/> per Person.
    /// A Person without an Eintritt never counts, whatever their Mitgliedschaft.
    /// </summary>
    public static IReadOnlyList<Person> Active(IReadOnlyList<Person> persons, DateOnly at) =>
        persons.Where(p => MembershipFeeRules.IsActive(p, at)).ToList();

    /// <summary>
    /// The active Personen tallied per Mitgliedschaft: 0 counts are left out,
    /// sorted by count descending, ties in enum order.
    /// </summary>
    public static IReadOnlyList<MembershipSummary> ByMembership(IEnumerable<Person> persons) =>
        persons
            .GroupBy(p => p.Membership)
            .Select(g => new MembershipSummary(g.Key, g.Count()))
            .Where(s => s.Count > 0)
            .OrderByDescending(s => s.Count)
            .ThenBy(s => s.Membership)
            .ToList();

    public static PersonSummary Summarize(IReadOnlyList<Person> persons, DateOnly at)
    {
        var active = Active(persons, at);
        return new PersonSummary(active.Count, ByMembership(active));
    }
}
