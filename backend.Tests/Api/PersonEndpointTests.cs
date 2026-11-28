using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using backend.Tests.Invoicing;
using backend.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using PersonEntity = backend.Person.Person;

namespace backend.Tests.Api;

/// <summary>Changing a Person and listing Jubiläen over HTTP. The Payer and Main Payer rules themselves are tested at PayerRules.</summary>
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

    [Fact]
    public async Task Jubilees_cover_the_club_year_of_todays_Zurich_date_even_when_UTC_is_still_on_the_previous_day()
    {
        await using var db = NewDb();
        await db.Settings.ExecuteDeleteAsync();
        db.Settings.Add(new backend.Settings.Settings { StartDate = new DateOnly(2003, 4, 1) });
        var member = new PersonEntity { FirstName = "Jana", LastName = "Jubilar", JoinedOn = new DateOnly(2010, 5, 1) };
        db.Persons.Add(member);
        await db.SaveChangesAsync();
        // 31 March 22:30 UTC is already 1 April 00:30 in Zurich: the club year starting 2020 has begun.
        var clock = new TestClock(new DateTime(2020, 3, 31, 22, 30, 0, DateTimeKind.Utc));
        await using var factory = new AppFactory(_postgres, clock);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "anna@example.ch");

        var body = await client.GetFromJsonAsync<JsonElement>("/api/person/jubilees");

        Assert.Equal(2020, body.GetProperty("clubYearStartYear").GetInt32());
        var jubilee = body.GetProperty("jubilees")
            .EnumerateArray()
            .Single(j => j.GetProperty("personId").GetInt32() == member.Id);
        Assert.Equal(10, jubilee.GetProperty("years").GetInt32());
    }
}
