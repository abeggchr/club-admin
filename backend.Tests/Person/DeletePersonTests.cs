using backend.Person;
using backend.Tests.Invoicing;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Person;

/// <summary>Controller-level tests for deleting a Person (Payer, ticket #66).</summary>
public class DeletePersonTests(LocalPostgresFixture fixture) : PersonControllerTestBase(fixture)
{
    [Fact]
    public async Task A_Person_who_is_a_Zahler_cannot_be_deleted()
    {
        await using var db = Database();
        var payer = Add(db, "Payer");
        Add(db, "Child", payer.Id);

        var (status, body) = await Run(await new PersonController(db, TimeProvider.System).DeleteAsync(payer.Id));

        Assert.Equal(400, status);
        Assert.Contains("error", body);
        Assert.True(Database().Persons.Any(p => p.Id == payer.Id));
    }

    [Fact]
    public async Task A_Person_with_a_Zahler_can_be_deleted()
    {
        await using var db = Database();
        var payer = Add(db, "Payer");
        var child = Add(db, "Child", payer.Id);

        var (status, _) = await Run(await new PersonController(db, TimeProvider.System).DeleteAsync(child.Id));

        Assert.Equal(204, status);
        Assert.False(Database().Persons.Any(p => p.Id == child.Id));
    }

    [Fact]
    public async Task Deleting_an_unknown_Person_is_404()
    {
        await using var db = Database();

        var (status, _) = await Run(await new PersonController(db, TimeProvider.System).DeleteAsync(-1));

        Assert.Equal(404, status);
    }

    [Fact]
    public async Task The_database_refuses_to_delete_a_Zahler_even_when_the_rule_is_bypassed()
    {
        await using var db = Database();
        var payer = Add(db, "Payer");
        Add(db, "Child", payer.Id);

        // A fresh context does not track the dependent, so only the database's FK can object.
        await using var fresh = Database();
        fresh.Persons.Remove(await fresh.Persons.SingleAsync(p => p.Id == payer.Id));

        await Assert.ThrowsAsync<DbUpdateException>(() => fresh.SaveChangesAsync());
    }
}
