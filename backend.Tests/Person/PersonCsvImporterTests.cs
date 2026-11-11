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

    // Parses a one-row member CSV with the given "Adresse" value and returns the stored address parts.
    static (string? Street, string? Number) ParseAddress(string address)
    {
        var csv = $"Vorname;Nachname;Adresse\nAnna;Meier;{address}";
        var person = PersonCsvImporter.Parse(new StringReader(csv)).Single();
        return (person.Street, person.Number);
    }
}
