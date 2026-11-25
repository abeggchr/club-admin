using backend.Settings;
using backend.Accounting;
using backend.Auth;
using backend.Invoicing;
using backend.Person;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
// Request bodies are validated by the rules classes, not by implicit [Required] on non-nullable types.
builder.Services.AddControllers(options =>
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true
);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
);

builder.Services.AddEntraLogin(builder.Configuration);
builder.Services.AddObjectStorage(builder.Configuration);
builder.Services.AddScoped<InvoiceCreator>();
builder.Services.AddScoped<BankImporter>();
builder.Services.AddSingleton<IEmailSender, MailKitEmailSender>();
builder.Services.AddSingleton<IInvoiceSender, SmtpInvoiceSender>();
builder.Services.AddScoped<InvoiceDispatcher>();
InvoiceDocument.UseCommunityLicense();

// A malformed Chart of Accounts file fails startup (see ADR 0001).
builder.Services.AddSingleton(
    ChartOfAccounts.Load(
        Path.Combine(AppContext.BaseDirectory, "Accounting", "chart-of-accounts.json")
    )
);

var app = builder.Build();

// Apply pending EF migrations on startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    // Refuse to start when the Chart of Accounts file disagrees with the stored Entries (ADR 0001, ADR 0002).
    var chart = scope.ServiceProvider.GetRequiredService<ChartOfAccounts>();
    var recorded = AccountUsageSync.Sync(db, chart);
    if (ChartConsistency.Check(chart, recorded) is { } inconsistency)
    {
        throw new ChartOfAccountsException(
            $"Kontoplan passt nicht zu den Buchungen. {inconsistency}"
        );
    }
    db.SaveChanges();
}

// Configure the HTTP request pipeline.
// Behind Railway's TLS proxy this makes redirect URIs and cookies https.
app.UseForwardedHeaders();
if (app.Environment.IsDevelopment())
{
    // Local MinIO starts empty; production buckets are provisioned by Railway.
    await app.Services.EnsureBucketAsync();
    app.MapOpenApi();
}

// Railway terminates TLS in front of the container, so only redirect locally.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Serve the built frontend (copied to wwwroot in the Docker image).
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api").RequireAuthorization();
api.MapAuthEndpoints();
api.MapGet("/health", () => Results.Ok()).AllowAnonymous();

app.MapControllers().RequireAuthorization();

// SPA fallback for client-side routes (not for unknown /api paths or files).
app.MapFallbackToFile("{*path:nonfile:regex(^(?!api/).*$)}", "index.html");

app.Run();

// Makes the entry point visible to WebApplicationFactory in the tests.
public partial class Program;
