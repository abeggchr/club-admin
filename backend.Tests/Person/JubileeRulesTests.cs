using Member = backend.Person.Person;
using backend.Person;

namespace backend.Tests.Person;

public class JubileeRulesTests
{
    private static Member Member(
        int id,
        string? birthdate = null,
        string? joined = null,
        string? left = null,
        string firstName = "Vor",
        string lastName = "Nach"
    ) => new()
    {
        Id = id,
        FirstName = firstName,
        LastName = lastName,
        DateOfBirth = birthdate is null ? null : DateOnly.Parse(birthdate),
        JoinedOn = joined is null ? null : DateOnly.Parse(joined),
        LeftOn = left is null ? null : DateOnly.Parse(left),
    };

    private static Jubilees Compute(Member[] persons, string today)
    {
        return JubileeRules.Compute(persons, StartDate, DateOnly.Parse(today));
    }

    private static readonly DateOnly StartDate = new(2003, 4, 1);

    [Fact]
    public void A_person_joined_in_2010_is_a_jubilee_in_the_club_year_starting_2020()
    {
        var result = Compute([Member(7, joined: "2010-05-01")], today: "2020-06-15");

        Assert.Single(result.JubileesRows);
        Assert.Equal(2020, result.ClubYearStartYear);
        Assert.Equal(7, result.JubileesRows.Single()!.PersonId);
        Assert.Equal(10, result.JubileesRows.Single().Years);
        Assert.Equal(new DateOnly(2010, 5, 1), result.JubileesRows.Single()!.EntryDate);
        Assert.Empty(result.Anniversaries);
    }

    [Fact]
    public void A_person_whose_milestone_falls_at_the_club_year_start_appears_that_year_not_the_previous()
    {
        var previous = Compute([Member(1, joined: "2010-01-01")], today: "2020-03-31");
        var current = Compute([Member(1, joined: "2010-01-01")], today: "2020-04-01");

        Assert.Empty(previous.JubileesRows);
        Assert.Single(current.JubileesRows);
        Assert.Equal(10, current.JubileesRows.Single().Years);
    }

    [Fact]
    public void Left_members_are_excluded_from_both_tables()
    {
        var result = Compute(
            [
                Member(1, birthdate: "1950-05-01", joined: "2010-05-01", left: "2019-12-31"),
                Member(2, birthdate: "1960-05-01", joined: "2010-05-01", left: "2021-06-30"),
            ],
            today: "2020-06-15"
        );

        Assert.Empty(result.Anniversaries);
        Assert.Empty(result.JubileesRows);
    }

    [Fact]
    public void Missing_dates_are_skipped_per_table_independently()
    {
        // No JoinedOn: skipped for Jubilee, counted for Anniversary.
        var noJoined = Compute([Member(1, birthdate: "1950-05-01")], today: "2020-06-15");
        Assert.Single(noJoined.Anniversaries);
        Assert.Empty(noJoined.JubileesRows);

        // No DateOfBirth: skipped for Anniversary, counted for Jubilee.
        var noBirth = Compute([Member(1, joined: "2010-05-01")], today: "2020-06-15");
        Assert.Empty(noBirth.Anniversaries);
        Assert.Single(noBirth.JubileesRows);
    }

    [Fact]
    public void Milestones_from_10_to_90_are_included_with_no_floor_for_birth_age()
    {
        var today = "2020-06-15";

        foreach (var milestone in new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90 })
        {
            var bornYear = 2020 - milestone;
            var person = Member(milestone, birthdate: $"{bornYear}-05-01");
            var result = Compute([person], today: today);

            Assert.Single(result.Anniversaries);
            Assert.Equal(milestone, result.Anniversaries.Single()!.Years);
        }
    }

    [Fact]
    public void Non_milestones_are_excluded()
    {
        var result = Compute(
            [
                Member(1, birthdate: "1995-05-01", joined: "2005-05-01"), // 25 years on both counts
                Member(2, birthdate: "2011-05-01", joined: "2011-05-01"), // 9 years
            ],
            today: "2020-06-15"
        );

        Assert.Empty(result.Anniversaries);
        Assert.Empty(result.JubileesRows);
    }

    [Fact]
    public void Inactive_members_are_excluded_even_when_both_milestones_match()
    {
        var left = Compute(
            [Member(1, birthdate: "1950-05-01", joined: "2010-05-01", left: "2021-06-30")],
            today: "2020-06-15"
        );
        Assert.Empty(left.Anniversaries);
        Assert.Empty(left.JubileesRows);

        var active = Compute(
            [Member(1, birthdate: "1950-05-01", joined: "2010-05-01")],
            today: "2020-06-15"
        );
        Assert.Single(active.Anniversaries);
        Assert.Single(active.JubileesRows);
    }

    [Fact]
    public void Rows_carry_the_persons_full_name_and_the_result_covers_the_current_club_year()
    {
        var result = Compute(
            [Member(3, birthdate: "1956-09-01", firstName: "Frida", lastName: "Muster")],
            today: "2026-04-01"
        );

        Assert.Equal(2026, result.ClubYearStartYear);
        var anniversary = result.Anniversaries.Single();
        Assert.Equal(70, anniversary.Years);
        Assert.Equal("Frida", anniversary.FirstName);
        Assert.Equal("Muster", anniversary.LastName);
        Assert.Equal(new DateOnly(1956, 9, 1), anniversary!.Birthdate);
    }
}
