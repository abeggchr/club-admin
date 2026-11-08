using backend.Accounting;
using backend.Person;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
);

// A malformed Kontoplan file fails startup (see ADR 0001).
builder.Services.AddSingleton(
    ChartOfAccounts.Load(Path.Combine(AppContext.BaseDirectory, "Accounting", "chart-of-accounts.json"))
);

var app = builder.Build();

// Apply pending EF migrations on startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    // Refuse to start when the Kontoplan file disagrees with the stored Buchungen (ADR 0001, ADR 0002).
    var chart = scope.ServiceProvider.GetRequiredService<ChartOfAccounts>();
    var recorded = SyncAccountUsages(db, chart);
    if (ChartConsistency.Check(chart, recorded) is { } inconsistency)
    {
        throw new ChartOfAccountsException($"Kontoplan passt nicht zu den Buchungen. {inconsistency}");
    }
    db.SaveChanges();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
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

var api = app.MapGroup("/api");
api.MapGet("/health", () => Results.Ok());

api.MapGet("/accounts", (ChartOfAccounts chart) => chart.Accounts);

api.MapGet(
    "/persons",
    async (AppDbContext db) => await db.Persons.OrderBy(p => p.Id).ToListAsync()
);

api.MapPut(
    "/persons/{id:int}",
    async (int id, PersonUpdate update, AppDbContext db) =>
    {
        if (string.IsNullOrWhiteSpace(update.FirstName) || string.IsNullOrWhiteSpace(update.LastName))
        {
            return Results.BadRequest(new { error = "Vorname und Nachname sind erforderlich." });
        }

        var person = await db.Persons.FindAsync(id);
        if (person is null)
        {
            return Results.NotFound();
        }

        person.FirstName = update.FirstName.Trim();
        person.LastName = update.LastName.Trim();
        person.DateOfBirth = update.DateOfBirth;
        person.JoinedOn = update.JoinedOn;
        person.LeftOn = update.LeftOn;
        person.Email = update.Email;
        person.MobilePhone = update.MobilePhone;
        person.LandlinePhone = update.LandlinePhone;
        person.Address = update.Address;
        person.PostalCode = update.PostalCode;
        person.City = update.City;
        person.Country = update.Country;
        person.Notes = update.Notes;

        await db.SaveChangesAsync();
        return Results.Ok(person);
    }
);

api.MapPost(
        "/persons/import",
        async (IFormFile file, AppDbContext db) =>
        {
            List<Person> persons;
            try
            {
                using var reader = new StreamReader(file.OpenReadStream());
                persons = PersonCsvImporter.Parse(reader);
            }
            catch (FormatException e)
            {
                return Results.BadRequest(new { error = e.Message });
            }

            db.Persons.AddRange(persons);
            await db.SaveChangesAsync();
            return Results.Ok(new { imported = persons.Count });
        }
    )
    .DisableAntiforgery();

api.MapGet(
    "/opening-balance",
    async (AppDbContext db) =>
        await db.Settings.FindAsync(Settings.SingletonId) is { } ob
            ? Results.Ok(ob)
            : Results.NotFound()
);

api.MapPut(
    "/opening-balance",
    async (OpeningBalanceUpdate update, AppDbContext db) =>
    {
        if (update.OpeningBalance != decimal.Round(update.OpeningBalance, 2))
        {
            return Results.BadRequest(new { error = "Der Betrag darf höchstens zwei Dezimalstellen haben." });
        }

        var earliest = await db.JournalEntries.AnyAsync()
            ? await db.JournalEntries.MinAsync(e => e.Date)
            : (DateOnly?)null;
        if (EntryRules.ValidateOpeningStartDate(update.StartDate, earliest) is { } dateError)
        {
            return Results.BadRequest(new { error = dateError });
        }

        var ob = await db.Settings.FindAsync(Settings.SingletonId);
        if (ob is null)
        {
            ob = new Settings();
            db.Settings.Add(ob);
        }

        ob.OpeningBalance = update.OpeningBalance;
        ob.StartDate = update.StartDate;

        await db.SaveChangesAsync();
        return Results.Ok(ob);
    }
);

api.MapGet(
    "/entries",
    async (
        DateOnly? from,
        DateOnly? to,
        [FromQuery(Name = "account")] int? accountNumber,
        [FromQuery(Name = "q")] string? search,
        AppDbContext db
    ) =>
    {
        var opening = await db.Settings.FindAsync(Settings.SingletonId);
        if (opening is null)
        {
            return [];
        }

        var entries = await db.JournalEntries.ToListAsync();
        // Balances are computed over the whole journal; filters only hide rows.
        return JournalFilter
            .Apply(AccountingCalculator.RunningBalances(opening, entries), from, to, accountNumber, search)
            .Select(r => new JournalEntryDto(r.Entry.Id, r.Entry.Date, r.Entry.Text, r.Entry.AccountNumber, r.Entry.Amount, r.Balance))
            .ToList();
    }
);

api.MapGet(
    "/expected-balance",
    async (DateOnly date, AppDbContext db) =>
    {
        var opening = await db.Settings.FindAsync(Settings.SingletonId);
        if (opening is null)
        {
            return Results.NotFound();
        }

        var entries = await db.JournalEntries.Where(e => e.Date <= date).ToListAsync();
        return Results.Ok(new { date, balance = AccountingCalculator.ExpectedBalance(opening, entries, date) });
    }
);

api.MapPost(
    "/entries",
    async (EntryInput input, AppDbContext db, ChartOfAccounts chart) =>
    {
        var opening = await db.Settings.FindAsync(Settings.SingletonId);
        if (EntryRules.Validate(opening, chart, input.Date, input.Text, input.AccountNumber, input.Amount) is { } error)
        {
            return Results.BadRequest(new { error });
        }

        var entry = new Entry
        {
            Date = input.Date,
            Text = input.Text.Trim(),
            AccountNumber = input.AccountNumber,
            Amount = input.Amount,
        };
        db.JournalEntries.Add(entry);
        await SaveEntryChangeAsync(db, chart);
        return Results.Created($"/api/entries/{entry.Id}", entry);
    }
);

api.MapPut(
    "/entries/{id:int}",
    async (int id, EntryInput input, AppDbContext db, ChartOfAccounts chart) =>
    {
        var entry = await db.JournalEntries.FindAsync(id);
        if (entry is null)
        {
            return Results.NotFound();
        }
        var opening = await db.Settings.FindAsync(Settings.SingletonId);
        if (
            EntryRules.Validate(
                opening,
                chart,
                input.Date,
                input.Text,
                input.AccountNumber,
                input.Amount,
                entry.AccountNumber
            ) is { } error
        )
        {
            return Results.BadRequest(new { error });
        }

        entry.Date = input.Date;
        entry.Text = input.Text.Trim();
        entry.AccountNumber = input.AccountNumber;
        entry.Amount = input.Amount;
        await SaveEntryChangeAsync(db, chart);
        return Results.Ok(entry);
    }
);

api.MapDelete(
    "/entries/{id:int}",
    async (int id, AppDbContext db, ChartOfAccounts chart) =>
    {
        var entry = await db.JournalEntries.FindAsync(id);
        if (entry is null)
        {
            return Results.NotFound();
        }
        db.JournalEntries.Remove(entry);
        await SaveEntryChangeAsync(db, chart);
        return Results.NoContent();
    }
);

// SPA fallback for client-side routes (not for unknown /api paths or files).
app.MapFallbackToFile("{*path:nonfile:regex(^(?!api/).*$)}", "index.html");

app.Run();

// Keeps the AccountUsage records in step with the Buchungen (ADR 0002): a Konto's Kategorie is recorded
// when a Buchung first uses it and the record is dropped once no Buchung uses the Konto any more.
// Returns the recorded Kategorie per used Konto; a used Konto missing from the Kontoplan gets a
// placeholder (not persisted) so the consistency check reports it.
static Dictionary<int, Category> SyncAccountUsages(AppDbContext db, ChartOfAccounts chart)
{
    var usages = db.AccountUsages.ToList();
    var used = db.JournalEntries.Select(e => e.AccountNumber).Distinct().ToList();
    var recordedNumbers = usages.Select(u => u.AccountNumber).ToList();

    var unused = AccountUsageRules.Unused(recordedNumbers, used);
    db.AccountUsages.RemoveRange(usages.Where(u => unused.Contains(u.AccountNumber)));
    usages.RemoveAll(u => unused.Contains(u.AccountNumber));

    var result = usages.ToDictionary(u => u.AccountNumber, u => u.Category);
    // Konten used before their Kategorie was recorded adopt the file's current Kategorie.
    foreach (var number in AccountUsageRules.Unrecorded(recordedNumbers, used))
    {
        if (chart.Find(number) is { } account)
        {
            db.AccountUsages.Add(new AccountUsage { AccountNumber = number, Category = account.Category });
            result[number] = account.Category;
        }
        else
        {
            result[number] = Category.Income;
        }
    }
    return result;
}

// Saves a pending Buchung change and the matching AccountUsage changes atomically.
static async Task SaveEntryChangeAsync(AppDbContext db, ChartOfAccounts chart)
{
    await using var tx = await db.Database.BeginTransactionAsync();
    await db.SaveChangesAsync();
    SyncAccountUsages(db, chart);
    await db.SaveChangesAsync();
    await tx.CommitAsync();
}

record EntryInput(DateOnly Date, string Text, int AccountNumber, decimal Amount);

record JournalEntryDto(int Id, DateOnly Date, string Text, int AccountNumber, decimal Amount, decimal Balance);

record OpeningBalanceUpdate(decimal OpeningBalance, DateOnly StartDate);

record PersonUpdate(
    string FirstName,
    string LastName,
    DateOnly? DateOfBirth,
    DateOnly? JoinedOn,
    DateOnly? LeftOn,
    string? Email,
    string? MobilePhone,
    string? LandlinePhone,
    string? Address,
    string? PostalCode,
    string? City,
    string? Country,
    string? Notes
);
