using backend.Invoicing;
using backend.Person;

namespace backend.Tests.Person;

public class ClubNewsRulesTests
{
    private static readonly DateOnly At = new(2026, 6, 1);
    private static int nextId = 1000;

    private static backend.Person.Person P(
        string first,
        MembershipType type = MembershipType.Single,
        string? joined = "2020-01-01",
        string? left = null,
        bool newsletter = false,
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
            ReceivesClubNews = newsletter,
            Street = street,
            Number = number,
            PostalCode = zip,
            City = city,
            Country = country,
        };

    private static MailingListRules.Row[] Rows(backend.Person.Person[] recipients) =>
        MailingListRules.BuildRows(ClubNewsRecipientRules.Recipients(recipients, At)).ToArray();

    [Fact]
    public void An_active_member_is_a_recipient()
    {
        var rows = Rows([P("Anna")]);
        var row = Assert.Single(rows);
        Assert.Equal("Anna Muster", row.Recipients);
    }

    [Fact]
    public void A_person_with_an_Austritt_in_the_past_is_no_recipient_unless_flagged()
    {
        Assert.Empty(Rows([P("Anna", left: "2025-12-31")]));
        Assert.Single(Rows([P("Anna", left: "2025-12-31", newsletter: true)]));
    }

    [Fact]
    public void An_active_person_with_an_Austritt_in_the_future_is_a_recipient()
    {
        Assert.Single(Rows([P("Anna", left: "2026-07-31")]));
    }

    [Fact]
    public void A_flagged_person_who_never_joined_is_a_recipient()
    {
        Assert.Single(Rows([P("Anna", joined: null, newsletter: true)]));
    }

    [Fact]
    public void An_active_person_with_membership_unknown_is_a_recipient()
    {
        Assert.Single(Rows([P("Anna", MembershipType.Unknown)]));
    }

    [Fact]
    public void An_unflagged_contact_is_not_a_recipient()
    {
        Assert.Empty(Rows([P("Anna", joined: null)]));
    }

    [Fact]
    public void Personen_with_the_same_complete_address_become_one_row_with_one_name_per_line()
    {
        var rows = Rows(
        [
            P("Anna", id: 1),
            P("Ben", id: 2),
            P("Clara", id: 3, street: "sportweg", number: "12", zip: "8000", city: "zürich"),
        ]);

        var row = Assert.Single(rows);
        Assert.Equal("Anna Muster\nBen Muster\nClara Muster", row.Recipients);
        Assert.Equal("Sportweg 12", row.Address);
        Assert.Equal("8000 Zürich", row.Ort);
        Assert.Equal("CH", row.Land);
    }

    [Fact]
    public void The_same_address_in_another_Land_is_one_row_too()
    {
        var rows = Rows([P("Anna"), P("Ben", country: "LI")]);
        var row = Assert.Single(rows);
        Assert.Equal("Anna Muster\nBen Muster", row.Recipients);
        // The Land of the first Person of the address is printed.
        Assert.Equal("CH", row.Land);
    }

    [Fact]
    public void Different_addresses_get_different_rows()
    {
        var rows = Rows([P("Anna"), P("Ben", city: "Bern")]);
        Assert.Equal(2, rows.Count());
    }

    [Fact]
    public void A_person_with_an_incomplete_address_gets_a_row_of_their_own_with_the_parts_that_exist()
    {
        var rows = Rows(
        [
            P("Anna", number: null),
            P("Ben"),
        ]);

        Assert.Equal(2, rows.Count());
        var anna = Assert.Single(rows, r => r.Recipients == "Anna Muster");
        Assert.Equal("Sportweg", anna.Address);
        Assert.Equal("8000 Zürich", anna.Ort);
    }

    [Fact]
    public void A_person_without_any_address_gets_a_row_with_empty_address_cells()
    {
        var row = Assert.Single(
            MailingListRules.BuildRows([P("Anna", street: null, number: null, zip: null, city: null, country: null)])
        );
        Assert.Equal("Anna Muster", row.Recipients);
        Assert.Equal("", row.Address);
        Assert.Equal("", row.Ort);
        Assert.Equal("", row.Land);
    }

    [Fact]
    public void The_rows_of_the_mailing_list_are_semicolon_separated_with_the_spec_headers()
    {
        var csv = MailingListCsv.Build(
            [
                new MailingListRules.Row(
                    "Anna Muster\nBen Muster",
                    "Sportweg 12",
                    "8000 Zürich",
                    "CH"
                )
            ]
        );

        Assert.Equal(
            "Empfänger;Adresse;Ort;Land\r\n\"Anna Muster\nBen Muster\";Sportweg 12;8000 Zürich;CH",
            csv
        );
    }

}
