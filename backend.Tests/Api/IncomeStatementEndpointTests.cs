using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using backend.Accounting;
using backend.IncomeStatement;
using backend.Tests.Invoicing;
using backend.Tests.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests.Api;

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

            var factory = new AppFactory(Postgres, TestClock.On(TodayOverride ?? Today));
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
            // Out-of-window Entries on 4000 (Anlässe): before the Start Date and after today.
            yield return (new DateOnly(2025, 12, 31), "Vor Startdatum", 200m, 4000, "Vor Startdatum");
            yield return (new DateOnly(2026, 7, 1), "In der Zukunft", 200m, 4000, "In der Zukunft");
        }
    }

    private async Task<JsonElement> GetReport(Fixture fixture)
    {
        var response = await fixture.Client.GetAsync("/api/income-statement?view=all");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<JsonElement> GetSimplifiedReport(Fixture fixture)
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
        Assert.Equal(1205m, report.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(892m, report.GetProperty("totalExpense").GetDecimal());
        Assert.Equal(313m, report.GetProperty("overallResult").GetDecimal());

        var sections = report.GetProperty("sections").EnumerateArray().ToArray();
        Assert.Equal(2, sections.Length);
        Assert.Equal("Ertrag", sections[0].GetProperty("label").GetString());
        Assert.Equal(1205m, sections[0].GetProperty("total").GetDecimal());
        Assert.Equal("Aufwand", sections[1].GetProperty("label").GetString());
        Assert.Equal(892m, sections[1].GetProperty("total").GetDecimal());

        var revenueGroups = sections[0].GetProperty("groups").EnumerateArray().ToArray();
        Assert.Equal(3, revenueGroups.Length);
        Assert.Equal(400m, revenueGroups[0].GetProperty("total").GetDecimal());
        Assert.Equal(500m, revenueGroups[1].GetProperty("total").GetDecimal());
        Assert.Equal(305m, revenueGroups[2].GetProperty("total").GetDecimal());

        var otherRevenue = revenueGroups[2].GetProperty("lines").EnumerateArray().ToArray();
        Assert.Equal(3, otherRevenue.Length);
        Assert.Equal(200m, otherRevenue[0].GetProperty("amount").GetDecimal());
        Assert.Equal(25m, otherRevenue[1].GetProperty("amount").GetDecimal());
        // 3610-3620: 3620 is not in the chart, the line shows the entry on 3610.
        Assert.Equal(80m, otherRevenue[2].GetProperty("amount").GetDecimal());
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
        Assert.Equal(500m, revenueGroups[1].GetProperty("total").GetDecimal());
        Assert.Equal(25m, revenueGroups[2].GetProperty("lines")[1].GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task Entries_before_the_Start_Date_and_after_today_are_left_out()
    {
        // The out-of-window Entries (2025-12-31: +1000 and 2026-07-01: -1000, both on 6000) are seeded
        // with the fixture; 6000 is in the real chart, so the report would carry them if the window were off.
        await using var fixture = new Fixture(_postgres);
        await fixture.InitializeAsync();

        var report = await GetReport(fixture);

        // 4000 (Anlässe): only the in-window Entry of 300 remains.
        var anlaesse = report.GetProperty("sections")[1].GetProperty("groups")[0].GetProperty("lines").EnumerateArray().ToArray();
        Assert.Equal(300m, anlaesse[0].GetProperty("amount").GetDecimal());
        Assert.Equal(892m, report.GetProperty("totalExpense").GetDecimal());
        Assert.Equal(313m, report.GetProperty("overallResult").GetDecimal());
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
            var response = await client.GetAsync("/api/income-statement?view=all");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = (await response.Content.ReadFromJsonAsync<JsonElement>())!;
            Assert.Contains("Startdatum", body.GetProperty("error").GetString());
        }
    }

    [Fact]
    public async Task The_default_view_is_simplified()
    {
        await using var fixture = new Fixture(_postgres);
        await fixture.InitializeAsync();

        var report = await GetSimplifiedReport(fixture);

        // Without a view param, lines without Entries in the period are hidden: the seeded data
        // touches 12 of the 17 configured lines, so the simplified report has fewer lines.
        var all = await GetReport(fixture);
        var simplifiedLineCount = report.GetProperty("sections").EnumerateArray().Sum(
            s => s.GetProperty("groups").EnumerateArray().Sum(g => g.GetProperty("lines").EnumerateArray().Count())
        );
        var allLineCount = all.GetProperty("sections").EnumerateArray().Sum(
            s => s.GetProperty("groups").EnumerateArray().Sum(g => g.GetProperty("lines").EnumerateArray().Count())
        );
        Assert.True(simplifiedLineCount < allLineCount);
        // Totals and the overall result are identical in both views.
        Assert.Equal(all.GetProperty("totalRevenue").GetDecimal(), report.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(all.GetProperty("totalExpense").GetDecimal(), report.GetProperty("totalExpense").GetDecimal());
        Assert.Equal(all.GetProperty("overallResult").GetDecimal(), report.GetProperty("overallResult").GetDecimal());
    }

    [Fact]
    public async Task The_simplified_view_hides_Accounts_without_Entries_and_empty_groups()
    {
        await using var fixture = new Fixture(_postgres);
        await fixture.InitializeAsync();

        var report = await GetSimplifiedReport(fixture);

        var revenueGroups = report.GetProperty("sections")[0].GetProperty("groups").EnumerateArray().ToArray();
        // 3400, 3500, 3510, 3600, 3610 and 6950 have Entries, so all three revenue groups stay.
        Assert.Equal(3, revenueGroups.Length);
        Assert.Equal(3, revenueGroups[2].GetProperty("lines").EnumerateArray().Count());

        var expenseGroups = report.GetProperty("sections")[1].GetProperty("groups").EnumerateArray().ToArray();
        // Only 4000 (Anlässe), 4010 (Clubnews) and 6940 (Bankgebühren) have Entries in the Clubbetrieb
        // group; Geschenke (4020) and Diverses (4099) are hidden.
        var clubbetrieb = expenseGroups[0].GetProperty("lines").EnumerateArray().ToArray();
        Assert.Equal(3, clubbetrieb.Length);
        Assert.Equal("Anlässe", clubbetrieb[0].GetProperty("label").GetString());
        Assert.Equal("Clubnews", clubbetrieb[1].GetProperty("label").GetString());
        Assert.Equal("Bankgebühren", clubbetrieb[2].GetProperty("label").GetString());
        // The Skihaus group keeps only Miete (6010) and Nebenkosten (6020).
        var skihaus = expenseGroups[1].GetProperty("lines").EnumerateArray().ToArray();
        Assert.Equal(2, skihaus.Length);
        Assert.Equal("Miete", skihaus[0].GetProperty("label").GetString());
        Assert.Equal("Nebenkosten", skihaus[1].GetProperty("label").GetString());
    }

    [Fact]
    public async Task A_net_zero_Account_with_Entries_stays_in_the_simplified_view()
    {
        await using var db = NewDb();
        await db.Transactions.ExecuteDeleteAsync();
        await db.Settings.ExecuteDeleteAsync();

        var factory = new AppFactory(_postgres, TestClock.On(Today));
        await using (factory)
        {
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
            var transaction = new backend.Accounting.Transaction
            {
                Date = new DateOnly(2026, 2, 1),
                Amount = 0m,
                Text = "Gegeneinander",
            };
            transaction.Entries.Add(new backend.Accounting.Entry { Text = "Einnahme", AccountNumber = 4000, Amount = 100m });
            transaction.Entries.Add(new backend.Accounting.Entry { Text = "Ausgabe", AccountNumber = 4000, Amount = -100m });
            db.Transactions.Add(transaction);
            await db.SaveChangesAsync();

            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "anna@example.ch");
            var response = await client.GetAsync("/api/income-statement");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var report = (await response.Content.ReadFromJsonAsync<JsonElement>())!;
            // 4000 (Anlässe) nets to 0.00 but has two Entries, so the line stays.
            var anlaesse = report.GetProperty("sections")[1].GetProperty("groups")[0].GetProperty("lines").EnumerateArray().ToArray();
            Assert.Single(anlaesse);
            Assert.Equal("Anlässe", anlaesse[0].GetProperty("label").GetString());
            Assert.Equal(0m, anlaesse[0].GetProperty("amount").GetDecimal());
        }
    }

    [Fact]
    public async Task The_all_view_shows_every_configured_line()
    {
        await using var fixture = new Fixture(_postgres);
        await fixture.InitializeAsync();

        var report = await GetReport(fixture);

        // The checked-in layout has 18 configured lines; the "all" view keeps all of them.
        var lineCount = report.GetProperty("sections").EnumerateArray().Sum(
            s => s.GetProperty("groups").EnumerateArray().Sum(g => g.GetProperty("lines").EnumerateArray().Count())
        );
        Assert.Equal(18, lineCount);
    }

    [Fact]
    public async Task An_invalid_view_value_is_rejected()
    {
        await using var fixture = new Fixture(_postgres);
        await fixture.InitializeAsync();

        var response = await fixture.Client.GetAsync("/api/income-statement?view=none");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<JsonElement>())!;
        Assert.Contains("'none'", body.GetProperty("error").GetString());
    }

    /// <summary>
    /// An <see cref="AppFactory"/> whose chart and Income Statement layout are registered through
    /// <see cref="ConfigureWebHost"/> (before the app startup runs), so endpoint tests can exercise
    /// a layout that does not cover every chart Account (ticket #115).
    /// </summary>
    public sealed class UnmappedChartIncomeStatementAppFactory(
        LocalPostgresFixture postgres,
        DateOnly today,
        ChartOfAccounts chart,
        IncomeStatementConfig config
    ) : AppFactory(postgres, TestClock.On(today))
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.Remove(services.First(d => d.ServiceType == typeof(ChartOfAccounts)));
                services.AddSingleton(chart);
                services.Remove(services.First(d => d.ServiceType == typeof(IncomeStatementConfig)));
                services.AddSingleton(config);
            });
        }
    }

    [Fact]
    public async Task Unmapped_Accounts_with_Entries_show_under_Nicht_zugeordnet_and_reconcile()
    {
        await using var db = NewDb();
        await db.Transactions.ExecuteDeleteAsync();
        await db.Settings.ExecuteDeleteAsync();

        // The checked-in YAML covers every account of the checked-in chart (3620 is in a range
        // but not in the chart), so the chart is extended with two unmapped Accounts (3700, 4005)
        // and the YAML is re-parsed without them. Expected: revenue 500 (3400), expense 300 (4000)
        // + 75 (4005); 3700 nets to 0.00 and must not change any total or show a group.
        var chart = ChartOfAccounts.Parse(
            $$"""
            {
              "income": [
                { "number": 3400, "name": "Jahresbeiträge" },
                { "number": 3700, "name": "Unzugewiesene Einnahmen" }
              ],
              "expense": [
                { "number": 4000, "name": "Anlässe" },
                { "number": 4005, "name": "Unzugewiesene Ausgaben" }
              ]
            }
            """
        );
        var config = IncomeStatementConfig.Parse(
            """
            Erfolgsrechnung:
              Ertrag:
                I. Beiträge:
                  Jahresbeiträge: 3400
              Aufwand:
                I. Anlässe:
                  Anlässe: 4000
            """
        );
        Assert.Null(IncomeStatementConfig.Validate(config, chart));

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
        var incomeTransaction = new backend.Accounting.Transaction
        {
            Date = new DateOnly(2026, 2, 1),
            Amount = 590m,
            Text = "Einnahmen",
        };
        incomeTransaction.Entries.Add(new backend.Accounting.Entry { Text = "Jahresbeiträge", AccountNumber = 3400, Amount = 500m });
        incomeTransaction.Entries.Add(new backend.Accounting.Entry { Text = "Unzugewiesene Einnahme", AccountNumber = 3700, Amount = 90m });
        var expenseTransaction = new backend.Accounting.Transaction
        {
            Date = new DateOnly(2026, 3, 1),
            Amount = -375m,
            Text = "Ausgaben",
        };
        expenseTransaction.Entries.Add(new backend.Accounting.Entry { Text = "Anlässe", AccountNumber = 4000, Amount = -300m });
        expenseTransaction.Entries.Add(new backend.Accounting.Entry { Text = "Unzugewiesene Ausgabe", AccountNumber = 4005, Amount = -75m });
        // 3700 nets to 0.00 (90 in, 90 out): its "Nicht zugeordnet" line must not show.
        var netZeroTransaction = new backend.Accounting.Transaction
        {
            Date = new DateOnly(2026, 3, 15),
            Amount = 0m,
            Text = "Gegeneinander",
        };
        netZeroTransaction.Entries.Add(new backend.Accounting.Entry { Text = "Rückerstattung", AccountNumber = 3700, Amount = -90m });
        db.Transactions.Add(incomeTransaction);
        db.Transactions.Add(expenseTransaction);
        db.Transactions.Add(netZeroTransaction);
        await db.SaveChangesAsync();

        var factory = new UnmappedChartIncomeStatementAppFactory(_postgres, Today, chart, config);
        await using (factory)
        {
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "anna@example.ch");

            var response = await client.GetAsync("/api/income-statement?view=all");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var report = (await response.Content.ReadFromJsonAsync<JsonElement>())!;

            Assert.Equal(500m, report.GetProperty("totalRevenue").GetDecimal());
            Assert.Equal(375m, report.GetProperty("totalExpense").GetDecimal());
            Assert.Equal(125m, report.GetProperty("overallResult").GetDecimal());

            var sections = report.GetProperty("sections").EnumerateArray().ToArray();
            var revenueGroups = sections[0].GetProperty("groups").EnumerateArray().ToArray();
            Assert.Single(revenueGroups);
            Assert.Equal("I. Beiträge", revenueGroups[0].GetProperty("label").GetString());
            Assert.Equal(500m, sections[0].GetProperty("total").GetDecimal());

            var expenseGroups = sections[1].GetProperty("groups").EnumerateArray().ToArray();
            Assert.Equal(2, expenseGroups.Length);
            Assert.Equal("Nicht zugeordnet", expenseGroups[1].GetProperty("label").GetString());
            Assert.Equal(75m, expenseGroups[1].GetProperty("total").GetDecimal());
            Assert.Equal(375m, sections[1].GetProperty("total").GetDecimal());

            // The simplified view shows the same expense group with the same totals.
            var simplifiedResponse = await client.GetAsync("/api/income-statement");
            Assert.Equal(HttpStatusCode.OK, simplifiedResponse.StatusCode);
            var simplified = (await simplifiedResponse.Content.ReadFromJsonAsync<JsonElement>())!;
            var simplifiedExpenseGroups = simplified.GetProperty("sections")[1].GetProperty("groups").EnumerateArray().ToArray();
            Assert.Equal("Nicht zugeordnet", simplifiedExpenseGroups[1].GetProperty("label").GetString());
            Assert.Equal(75m, simplifiedExpenseGroups[1].GetProperty("total").GetDecimal());
            Assert.Equal(125m, simplified.GetProperty("overallResult").GetDecimal());
        }
    }

    [Fact]
    public async Task The_Nicht_zugeordnet_group_is_absent_when_all_Accounts_are_mapped()
    {
        await using var fixture = new Fixture(_postgres);
        await fixture.InitializeAsync();

        var report = await GetReport(fixture);

        // Every Account with an Entry in the fixture is mapped in the checked-in YAML, so neither
        // section carries a "Nicht zugeordnet" group.
        var labels = report.GetProperty("sections")
            .EnumerateArray()
            .SelectMany(s => s.GetProperty("groups").EnumerateArray())
            .Select(g => g.GetProperty("label").GetString());
        Assert.DoesNotContain(labels, label => label == "Nicht zugeordnet");
    }
}
