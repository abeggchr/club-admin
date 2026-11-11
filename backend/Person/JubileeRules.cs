namespace backend.Person;

/// <summary>The Jubiliäen of the current club year: 10-year milestones of active members.</summary>
public class Jubilees
{
    public required int ClubYearStartYear { get; init; }
    public required IReadOnlyList<AnniversaryRow> Anniversaries { get; init; }
    public required IReadOnlyList<JubileeRow> JubileesRows { get; init; }
}

public class AnniversaryRow
{
    public required int PersonId { get; init; }
    public required int Years { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required DateOnly Birthdate { get; init; }
}

public class JubileeRow
{
    public required int PersonId { get; init; }
    public required int Years { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required DateOnly EntryDate { get; init; }
}

public static class JubileeRules
{
    /// <summary>
    /// The years of a milestone = the calendar year in which the current club year starts minus
    /// the year of the relevant date. A member counts iff active (LeftOn null), the date is set,
    /// and the years are a positive multiple of ten.
    /// </summary>
    public static Jubilees Compute(
        IEnumerable<Person> persons,
        DateOnly clubYearStart,
        DateOnly today
    )
    {
        var thisYearStart = new DateOnly(today.Year, clubYearStart.Month, clubYearStart.Day);
        var startYear = today >= thisYearStart ? today.Year : today.Year - 1;

        var anniversaries = new List<AnniversaryRow>();
        var jubilees = new List<JubileeRow>();

        foreach (var person in persons.Where(p => p.LeftOn is null))
        {
            if (person.DateOfBirth is { } birth)
            {
                var years = startYear - birth.Year;
                if (years > 0 && years % 10 == 0)
                {
                    anniversaries.Add(
                        new AnniversaryRow
                        {
                            PersonId = person.Id,
                            Years = years,
                            FirstName = person.FirstName,
                            LastName = person.LastName,
                            Birthdate = birth,
                        }
                    );
                }
            }

            if (person.JoinedOn is { } joined)
            {
                var years = startYear - joined.Year;
                if (years > 0 && years % 10 == 0)
                {
                    jubilees.Add(
                        new JubileeRow
                        {
                            PersonId = person.Id,
                            Years = years,
                            FirstName = person.FirstName,
                            LastName = person.LastName,
                            EntryDate = joined,
                        }
                    );
                }
            }
        }

        return new Jubilees
        {
            ClubYearStartYear = startYear,
            Anniversaries = anniversaries,
            JubileesRows = jubilees,
        };
    }
}
