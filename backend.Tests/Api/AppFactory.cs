using backend.Accounting;
using backend.Invoicing;
using backend.Settings;
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
public class AppFactory(LocalPostgresFixture postgres) : WebApplicationFactory<Program>
{
    /// <summary>Receipt PDFs of this app instance; the S3 endpoint is unreachable, so tests use memory.</summary>
    public InMemoryReceiptStorage Receipts { get; } = new();

    /// <summary>Invoice PDFs of this app instance, for the same reason.</summary>
    public InMemoryInvoiceStorage InvoicePdfs { get; } = new();

    /// <summary>Test emails of this app instance; no SMTP server is contacted.</summary>
    public InMemoryEmailSender Emails { get; } = new();

    /// <summary>The stand-in for api.epost.ch of this app instance; the real API bills every letter and is never called.</summary>
    public InMemoryEPostHandler EPost { get; } = new();

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
        {
            services.AddSingleton<IReceiptStorage>(Receipts);
            services.AddSingleton<IInvoiceStorage>(InvoicePdfs);
            services.AddSingleton<IEmailSender>(Emails);
            services.AddHttpClient<EPostInvoiceSender>().ConfigurePrimaryHttpMessageHandler(() => EPost);
            services.AddHttpClient<EPostEmailInvoiceSender>().ConfigurePrimaryHttpMessageHandler(() => EPost);
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
                );
        });
    }
}
