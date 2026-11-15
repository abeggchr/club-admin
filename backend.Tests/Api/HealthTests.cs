using System.Net;
using backend.Tests.Invoicing;

namespace backend.Tests.Api;

public class HealthTests : IClassFixture<LocalPostgresFixture>
{
    private readonly LocalPostgresFixture _postgres;

    public HealthTests(LocalPostgresFixture postgres)
    {
        _postgres = postgres;
        if (!_postgres.Available)
            throw new SkipException("No local PostgreSQL available (README: localhost:5432, postgres/secret).");
    }

    [Fact]
    public async Task Booted_app_answers_health_with_200()
    {
        await using var factory = new AppFactory(_postgres);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
