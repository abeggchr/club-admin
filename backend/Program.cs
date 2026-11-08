using backend.Accounting;
using backend.Invoicing;
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

builder.Services.AddObjectStorage(builder.Configuration);
builder.Services.AddScoped<InvoiceCreator>();
InvoiceDocument.UseCommunityLicense();

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

        var earliest = await db.Transactions.AnyAsync()
            ? await db.Transactions.MinAsync(t => t.Date)
            : (DateOnly?)null;
        if (TransactionRules.ValidateOpeningStartDate(update.StartDate, earliest) is { } dateError)
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

        var entries = await db.JournalEntries.Include(e => e.Transaction).ToListAsync();
        // Balances are computed over the whole journal; filters only hide rows.
        return JournalFilter
            .Apply(AccountingCalculator.RunningBalances(opening, entries), from, to, accountNumber, search)
            .Select(r => new JournalEntryDto(
                r.Entry.Id,
                r.Entry.TransactionId,
                r.Entry.Date,
                r.Entry.Text,
                r.Entry.AccountNumber,
                r.Entry.Amount,
                r.Balance
            ))
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

        var entries = await db.JournalEntries
            .Include(e => e.Transaction)
            .Where(e => e.Transaction.Date <= date)
            .ToListAsync();
        var transactions = await db.Transactions.Where(t => t.Date <= date).ToListAsync();

        var journalBalance = AccountingCalculator.ExpectedBalance(opening, entries, date);
        var transactionBalance = AccountingCalculator.TransactionBalance(opening, transactions, date);
        return Results.Ok(
            new
            {
                date,
                balance = journalBalance,
                transactionBalance,
                differs = AccountingCalculator.BalancesDiffer(journalBalance, transactionBalance),
            }
        );
    }
);

api.MapGet(
    "/transactions",
    async (AppDbContext db) =>
    {
        var opening = await db.Settings.FindAsync(Settings.SingletonId);
        return Results.Ok(opening is null ? [] : await LoadTransactionDtosAsync(db, opening));
    }
);

api.MapPost(
    "/transactions",
    async (TransactionInput input, AppDbContext db, ChartOfAccounts chart) =>
    {
        var lines = input.Entries ?? [];
        if (lines.Any(l => l.Id is not null))
        {
            return Results.BadRequest(new { error = "Neue Buchungen dürfen keine Id haben." });
        }
        var opening = await db.Settings.FindAsync(Settings.SingletonId);
        var drafts = lines.Select(l => new EntryDraft(l.Text, l.AccountNumber, l.Amount)).ToList();
        if (TransactionRules.Validate(opening, chart, input.Date, input.Amount, drafts) is { } error)
        {
            return Results.BadRequest(new { error });
        }
        if (input.PersonId is { } personId && !await db.Persons.AnyAsync(p => p.Id == personId))
        {
            return Results.BadRequest(new { error = "Die Person existiert nicht." });
        }

        var transaction = new Transaction
        {
            Date = input.Date,
            Amount = input.Amount,
            Text = NormalizeText(input.Text),
            PersonId = input.PersonId,
            Entries = lines.Select(l => Line(new Entry(), l.Text, l.AccountNumber, l.Amount)).ToList(),
        };
        db.Transactions.Add(transaction);
        await SaveEntryChangeAsync(db, chart);
        return Results.Created(
            $"/api/transactions/{transaction.Id}",
            await LoadTransactionDtoAsync(db, opening!, transaction.Id)
        );
    }
);

api.MapPut(
    "/transactions/{id:int}",
    async (int id, TransactionInput input, AppDbContext db, ChartOfAccounts chart) =>
    {
        var transaction = await db.Transactions.Include(t => t.Entries).FirstOrDefaultAsync(t => t.Id == id);
        if (transaction is null)
        {
            return Results.NotFound();
        }

        var lines = input.Entries ?? [];
        var stored = transaction.Entries.ToDictionary(e => e.Id);
        var givenIds = lines.Where(l => l.Id is not null).Select(l => l.Id!.Value).ToList();
        if (givenIds.Any(i => !stored.ContainsKey(i)))
        {
            return Results.BadRequest(new { error = "Eine Buchung gehört nicht zu dieser Bewegung." });
        }
        if (givenIds.Distinct().Count() != givenIds.Count)
        {
            return Results.BadRequest(new { error = "Eine Buchung kommt mehrfach vor." });
        }

        var opening = await db.Settings.FindAsync(Settings.SingletonId);
        var drafts = lines
            .Select(l => new EntryDraft(l.Text, l.AccountNumber, l.Amount, l.Id is { } i ? stored[i].AccountNumber : null))
            .ToList();
        if (TransactionRules.Validate(opening, chart, input.Date, input.Amount, drafts) is { } error)
        {
            return Results.BadRequest(new { error });
        }
        if (input.PersonId is { } personId && !await db.Persons.AnyAsync(p => p.Id == personId))
        {
            return Results.BadRequest(new { error = "Die Person existiert nicht." });
        }

        transaction.Date = input.Date;
        transaction.Amount = input.Amount;
        transaction.Text = NormalizeText(input.Text);
        transaction.PersonId = input.PersonId;
        foreach (var line in lines)
        {
            if (line.Id is { } entryId)
            {
                Line(stored[entryId], line.Text, line.AccountNumber, line.Amount);
            }
            else
            {
                transaction.Entries.Add(Line(new Entry(), line.Text, line.AccountNumber, line.Amount));
            }
        }
        db.JournalEntries.RemoveRange(stored.Values.Where(e => !givenIds.Contains(e.Id)));
        await SaveEntryChangeAsync(db, chart);
        return Results.Ok(await LoadTransactionDtoAsync(db, opening!, id));
    }
);

// Deletes the Bewegung together with any Buchungen still stored for it (cascade), syncing AccountUsage.
api.MapDelete(
    "/transactions/{id:int}",
    async (int id, AppDbContext db, ChartOfAccounts chart) =>
    {
        var transaction = await db.Transactions.Include(t => t.Entries).FirstOrDefaultAsync(t => t.Id == id);
        if (transaction is null)
        {
            return Results.NotFound();
        }

        db.JournalEntries.RemoveRange(transaction.Entries);
        db.Transactions.Remove(transaction);
        await SaveEntryChangeAsync(db, chart);
        return Results.NoContent();
    }
);

api.MapGet(
    "/text-templates",
    async (AppDbContext db) => await db.TextTemplates.OrderBy(t => t.Title).ThenBy(t => t.Id).ToListAsync()
);

api.MapPost(
    "/text-templates",
    async (TextTemplateInput input, AppDbContext db) =>
    {
        if (TextTemplateRules.Validate(input.Title, input.Text, input.Greetings) is { } error)
        {
            return Results.BadRequest(new { error });
        }
        var template = ApplyInput(new TextTemplate(), input);
        db.TextTemplates.Add(template);
        await db.SaveChangesAsync();
        return Results.Created($"/api/text-templates/{template.Id}", template);
    }
);

api.MapPut(
    "/text-templates/{id:int}",
    async (int id, TextTemplateInput input, AppDbContext db) =>
    {
        if (TextTemplateRules.Validate(input.Title, input.Text, input.Greetings) is { } error)
        {
            return Results.BadRequest(new { error });
        }
        var template = await db.TextTemplates.FindAsync(id);
        if (template is null)
        {
            return Results.NotFound();
        }
        ApplyInput(template, input);
        await db.SaveChangesAsync();
        return Results.Ok(template);
    }
);

api.MapDelete(
    "/text-templates/{id:int}",
    async (int id, AppDbContext db) =>
    {
        var template = await db.TextTemplates.FindAsync(id);
        if (template is null)
        {
            return Results.NotFound();
        }
        db.TextTemplates.Remove(template);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }
);

api.MapGet(
    "/invoices",
    async (AppDbContext db) =>
    {
        var invoices = await db.Invoices.WithDetails()
            .OrderByDescending(i => i.Id)
            .ToListAsync();
        var people = await PeopleByIdAsync(db, invoices.Select(i => i.PersonId));
        return invoices.Select(i => InvoiceMapping.ToInvoiceListItem(i, people[i.PersonId])).ToList();
    }
);

api.MapGet(
    "/invoices/{id:int}",
    async (int id, AppDbContext db) =>
    {
        var invoice = await db.Invoices.WithDetails()
            .FirstOrDefaultAsync(i => i.Id == id);
        if (invoice is null)
        {
            return Results.NotFound();
        }
        var people = await PeopleByIdAsync(db, [invoice.PersonId]);
        return Results.Ok(InvoiceMapping.ToInvoiceDetail(invoice, people[invoice.PersonId]));
    }
);

api.MapPost(
    "/invoices",
    async (InvoiceInput input, AppDbContext db, InvoiceCreator creator, CancellationToken ct) =>
    {
        var lines = (input.Lines ?? []).Select(l => new InvoiceLineDraft(l.Text, l.Amount)).ToList();
        if (InvoiceRules.Validate(input.PersonId, input.TemplateId, lines) is { } error)
        {
            return Results.BadRequest(new { error });
        }
        var person = await db.Persons.FindAsync([input.PersonId!.Value], ct);
        if (person is null)
        {
            return Results.BadRequest(new { error = "Die Person existiert nicht." });
        }
        var template = await db.TextTemplates.FindAsync([input.TemplateId!.Value], ct);
        if (template is null)
        {
            return Results.BadRequest(new { error = "Die Textvorlage existiert nicht." });
        }

        var result = await creator.CreateAsync(person, template, lines, ct);
        if (result.Invoice is not { } invoice)
        {
            return Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status500InternalServerError);
        }
        return Results.Created($"/api/invoices/{invoice.Id}", InvoiceMapping.ToInvoiceDetail(invoice, person));
    }
);

api.MapGet(
    "/invoices/{id:int}/pdf",
    async (int id, AppDbContext db, IInvoiceStorage storage, HttpContext http, CancellationToken ct) =>
    {
        if (!await db.Invoices.AnyAsync(i => i.Id == id, ct))
        {
            return Results.NotFound();
        }
        var pdf = await storage.GetAsync(id, ct);
        if (pdf is null)
        {
            return Results.NotFound();
        }
        // Set directly: Results.File with a filename would emit "attachment".
        http.Response.Headers.ContentDisposition = $"inline; filename=\"Rechnung-{InvoiceRules.FormatNumber(id)}.pdf\"";
        return Results.Bytes(pdf, "application/pdf");
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

// Sets the fields a client supplies for a Buchung (already validated, so the text is not blank).
static Entry Line(Entry entry, string? text, int accountNumber, decimal amount)
{
    entry.Text = text!.Trim();
    entry.AccountNumber = accountNumber;
    entry.Amount = amount;
    return entry;
}

static async Task<TransactionDto> LoadTransactionDtoAsync(AppDbContext db, Settings opening, int id) =>
    (await LoadTransactionDtosAsync(db, opening)).Single(t => t.Id == id);

// All Bewegungen, newest first, each with the Kontostand after it.
static async Task<Dictionary<int, Person>> PeopleByIdAsync(AppDbContext db, IEnumerable<int> ids)
{
    var wanted = ids.Distinct().ToList();
    return await db.Persons.AsNoTracking().Where(p => wanted.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
}

// Copies an already validated Textvorlage input onto the entity, trimmed.
static TextTemplate ApplyInput(TextTemplate template, TextTemplateInput input)
{
    template.Title = input.Title!.Trim();
    template.Text = input.Text!.Trim();
    template.Greetings = input.Greetings!.Trim();
    return template;
}

static string? NormalizeText(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

static async Task<List<TransactionDto>> LoadTransactionDtosAsync(AppDbContext db, Settings opening)
{
    var transactions = await db.Transactions.Include(t => t.Entries).AsNoTracking().ToListAsync();
    return AccountingCalculator
        .TransactionBalances(opening, transactions)
        .Select(r => new TransactionDto(
            r.Transaction.Id,
            r.Transaction.Date,
            r.Transaction.Amount,
            r.Transaction.Text,
            r.Transaction.PersonId,
            r.Balance,
            r.Transaction.Entries.Select(e => e.Id).Order().ToList()
        ))
        .ToList();
}

record JournalEntryDto(
    int Id,
    int TransactionId,
    DateOnly Date,
    string Text,
    int AccountNumber,
    decimal Amount,
    decimal Balance
);

record TransactionEntryInput(int? Id, string? Text, int AccountNumber, decimal Amount);

record TransactionInput(DateOnly Date, decimal Amount, string? Text, int? PersonId, List<TransactionEntryInput>? Entries);

record TransactionDto(int Id, DateOnly Date, decimal Amount, string? Text, int? PersonId, decimal Balance, List<int> EntryIds);

record TextTemplateInput(string? Title, string? Text, string? Greetings);

record InvoiceLineInput(string? Text, decimal Amount);

record InvoiceInput(int? PersonId, int? TemplateId, List<InvoiceLineInput>? Lines);

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
