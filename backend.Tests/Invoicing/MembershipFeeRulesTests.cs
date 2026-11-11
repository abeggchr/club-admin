using backend.Invoicing;
using backend.Person;

namespace backend.Tests.Invoicing;

public class MembershipFeeRulesTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);
    private static int nextId = 1000;

    private static backend.Person.Person P(
        string first,
        MembershipType type,
        string? joined = "2020-01-01",
        string? left = null,
        string? street = "Sportweg",
        string? number = "12",
        string? zip = "8000",
        string? city = "Zürich",
        string? country = "CH",
        int? id = null
    ) =>
        new()
        {
            Id = id ?? nextId++,
            FirstName = first,
            LastName = "Muster",
            Membership = type,
            JoinedOn = joined is null ? null : DateOnly.Parse(joined),
            LeftOn = left is null ? null : DateOnly.Parse(left),
            Street = street,
            Number = number,
            PostalCode = zip,
            City = city,
            Country = country,
        };

    [Theory]
    [InlineData(MembershipType.Family, 135)]
    [InlineData(MembershipType.Single, 90)]
    [InlineData(MembershipType.Junior, 35)]
    [InlineData(MembershipType.Free, 35)]
    [InlineData(MembershipType.Patron, 35)]
    public void A_member_is_invoiced_their_fee_booked_to_3400(MembershipType type, int fee)
    {
        var plan = MembershipFeeRules.Plan([P("Anna", type)], Start);

        var invoice = Assert.Single(plan.Invoices);
        var line = Assert.Single(invoice.Lines);
        Assert.Equal(fee, line.Amount);
        Assert.Equal(3400, line.AccountNumber);
        Assert.Equal($"Jahresbeitrag – Anna Muster ({type.Label()})", line.Text);
    }

    [Fact]
    public void Ehrenmitglieder_pay_nothing_so_no_Rechnung_is_created()
    {
        var plan = MembershipFeeRules.Plan([P("Anna", MembershipType.Honorary)], Start);

        Assert.Empty(plan.Invoices);
    }

    [Fact]
    public void Only_active_members_at_the_start_date_are_invoiced()
    {
        var plan = MembershipFeeRules.Plan(
            [
                P("NoJoin", MembershipType.Single, joined: null, street: "A"),
                P("Left", MembershipType.Single, left: "2025-12-31", street: "B"),
                P("Future", MembershipType.Single, joined: "2026-06-01", street: "C"),
                P("LeavesLater", MembershipType.Single, left: "2026-06-01", street: "D"),
                P("Active", MembershipType.Single, street: "E"),
            ],
            Start
        );

        Assert.Equal(["LeavesLater", "Active"], plan.Invoices.Select(i => i.Recipient.FirstName));
    }

    [Fact]
    public void Unbekannt_is_excluded_with_a_warning()
    {
        var plan = MembershipFeeRules.Plan([P("Anna", MembershipType.Unknown)], Start);

        Assert.Empty(plan.Invoices);
        Assert.Equal(["Anna Muster hat die Mitgliedschaft Unbekannt und wird nicht verrechnet."], plan.Warnings);
    }

    [Fact]
    public void Persons_at_the_same_address_get_one_Rechnung_with_a_line_each()
    {
        var plan = MembershipFeeRules.Plan(
            [
                P("Anna", MembershipType.Single),
                P("Ben", MembershipType.Junior, street: " sportweg ", city: "ZÜRICH", country: "ch"),
            ],
            Start
        );

        var invoice = Assert.Single(plan.Invoices);
        Assert.Equal([90m, 35m], invoice.Lines.Select(l => l.Amount));
    }

    [Fact]
    public void Persons_with_an_incomplete_address_are_never_grouped_and_a_warning_is_given()
    {
        var plan = MembershipFeeRules.Plan(
            [
                P("Anna", MembershipType.Single, street: null),
                P("Ben", MembershipType.Single, street: null),
            ],
            Start
        );

        Assert.Equal(2, plan.Invoices.Count);
        Assert.Equal(2, plan.Warnings.Count);
        Assert.Contains("Anna Muster", plan.Warnings[0]);
        Assert.Contains("Adresse", plan.Warnings[0]);
    }

    [Fact]
    public void At_one_address_only_the_earliest_Familienmitglied_pays_and_is_the_recipient()
    {
        var plan = MembershipFeeRules.Plan(
            [
                P("Later", MembershipType.Family, joined: "2022-01-01"),
                P("Junior", MembershipType.Junior, joined: "2010-01-01"),
                P("First", MembershipType.Family, joined: "2018-01-01", id: 1),
                P("Tie", MembershipType.Family, joined: "2018-01-01", id: 2),
            ],
            Start
        );

        var invoice = Assert.Single(plan.Invoices);
        Assert.Equal("First", invoice.Recipient.FirstName);
        Assert.Equal(2, invoice.Lines.Count);
        Assert.Equal(170m, invoice.Lines.Sum(l => l.Amount));
        Assert.Contains(invoice.Lines, l => l.Text!.Contains("First Muster"));
        Assert.Contains(invoice.Lines, l => l.Text!.Contains("Junior Muster"));
    }

    [Fact]
    public void Without_a_Familienmitglied_the_recipient_is_the_earliest_paying_person()
    {
        var plan = MembershipFeeRules.Plan(
            [
                P("Late", MembershipType.Single, joined: "2022-01-01"),
                P("Honorary", MembershipType.Honorary, joined: "2000-01-01"),
                P("Early", MembershipType.Junior, joined: "2015-01-01"),
            ],
            Start
        );

        var invoice = Assert.Single(plan.Invoices);
        Assert.Equal("Early", invoice.Recipient.FirstName);
        Assert.Equal(2, invoice.Lines.Count);
    }

    [Fact]
    public void Ties_in_JoinedOn_go_to_the_lowest_id()
    {
        var plan = MembershipFeeRules.Plan(
            [
                P("B", MembershipType.Single, id: 20),
                P("A", MembershipType.Single, id: 10),
            ],
            Start
        );

        Assert.Equal("A", Assert.Single(plan.Invoices).Recipient.FirstName);
    }
}
