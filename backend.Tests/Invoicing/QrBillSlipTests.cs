using backend.Invoicing;

namespace backend.Tests.Invoicing;

public class QrBillSlipTests
{
    private static readonly QrBillCreditor Club = new(
        "CH9300762011623852957",
        "SC Wettingen",
        "Sportweg 12",
        "5430",
        "Wettingen"
    );

    [Fact]
    public void Reference_is_a_valid_SCOR_reference_ending_in_the_Rechnungsnummer()
    {
        var bill = QrBillSlip.Build(Club, "0042", 100m, null);

        // Worked example: "0042" + "RF00" -> 42271500; 98 - (42271500 mod 97 = 64) = 34.
        Assert.Equal("RF340042", bill.Reference);
    }

    private static readonly QrBillDebtor Hans = new(
        "Hans Muster",
        "Dorfstrasse",
        "1",
        "8000",
        "Zürich",
        "CH"
    );

    [Fact]
    public void Complete_Person_address_makes_the_Person_the_debtor()
    {
        var debtor = QrBillSlip.DebtorFor(
            "Hans",
            "Muster",
            "Dorfstrasse",
            "1",
            "8000",
            "Zürich",
            "Schweiz"
        );

        Assert.Equal(Hans, debtor);
        var bill = QrBillSlip.Build(Club, "0042", 100m, debtor);
        Assert.Equal("Hans Muster", bill.Debtor.Name);
        Assert.Equal("Dorfstrasse", bill.Debtor.Street);
        Assert.Equal("1", bill.Debtor.HouseNo);
        Assert.Equal("8000", bill.Debtor.PostalCode);
        Assert.Equal("Zürich", bill.Debtor.Town);
        Assert.Equal("CH", bill.Debtor.CountryCode);
    }

    [Theory]
    [InlineData(null, "1", "8000", "Zürich", "CH")]
    [InlineData("Dorfstrasse", " ", "8000", "Zürich", "CH")]
    [InlineData("Dorfstrasse", "1", null, "Zürich", "CH")]
    [InlineData("Dorfstrasse", "1", "8000", "", "CH")]
    [InlineData("Dorfstrasse", "1", "8000", "Zürich", null)]
    [InlineData("Dorfstrasse", "1", "8000", "Zürich", "Atlantis")]
    public void Incomplete_Person_address_leaves_the_debtor_out(
        string? street,
        string? number,
        string? postalCode,
        string? city,
        string? country
    )
    {
        var debtor = QrBillSlip.DebtorFor(
            "Hans",
            "Muster",
            street,
            number,
            postalCode,
            city,
            country
        );

        Assert.Null(debtor);
        Assert.Null(QrBillSlip.Build(Club, "0042", 100m, debtor).Debtor);
    }

    [Fact]
    public void Club_address_line_is_split_into_street_and_house_number()
    {
        var creditor = QrBillSlip.Build(Club, "0042", 100m, null).Creditor;

        Assert.Equal("Sportweg", creditor.Street);
        Assert.Equal("12", creditor.HouseNo);
        Assert.Equal("CH", creditor.CountryCode);
    }

    [Fact]
    public void Club_address_without_house_number_keeps_the_whole_line_as_street()
    {
        var club = Club with { Address = "Postfach" };

        var creditor = QrBillSlip.Build(club, "0042", 100m, null).Creditor;

        Assert.Equal("Postfach", creditor.Street);
        Assert.Null(creditor.HouseNo);
    }

    [Fact]
    public void Settings_with_a_complete_Klubadresse_give_the_creditor()
    {
        var settings = new backend.Settings.Settings
        {
            Iban = "CH9300762011623852957",
            Name = " SC Wettingen ",
            Address = "Sportweg 12",
            PostalCode = "5430",
            City = "Wettingen",
        };

        Assert.Equal(Club with { Name = "SC Wettingen" }, QrBillCreditor.From(settings));
    }

    [Theory]
    [InlineData("", "Sportweg 12", "5430", "Wettingen")]
    [InlineData("SC Wettingen", " ", "5430", "Wettingen")]
    [InlineData("SC Wettingen", "Sportweg 12", "", "Wettingen")]
    [InlineData("SC Wettingen", "Sportweg 12", "5430", "")]
    public void Missing_Klubadresse_gives_no_creditor_and_the_German_error(
        string name,
        string address,
        string postalCode,
        string city
    )
    {
        var settings = new backend.Settings.Settings
        {
            Iban = "CH9300762011623852957",
            Name = name,
            Address = address,
            PostalCode = postalCode,
            City = city,
        };

        Assert.Null(QrBillCreditor.From(settings));
        Assert.Null(QrBillCreditor.From(null));
        // POST /invoices answers 400 with this error before InvoiceCreator is called, so no Rechnung is created.
        // (The endpoint needs PostgreSQL, so the decision is tested through Resolve.)
        foreach (var s in new backend.Settings.Settings?[] { settings, null })
        {
            var (creditor, error) = QrBillCreditor.Resolve(s);
            Assert.Null(creditor);
            Assert.Equal("Bitte zuerst die Klubadresse in den Einstellungen erfassen", error);
        }
    }

    [Fact]
    public void Complete_Klubadresse_resolves_to_the_creditor_without_error()
    {
        var settings = new backend.Settings.Settings
        {
            Iban = Club.Iban,
            Name = Club.Name,
            Address = Club.Address,
            PostalCode = Club.PostalCode,
            City = Club.City,
        };

        Assert.Equal((Club, (string?)null), QrBillCreditor.Resolve(settings));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(2147483647)] // int.MaxValue: the longest Rechnungsnummer (10 digits)
    public void Reference_building_never_throws_for_any_Rechnungsnummer_the_system_produces(int id)
    {
        // Rechnungsnummer is InvoiceRules.FormatNumber(id): the ID zero-padded to at least 4 digits, so always
        // numeric and at most 10 characters; "RF" + 2 check digits + that is far below the SCOR limit of 25.
        var number = InvoiceRules.FormatNumber(id);

        var bill = QrBillSlip.Build(Club, number, 100m, null);

        Assert.StartsWith("RF", bill.Reference);
        Assert.EndsWith(number, bill.Reference);
    }

    [Fact]
    public void Message_names_the_Rechnung()
    {
        var bill = QrBillSlip.Build(Club, "0042", 100m, null);

        Assert.Equal("Rechnung #0042", bill.UnstructuredMessage);
    }

    [Fact]
    public void Amount_is_carried_into_the_QR_code()
    {
        var bill = QrBillSlip.Build(Club, "0042", 1334.50m, null);

        Assert.Equal(1334.50m, bill.Amount);
    }

    [Fact]
    public void Open_amount_leaves_the_amount_out_of_the_QR_code()
    {
        var bill = QrBillSlip.Build(Club, "0042", null, null);

        Assert.Null(bill.Amount);
    }
}
