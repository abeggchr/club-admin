using backend.Tests.Invoicing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonEntity = backend.Person.Person;

namespace backend.Tests.Person;

/// <summary>Shared setup for the PersonController tests; they need the local PostgreSQL.</summary>
public abstract class PersonControllerTestBase(LocalPostgresFixture fixture)
    : IClassFixture<LocalPostgresFixture>
{
    protected AppDbContext Database()
    {
        if (fixture.ConnectionString == "")
            throw new SkipException("No reachable PostgreSQL (see LocalPostgresFixture).");
        return new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(fixture.ConnectionString).Options
        );
    }

    protected static PersonEntity Add(AppDbContext db, string name, int? payerId = null)
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

    protected static async Task<(int Status, string Body)> Run(IActionResult result)
    {
        var ctx = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddLogging()
                .AddControllers()
                .Services.BuildServiceProvider(),
        };
        ctx.Response.Body = new MemoryStream();
        await result.ExecuteResultAsync(
            new ActionContext(ctx, new RouteData(), new ActionDescriptor())
        );
        ctx.Response.Body.Position = 0;
        return (
            ctx.Response.StatusCode,
            await new StreamReader(ctx.Response.Body).ReadToEndAsync()
        );
    }
}
