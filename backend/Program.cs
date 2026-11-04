using backend.Accounting;
using backend.Person;
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
    var recorded = db.AccountUsages.ToDictionary(u => u.AccountNumber, u => u.Category);
    // Konten used before their Kategorie was recorded adopt the file's current Kategorie.
    foreach (var number in db.Entries.Select(e => e.AccountNumber).Distinct().ToList())
    {
        if (!recorded.ContainsKey(number) && chart.Find(number) is { } account)
        {
            recorded[number] = account.Category;
            db.AccountUsages.Add(new AccountUsage { AccountNumber = number, Category = account.Category });
        }
        else if (!recorded.ContainsKey(number))
        {
            recorded[number] = Category.Income; // not in the file: reported as missing below
        }
    }
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
        await db.OpeningBalances.FindAsync(OpeningBalance.SingletonId) is { } ob
            ? Results.Ok(ob)
            : Results.NotFound()
);

api.MapPut(
    "/opening-balance",
    async (OpeningBalanceUpdate update, AppDbContext db) =>
    {
        if (update.Amount != decimal.Round(update.Amount, 2))
        {
            return Results.BadRequest(new { error = "Der Betrag darf höchstens zwei Dezimalstellen haben." });
        }

        var earliest = await db.Entries.AnyAsync()
            ? await db.Entries.MinAsync(e => e.Date)
            : (DateOnly?)null;
        if (EntryRules.ValidateOpeningStartDate(update.StartDate, earliest) is { } dateError)
        {
            return Results.BadRequest(new { error = dateError });
        }

        var ob = await db.OpeningBalances.FindAsync(OpeningBalance.SingletonId);
        if (ob is null)
        {
            ob = new OpeningBalance();
            db.OpeningBalances.Add(ob);
        }

        ob.Amount = update.Amount;
        ob.StartDate = update.StartDate;

        await db.SaveChangesAsync();
        return Results.Ok(ob);
    }
);

api.MapGet(
    "/entries",
    async (AppDbContext db) =>
    {
        var opening = await db.OpeningBalances.FindAsync(OpeningBalance.SingletonId);
        if (opening is null)
        {
            return [];
        }

        var entries = await db.Entries.ToListAsync();
        return AccountingCalculator
            .RunningBalances(opening, entries)
            .Select(r => new JournalEntryDto(r.Entry.Id, r.Entry.Date, r.Entry.Text, r.Entry.AccountNumber, r.Entry.Amount, r.Balance))
            .ToList();
    }
);

api.MapGet(
    "/expected-balance",
    async (DateOnly date, AppDbContext db) =>
    {
        var opening = await db.OpeningBalances.FindAsync(OpeningBalance.SingletonId);
        if (opening is null)
        {
            return Results.NotFound();
        }

        var entries = await db.Entries.Where(e => e.Date <= date).ToListAsync();
        return Results.Ok(new { date, balance = AccountingCalculator.ExpectedBalance(opening, entries, date) });
    }
);

api.MapPost(
    "/entries",
    async (EntryInput input, AppDbContext db, ChartOfAccounts chart) =>
    {
        var opening = await db.OpeningBalances.FindAsync(OpeningBalance.SingletonId);
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
        db.Entries.Add(entry);
        // Remember the Konto's Kategorie at first use (ADR 0002).
        if (!await db.AccountUsages.AnyAsync(u => u.AccountNumber == input.AccountNumber))
        {
            db.AccountUsages.Add(
                new AccountUsage
                {
                    AccountNumber = input.AccountNumber,
                    Category = chart.Find(input.AccountNumber)!.Category,
                }
            );
        }
        await db.SaveChangesAsync();
        return Results.Created($"/api/entries/{entry.Id}", entry);
    }
);

// SPA fallback for client-side routes (not for unknown /api paths or files).
app.MapFallbackToFile("{*path:nonfile:regex(^(?!api/).*$)}", "index.html");

app.Run();

record EntryInput(DateOnly Date, string Text, int AccountNumber, decimal Amount);

record JournalEntryDto(int Id, DateOnly Date, string Text, int AccountNumber, decimal Amount, decimal Balance);

record OpeningBalanceUpdate(decimal Amount, DateOnly StartDate);

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
