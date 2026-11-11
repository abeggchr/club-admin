using backend.Person;

namespace backend.Tests.Person;

public class PersonCsvImporterTests
{
    [Fact]
    public void An_Adresse_is_split_into_Street_and_Number()
    {
        var (street, number) = ParseAddress("Musterweg 12a");
        Assert.Equal("Musterweg", street);
        Assert.Equal("12a", number);
    }

    [Fact]
    public void A_multi_word_street_splits_at_the_last_token_that_starts_with_a_digit()
    {
        var (street, number) = ParseAddress("Im Gäslein 4");
        Assert.Equal("Im Gäslein", street);
        Assert.Equal("4", number);
    }

    [Fact]
    public void An_Adresse_that_cannot_be_split_goes_whole_into_Street()
    {
        var (street, number) = ParseAddress("Musterweg");
        Assert.Equal("Musterweg", street);
        Assert.Null(number);
    }

    [Fact]
    public void A_blank_Adresse_leaves_Street_and_Number_null()
    {
        var (street, number) = ParseAddress("");
        Assert.Null(street);
        Assert.Null(number);
    }

    [Fact]
    public void A_blank_Mitgliedschaft_stays_Unknown()
    {
        Assert.Equal(MembershipType.Unknown, ParseMembership(""));
    }

    [Theory]
    [InlineData("Familienmitglied", MembershipType.Family)]
    [InlineData("Einzelmitglied", MembershipType.Single)]
    [InlineData("Junior:innen", MembershipType.Junior)]
    [InlineData("Ehrenmitglied", MembershipType.Honorary)]
    [InlineData("Freimitglied", MembershipType.Free)]
    [InlineData("Gönner:innen", MembershipType.Patron)]
    public void A_known_Mitgliedschaft_is_stored_as_the_matching_type(string value, MembershipType expected)
    {
        Assert.Equal(expected, ParseMembership(value));
    }

    [Fact]
    public void A_Folgemitglied_imports_as_Family()
    {
        Assert.Equal(MembershipType.Family, ParseMembership("Folgemitglied"));
    }

    [Fact]
    public void A_Gönner_imports_as_Patron()
    {
        Assert.Equal(MembershipType.Patron, ParseMembership("Gönner"));
    }

    [Fact]
    public void An_unknown_Mitgliedschaft_rejects_the_file()
    {
        var csv = "Vorname;Nachname;Mitgliedschaft\nAnna;Meier;Zuschauer";
        var ex = Assert.Throws<FormatException>(() => PersonCsvImporter.Parse(new StringReader(csv)));
        Assert.Contains("Zuschauer", ex.Message);
    }

    // Parses a one-row member CSV with the given "Mitgliedschaft" value and returns the stored membership.
    static MembershipType ParseMembership(string membership)
    {
        var csv = $"Vorname;Nachname;Mitgliedschaft\nAnna;Meier;{membership}";
        return PersonCsvImporter.Parse(new StringReader(csv)).Single().Membership;
    }

    // Parses a one-row member CSV with the given "Adresse" value and returns the stored address parts.
    static (string? Street, string? Number) ParseAddress(string address)
    {
        var csv = $"Vorname;Nachname;Adresse\nAnna;Meier;{address}";
        var person = PersonCsvImporter.Parse(new StringReader(csv)).Single();
        return (person.Street, person.Number);
    }
}
