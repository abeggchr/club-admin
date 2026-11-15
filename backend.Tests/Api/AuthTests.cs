using System.Net;
using System.Net.Http.Json;
using backend.Tests.Invoicing;

namespace backend.Tests.Api;

public class AuthTests : IClassFixture<LocalPostgresFixture>
{
    private readonly LocalPostgresFixture _postgres;

    public AuthTests(LocalPostgresFixture postgres)
    {
        _postgres = postgres;
        _postgres.SkipUnlessAvailable();
    }

    [Theory]
    [InlineData("/api/person")]
    [InlineData("/api/accounts")]
    [InlineData("/api/settings")]
    public async Task Anonymous_api_request_gets_401(string path)
    {
        await using var factory = new AppFactory(_postgres);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_user_gets_200()
    {
        await using var factory = new AppFactory(_postgres);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "anna@example.ch");

        var response = await client.GetAsync("/api/person");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Me_returns_name_and_email_of_the_user()
    {
        await using var factory = new AppFactory(_postgres);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "anna@example.ch");

        var me = await client.GetFromJsonAsync<Dictionary<string, string>>("/api/auth/me");

        Assert.Equal("Test User", me!["name"]);
        Assert.Equal("anna@example.ch", me["email"]);
    }

    [Fact]
    public async Task Me_is_401_when_anonymous()
    {
        await using var factory = new AppFactory(_postgres);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
