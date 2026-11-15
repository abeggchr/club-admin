using backend.Invoicing;
using backend.Person;
using PersonEntity = backend.Person.Person;

namespace backend.Tests.Person;

public class PersonSummaryRulesTests
{
    private static int nextId = 1000;

    private static PersonEntity P(MembershipType type, string? joined, string? left = null) =>
        new()
        {
            Id = nextId++,
            FirstName = "M",
            LastName = "Muster",
            Membership = type,
            JoinedOn = joined is null ? null : DateOnly.Parse(joined),
            LeftOn = left is null ? null : DateOnly.Parse(left),
        };

    [Fact]
    public void A_Person_without_an_Eintritt_is_never_active()
    {
        var p = P(MembershipType.Single, null);

        Assert.False(MembershipFeeRules.IsActive(p, new DateOnly(2026, 6, 15)));
        Assert.Empty(PersonSummaryRules.Summarize([p], new DateOnly(2026, 6, 15)).ByMembership);
    }

    [Theory]
    [InlineData("2020-01-01", null)]
    [InlineData("2020-01-01", "2026-06-16")]
    [InlineData("2026-06-15", null)]
    public void An_Eintritt_on_the_day_and_an_Austritt_the_day_after_stay_active(string joined, string? left)
    {
        Assert.True(MembershipFeeRules.IsActive(P(MembershipType.Single, joined, left), new DateOnly(2026, 6, 15)));
    }

    [Theory]
    [InlineData("2026-06-16", null)]
    [InlineData("2020-01-01", "2026-06-15")]
    [InlineData("2026-06-14", "2026-06-15")]
    public void A_future_Eintritt_and_an_Austritt_on_the_day_are_inactive(string joined, string? left)
    {
        Assert.False(MembershipFeeRules.IsActive(P(MembershipType.Single, joined, left), new DateOnly(2026, 6, 15)));
    }

    [Fact]
    public void The_summary_counts_every_active_Mitgliedschaft_including_Unknown()
    {
        var summary = PersonSummaryRules.Summarize(
            [
                P(MembershipType.Single, "2020-01-01"),
                P(MembershipType.Single, "2021-01-01"),
                P(MembershipType.Family, "2019-01-01"),
                P(MembershipType.Unknown, "2018-01-01"),
                P(MembershipType.Single, "2026-06-16"),
                P(MembershipType.Family, "2020-01-01", "2026-01-01"),
                P(MembershipType.Single, null),
            ],
            new DateOnly(2026, 6, 15)
        );

        Assert.Equal(4, summary.Total);
        Assert.Equal(
            [
                new MembershipSummary(MembershipType.Single, 2),
                new MembershipSummary(MembershipType.Unknown, 1),
                new MembershipSummary(MembershipType.Family, 1),
            ],
            summary.ByMembership
        );
    }

    [Fact]
    public void The_counts_are_sorted_descending_with_ties_in_enum_order()
    {
        var summary = PersonSummaryRules.Summarize(
            [
                P(MembershipType.Patron, "2020-01-01"),
                P(MembershipType.Patron, "2020-01-02"),
                P(MembershipType.Family, "2020-01-03"),
                P(MembershipType.Single, "2020-01-04"),
                P(MembershipType.Junior, "2020-01-05"),
                P(MembershipType.Junior, "2020-01-06"),
            ],
            new DateOnly(2026, 6, 15)
        );

        Assert.Equal(
            [
                new MembershipSummary(MembershipType.Junior, 2),
                new MembershipSummary(MembershipType.Patron, 2),
                new MembershipSummary(MembershipType.Family, 1),
                new MembershipSummary(MembershipType.Single, 1),
            ],
            summary.ByMembership
        );
    }

    [Fact]
    public void Empty_memberships_leave_the_summary_empty()
    {
        var summary = PersonSummaryRules.Summarize(
            [P(MembershipType.Single, null)],
            new DateOnly(2026, 6, 15)
        );

        Assert.Equal(0, summary.Total);
        Assert.Empty(summary.ByMembership);
    }
}
