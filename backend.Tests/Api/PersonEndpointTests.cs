using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using backend.Tests.Invoicing;
using Microsoft.EntityFrameworkCore;
using PersonEntity = backend.Person.Person;

namespace backend.Tests.Api;

/// <summary>Changing a Person over HTTP. The Payer and Main Payer rules themselves are tested at PayerRules.</summary>
public class PersonEndpointTests : IClassFixture<LocalPostgresFixture>
{
    private readonly LocalPostgresFixture _postgres;

    public PersonEndpointTests(LocalPostgresFixture postgres)
    {
        _postgres = postgres;
        _postgres.SkipUnlessAvailable();
    }

    private AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_postgres.ConnectionString).Options);

    [Fact]
    public async Task Saving_a_Person_with_a_Zahler_as_Hauptzahler_answers_400_and_stores_nothing()
    {
        await using var db = NewDb();
        var payer = new PersonEntity { FirstName = "Paula", LastName = "Zahler" };
        var child = new PersonEntity { FirstName = "Kim", LastName = "Zahler" };
        db.Persons.AddRange(payer, child);
        await db.SaveChangesAsync();
        await using var factory = new AppFactory(_postgres);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "anna@example.ch");

        var response = await client.PutAsJsonAsync(
            $"/api/person/{child.Id}",
            new
            {
                firstName = "Kim",
                lastName = "Zahler",
                membership = "junior",
                isMainPayer = true,
                payerId = payer.Id,
                receivesClubNews = false,
            }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            "Eine Person mit Zahler kann nicht Hauptzahler sein.",
            body.GetProperty("error").GetString()
        );
        await using var check = NewDb();
        var stored = await check.Persons.AsNoTracking().SingleAsync(p => p.Id == child.Id);
        Assert.Null(stored.PayerId);
        Assert.False(stored.IsMainPayer);
    }
}
