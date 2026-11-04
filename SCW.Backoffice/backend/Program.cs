using backend.Person;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
);

var app = builder.Build();

// Apply pending EF migrations on startup.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
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

var summaries = new[]
{
    "Freezing",
    "Bracing",
    "Chilly",
    "Cool",
    "Mild",
    "Warm",
    "Balmy",
    "Hot",
    "Sweltering",
    "Scorching",
};

api.MapGet(
        "/weatherforecast",
        () =>
        {
            var forecast = Enumerable
                .Range(1, 5)
                .Select(index => new WeatherForecast(
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
                .ToArray();
            return forecast;
        }
    )
    .WithName("GetWeatherForecast");

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

// SPA fallback for client-side routes (not for unknown /api paths or files).
app.MapFallbackToFile("{*path:nonfile:regex(^(?!api/).*$)}", "index.html");

app.Run();

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

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
