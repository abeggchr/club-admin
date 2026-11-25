using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using backend.IncomeStatement;
using backend.Tests.Invoicing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests.Api;

/// <summary>
/// An <see cref="AppFactory"/> with a fixed clock, so the period end of the Income Statement is
/// deterministic in endpoint tests.
/// </summary>
public sealed class IncomeStatementAppFactory(LocalPostgresFixture postgres, DateOnly today) : AppFactory(postgres)
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.AddSingleton<backend.IncomeStatement.ISystemClock>(new backend.IncomeStatement.FixedClock(today)));
    }
}

/// <summary>
/// GET /api/income-statement: the paper report as a fixture, the date window (both boundaries
/// inclusive), and a missing Start Date. Figures are internally consistent per the ACs; exact
/// paper values are not provided.
/// </summary>
public class IncomeStatementEndpointTests : IClassFixture<LocalPostgresFixture>
{
    private static readonly DateOnly Start = new(2026, 1, 1);
    private static readonly DateOnly Today = new(2026, 6, 30);

    private readonly LocalPostgresFixture _postgres;

    public IncomeStatementEndpointTests(LocalPostgresFixture postgres)
    {
        _postgres = postgres;
        _postgres.SkipUnlessAvailable();
    }

    private AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_postgres.ConnectionString).Options);

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture(LocalPostgresFixture postgres, DateOnly? today = null)
        {
            Postgres = postgres;
            TodayOverride = today;
        }

        public LocalPostgresFixture Postgres { get; }
        public DateOnly? TodayOverride { get; }
        public AppFactory Factory { get; private set; } = null!;
        public HttpClient Client { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            // The database is empty when the app starts, so the chart consistency check passes;
            // the Entries are seeded afterwards, after the host is up.
            await using var db = new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Postgres.ConnectionString).Options
            );
            await db.Transactions.ExecuteDeleteAsync();
            await db.Settings.ExecuteDeleteAsync();

            var factory = new IncomeStatementAppFactory(Postgres, TodayOverride ?? Today);
            Factory = factory;
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "anna@example.ch");

            db.Settings.Add(
                new backend.Settings.Settings
                {
                    StartDate = Start,
                    Iban = "CH9300762011623852957",
                    Name = "SC Wettingen",
                    Address = "Sportweg 12",
                    PostalCode = "5430",
                    City = "Wettingen",
                }
            );
            foreach (var (date, text, amount, accountNumber, entryText) in SeedData())
            {
                var transaction = new backend.Accounting.Transaction { Date = date, Amount = amount, Text = text };
                transaction.Entries.Add(new backend.Accounting.Entry { Text = entryText, AccountNumber = accountNumber, Amount = amount });
                db.Transactions.Add(transaction);
            }
            await db.SaveChangesAsync();

            Client = client;
        }

        public ValueTask DisposeAsync()
        {
            var factory = Factory;
            Factory = null!;
            return factory is null ? ValueTask.CompletedTask : factory.DisposeAsync();
        }

        private static IEnumerable<(DateOnly, string, decimal, int, string)> SeedData()
        {
            // Paper-report fixture, internally consistent; the period is Start..Today (both inclusive).
            yield return (Start, "Jahresbeiträge", 500m, 3400, "Jahresbeiträge");
            yield return (new DateOnly(2026, 2, 1), "Übernachtungen", 250m, 3500, "Übernachtungen");
            yield return (new DateOnly(2026, 2, 15), "Getränke", 150m, 3510, "Getränkeeinnahmen");
            yield return (new DateOnly(2026, 3, 1), "Spende", 200m, 3600, "Spenden");
            yield return (new DateOnly(2026, 3, 15), "Einnahmen", 80m, 3610, "Diverse Einnahmen");
            yield return (Today, "Zinsen", 25m, 6950, "Zinsertrag");
            yield return (new DateOnly(2026, 1, 31), "Anlässe", -300m, 4000, "Anlässe");
            yield return (new DateOnly(2026, 2, 28), "Clubnews", -120m, 4010, "Clubnews");
            yield return (new DateOnly(2026, 3, 31), "Miete", -500m, 6010, "Miete");
            yield return (new DateOnly(2026, 4, 10), "Miete", 120m, 6010, "Miete");
            yield return (new DateOnly(2026, 4, 15), "Nebenkosten", -80m, 6020, "Nebenkosten");
            yield return (new DateOnly(2026, 5, 1), "Bankgebühren", -12m, 6940, "Bankgebühren");
            // Out-of-window Entries on the Skihaus account 6000: before the Start Date and after today.
            yield return (new DateOnly(2025, 12, 31), "Vor Startdatum", 1000m, 6000, "Vor Startdatum");
            yield return (new DateOnly(2026, 7, 1), "In der Zukunft", -1000m, 6000, "In der Zukunft");
        }
    }

    private async Task<JsonElement> GetReport(Fixture fixture)
    {
        var response = await fixture.Client.GetAsync("/api/income-statement");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task The_report_matches_the_paper_fixture_with_correct_signs_and_totals()
    {
        await using var fixture = new Fixture(_postgres);
        await fixture.InitializeAsync();

        var report = await GetReport(fixture);

        Assert.Equal("2026-01-01", report.GetProperty("period").GetProperty("from").GetString());
        Assert.Equal("2026-06-30", report.GetProperty("period").GetProperty("to").GetString());
        Assert.Equal(905m, report.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(1012m, report.GetProperty("totalExpense").GetDecimal());
        Assert.Equal(-107m, report.GetProperty("overallResult").GetDecimal());

        var sections = report.GetProperty("sections").EnumerateArray().ToArray();
        Assert.Equal(2, sections.Length);
        Assert.Equal("Ertrag", sections[0].GetProperty("label").GetString());
        Assert.Equal(905m, sections[0].GetProperty("total").GetDecimal());
        Assert.Equal("Aufwand", sections[1].GetProperty("label").GetString());
        Assert.Equal(1012m, sections[1].GetProperty("total").GetDecimal());

        var revenueGroups = sections[0].GetProperty("groups").EnumerateArray().ToArray();
        Assert.Equal(3, revenueGroups.Length);
        Assert.Equal(500m, revenueGroups[0].GetProperty("total").GetDecimal());
        Assert.Equal(400m, revenueGroups[1].GetProperty("total").GetDecimal());
        Assert.Equal(455m, revenueGroups[2].GetProperty("total").GetDecimal());

        var otherRevenue = revenueGroups[2].GetProperty("lines").EnumerateArray().ToArray();
        Assert.Equal(3, otherRevenue.Length);
        Assert.Equal(200m, otherRevenue[0].GetProperty("amount").GetDecimal());
        // 3610-3620: 3620 is not in the chart, the line shows the entry on 3610.
        Assert.Equal(80m, otherRevenue[1].GetProperty("amount").GetDecimal());
        Assert.Equal(25m, otherRevenue[2].GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task A_credit_on_an_expense_account_reduces_the_expense()
    {
        await using var fixture = new Fixture(_postgres);
        await fixture.InitializeAsync();

        var report = await GetReport(fixture);

        // 6010 (Miete): debits of 500, credits of 120 (a refund) -> 380.
        var skihaus = report.GetProperty("sections")[1].GetProperty("groups")[1].GetProperty("lines").EnumerateArray().ToArray();
        Assert.Equal(380m, skihaus[1].GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task Both_boundaries_of_the_period_are_inclusive()
    {
        await using var fixture = new Fixture(_postgres);
        await fixture.InitializeAsync();
        // The Entries above already sit on Start (3400) and Today (6950): a report of 1'055.00 revenue
        // only works if both boundary dates are included.
        var report = await GetReport(fixture);

        var revenueGroups = report.GetProperty("sections")[0].GetProperty("groups").EnumerateArray().ToArray();
        Assert.Equal(500m, revenueGroups[0].GetProperty("total").GetDecimal());
        Assert.Equal(25m, revenueGroups[2].GetProperty("lines")[2].GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task Entries_before_the_Start_Date_and_after_today_are_left_out()
    {
        // The out-of-window Entries (2025-12-31: +1000 and 2026-07-01: -1000, both on 6000) are seeded
        // with the fixture; 6000 is in the real chart, so the report would carry them if the window were off.
        await using var fixture = new Fixture(_postgres);
        await fixture.InitializeAsync();

        var report = await GetReport(fixture);

        // 6000 (Getränke): only the in-window Entry of 80 remains.
        var skihaus = report.GetProperty("sections")[1].GetProperty("groups")[1].GetProperty("lines").EnumerateArray().ToArray();
        Assert.Equal(80m, skihaus[0].GetProperty("amount").GetDecimal());
        Assert.Equal(1012m, report.GetProperty("totalExpense").GetDecimal());
        Assert.Equal(-107m, report.GetProperty("overallResult").GetDecimal());
    }

    [Fact]
    public async Task A_missing_Start_Date_gives_a_German_error()
    {
        await using var db = NewDb();
        await db.Settings.ExecuteDeleteAsync();
        await db.Transactions.ExecuteDeleteAsync();

        var factory = new AppFactory(_postgres);
        await using (factory)
        {
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "anna@example.ch");
            var response = await client.GetAsync("/api/income-statement");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = (await response.Content.ReadFromJsonAsync<JsonElement>())!;
            Assert.Contains("Startdatum", body.GetProperty("error").GetString());
        }
    }
}
