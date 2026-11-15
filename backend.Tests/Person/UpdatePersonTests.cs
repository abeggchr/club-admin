using System.Text.Json;
using backend.Person;
using backend.Tests.Invoicing;
using Microsoft.EntityFrameworkCore;
using PersonEntity = backend.Person.Person;

namespace backend.Tests.Person;

/// <summary>Controller-level tests for changing a Person (Payer, ticket #66).</summary>
public class UpdatePersonTests(LocalPostgresFixture fixture) : PersonControllerTestBase(fixture)
{
    private static PersonUpdate Update(PersonEntity p, int? payerId) =>
        new(
            p.FirstName,
            p.LastName,
            p.Membership,
            null,
            null,
            null,
            null,
            false,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            payerId,
            false
        );

    private static int? StoredPayer(AppDbContext db, int id) =>
        db.Persons.AsNoTracking().Single(p => p.Id == id).PayerId;

    [Fact]
    public async Task Setting_a_Zahler_stores_it()
    {
        await using var db = Database();
        var payer = Add(db, "Payer");
        var child = Add(db, "Child");

        var (status, _) = await Run(
            await new PersonController(db).UpdateAsync(child.Id, Update(child, payer.Id))
        );

        Assert.Equal(200, status);
        Assert.Equal(payer.Id, StoredPayer(Database(), child.Id));
    }

    [Fact]
    public async Task Sending_no_Zahler_clears_it()
    {
        await using var db = Database();
        var payer = Add(db, "Payer");
        var child = Add(db, "Child", payer.Id);

        var (status, _) = await Run(
            await new PersonController(db).UpdateAsync(child.Id, Update(child, null))
        );

        Assert.Equal(200, status);
        Assert.Null(StoredPayer(Database(), child.Id));
    }

    [Fact]
    public async Task An_invalid_Zahler_is_rejected_with_400_and_nothing_is_stored()
    {
        await using var db = Database();
        var person = Add(db, "Self");

        var (status, body) = await Run(
            await new PersonController(db).UpdateAsync(person.Id, Update(person, person.Id))
        );

        Assert.Equal(400, status);
        Assert.Contains("error", body);
        Assert.Null(StoredPayer(Database(), person.Id));
    }

    [Fact]
    public async Task A_missing_name_is_rejected_with_400()
    {
        await using var db = Database();
        var person = Add(db, "Name");

        var (status, _) = await Run(
            await new PersonController(db).UpdateAsync(
                person.Id,
                Update(person, null) with
                {
                    FirstName = " ",
                }
            )
        );

        Assert.Equal(400, status);
    }

    [Fact]
    public async Task Updating_an_unknown_Person_is_404()
    {
        await using var db = Database();
        var ghost = new PersonEntity { FirstName = "G", LastName = "H" };

        var (status, _) = await Run(
            await new PersonController(db).UpdateAsync(-1, Update(ghost, null))
        );

        Assert.Equal(404, status);
    }

    [Fact]
    public void A_request_body_without_payerId_is_rejected_instead_of_clearing_the_Zahler()
    {
        var json = """{"firstName":"A","lastName":"B","membership":0,"isMainPayer":false}""";

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<PersonUpdate>(json, JsonSerializerOptions.Web)
        );
    }
}
