using backend.Tests.Invoicing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace backend.Tests.Api;

/// <summary>
/// Boots the real app in memory against the database of <see cref="LocalPostgresFixture"/>.
/// Startup migrates that database and checks the Kontoplan, like in production. Object storage needs
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
    }
}
