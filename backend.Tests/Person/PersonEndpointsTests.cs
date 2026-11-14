using System.Text.Json;
using backend.Person;
using backend.Tests.Invoicing;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonEntity = backend.Person.Person;

namespace backend.Tests.Person;

/// <summary>Endpoint-level tests for changing and deleting a Person (Zahler, ticket #66); need the local PostgreSQL.</summary>
public class PersonEndpointsTests : IClassFixture<LocalPostgresFixture>
{
    private readonly LocalPostgresFixture _fixture;

    public PersonEndpointsTests(LocalPostgresFixture fixture) => _fixture = fixture;

    private AppDbContext Database()
    {
        if (_fixture.ConnectionString == "")
            throw new SkipException("No reachable PostgreSQL (see LocalPostgresFixture).");
        return new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_fixture.ConnectionString).Options
        );
    }

    private static PersonEntity Add(AppDbContext db, string name, int? payerId = null)
    {
        var person = new PersonEntity
        {
            FirstName = name,
            LastName = "Test",
            PayerId = payerId,
        };
        db.Persons.Add(person);
        db.SaveChanges();
        return person;
    }

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

    private static async Task<(int Status, string Body)> Run(IResult result)
    {
        var ctx = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
        };
        ctx.Response.Body = new MemoryStream();
        await result.ExecuteAsync(ctx);
        ctx.Response.Body.Position = 0;
        return (
            ctx.Response.StatusCode,
            await new StreamReader(ctx.Response.Body).ReadToEndAsync()
        );
    }

    private static int? StoredPayer(AppDbContext db, int id) =>
        db.Persons.AsNoTracking().Single(p => p.Id == id).PayerId;

    [Fact]
    public async Task Setting_a_Zahler_stores_it()
    {
        await using var db = Database();
        var payer = Add(db, "Payer");
        var child = Add(db, "Child");

        var (status, _) = await Run(
            await PersonEndpoints.UpdateAsync(child.Id, Update(child, payer.Id), db)
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
            await PersonEndpoints.UpdateAsync(child.Id, Update(child, null), db)
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
            await PersonEndpoints.UpdateAsync(person.Id, Update(person, person.Id), db)
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
            await PersonEndpoints.UpdateAsync(
                person.Id,
                Update(person, null) with
                {
                    FirstName = " ",
                },
                db
            )
        );

        Assert.Equal(400, status);
    }

    [Fact]
    public async Task Updating_an_unknown_Person_is_404()
    {
        await using var db = Database();
        var ghost = new PersonEntity { FirstName = "G", LastName = "H" };

        var (status, _) = await Run(await PersonEndpoints.UpdateAsync(-1, Update(ghost, null), db));

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

    [Fact]
    public async Task A_Person_who_is_a_Zahler_cannot_be_deleted()
    {
        await using var db = Database();
        var payer = Add(db, "Payer");
        Add(db, "Child", payer.Id);

        var (status, body) = await Run(await PersonEndpoints.DeleteAsync(payer.Id, db));

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

        var (status, _) = await Run(await PersonEndpoints.DeleteAsync(child.Id, db));

        Assert.Equal(204, status);
        Assert.False(Database().Persons.Any(p => p.Id == child.Id));
    }

    [Fact]
    public async Task Deleting_an_unknown_Person_is_404()
    {
        await using var db = Database();

        var (status, _) = await Run(await PersonEndpoints.DeleteAsync(-1, db));

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
