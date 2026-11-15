using backend.Tests.Invoicing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests.Api;

/// <summary>
/// Boots the real app in memory against the database of <see cref="LocalPostgresFixture"/>.
/// Startup migrates that database and checks the Chart of Accounts, like in production. Object storage needs
/// no running server: the S3 client is only used by invoice endpoints, and the bucket is only
/// provisioned in Development.
/// </summary>
public sealed class AppFactory(LocalPostgresFixture postgres) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", postgres.ConnectionString);
        builder.UseSetting("ObjectStorage:Bucket", "test");
        builder.UseSetting("ObjectStorage:Endpoint", "http://localhost:9");
        builder.UseSetting("ObjectStorage:AccessKeyId", "test");
        builder.UseSetting("ObjectStorage:SecretAccessKey", "test");
        builder.UseSetting("Authentication:Microsoft:TenantId", "test-tenant");
        builder.UseSetting("Authentication:Microsoft:ClientId", "test-client");
        builder.UseSetting("Authentication:Microsoft:ClientSecret", "test-secret");

        // Entra is replaced by a test handler: a request is signed in when it carries the
        // TestAuthHandler.UserHeader header.
        builder.ConfigureTestServices(services =>
            services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { }
                )
        );
    }
}
