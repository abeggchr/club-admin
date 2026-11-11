using backend.Invoicing;
using backend.Person;

namespace backend.Tests.Invoicing;

public class MembershipDiscountTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);

    private static backend.Person.Person P(int id, string first, MembershipType type, string street = "Sportweg") =>
        new()
        {
            Id = id,
            FirstName = first,
            LastName = "Muster",
            Membership = type,
            JoinedOn = new DateOnly(2020, 1, 1),
            Street = street,
            Number = "12",
            PostalCode = "8000",
            City = "Zürich",
            Country = "CH",
        };

    private static IReadOnlyDictionary<int, MembershipDiscount> Discounts(params MembershipDiscount[] d) =>
        d.ToDictionary(x => x.PersonId);

    [Fact]
    public void A_discount_reduces_the_amount_and_is_named_on_the_line()
    {
        var plan = MembershipFeeRules.Plan(
            [P(1, "Anna", MembershipType.Single)],
            Start,
            Discounts(new MembershipDiscount { PersonId = 1, Percent = 20, Text = "Studentin" })
        );

        var line = Assert.Single(Assert.Single(plan.Invoices).Lines);
        Assert.Equal(72m, line.Amount);
        Assert.Equal("Jahresbeitrag – Anna Muster (Einzelmitglied) – 20 % Ermässigung: Studentin", line.Text);
    }

    [Fact]
    public void The_discounted_amount_is_a_multiple_of_5_Rappen()
    {
        // 135 * 0.67 = 90.45; 35 * 0.67 = 23.45
        var plan = MembershipFeeRules.Plan(
            [P(1, "Anna", MembershipType.Family), P(2, "Ben", MembershipType.Junior, "Anderswo")],
            Start,
            Discounts(
                new MembershipDiscount { PersonId = 1, Percent = 33, Text = "x" },
                new MembershipDiscount { PersonId = 2, Percent = 33, Text = "y" }
            )
        );

        Assert.Equal([90.45m, 23.45m], plan.Invoices.Select(i => i.Lines[0].Amount));
    }

    [Fact]
    public void A_discount_on_a_Person_who_pays_0_is_ignored_with_a_warning()
    {
        var plan = MembershipFeeRules.Plan(
            [P(1, "Anna", MembershipType.Family), P(2, "Ben", MembershipType.Family)],
            Start,
            Discounts(new MembershipDiscount { PersonId = 2, Percent = 50, Text = "x" })
        );

        var lines = Assert.Single(plan.Invoices).Lines;
        Assert.Equal([135m, 0m], lines.Select(l => l.Amount));
        Assert.DoesNotContain(lines, l => l.Text!.Contains("Ermässigung"));
        Assert.Contains(plan.Warnings, w => w.Contains("Ben Muster") && w.Contains("Ermässigung"));
    }

    [Fact]
    public void A_100_percent_discount_creates_no_Rechnung_when_the_total_is_0()
    {
        var plan = MembershipFeeRules.Plan(
            [P(1, "Anna", MembershipType.Single)],
            Start,
            Discounts(new MembershipDiscount { PersonId = 1, Percent = 100, Text = "x" })
        );

        Assert.Empty(plan.Invoices);
    }

    [Theory]
    [InlineData(0, "x")]
    [InlineData(101, "x")]
    [InlineData(null, "x")]
    [InlineData(20, "")]
    [InlineData(20, "  ")]
    [InlineData(20, null)]
    public void Invalid_percent_or_missing_text_is_refused(int? percent, string? text)
    {
        Assert.NotNull(MembershipDiscountRules.Validate(1, percent, text));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void Percent_from_1_to_100_with_text_is_valid(int percent)
    {
        Assert.Null(MembershipDiscountRules.Validate(1, percent, "Studentin"));
    }
}
