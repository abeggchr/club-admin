using backend.Accounting;
using backend.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Settings;

[ApiController]
[Route("api")]
public class SettingsController(AppDbContext db) : ControllerBase
{
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings() =>
        await db.Settings.FindAsync(Settings.SingletonId) is { } settings
            ? Ok(settings)
            : NotFound();

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(SettingsUpdate update)
    {
        var earliest = await db.Transactions.AnyAsync()
            ? await db.Transactions.MinAsync(t => t.Date)
            : (DateOnly?)null;
        if (
            SettingsRules.Validate(
                update.OpeningBalance,
                update.StartDate,
                update.Iban,
                update.Name,
                update.Address,
                update.PostalCode,
                update.City,
                earliest
            ) is
            { } error
        )
        {
            return BadRequest(new { error });
        }

        var settings = await db.Settings.FindAsync(Settings.SingletonId);
        if (settings is null)
        {
            settings = new Settings();
            db.Settings.Add(settings);
        }

        settings.OpeningBalance = update.OpeningBalance;
        settings.StartDate = update.StartDate;
        settings.Iban = Iban.Normalize(update.Iban)!;
        settings.Name = update.Name!.Trim();
        settings.Address = update.Address!.Trim();
        settings.PostalCode = update.PostalCode!.Trim();
        settings.City = update.City!.Trim();

        await db.SaveChangesAsync();
        return Ok(settings);
    }
}

public record SettingsUpdate(
    decimal OpeningBalance,
    DateOnly StartDate,
    string? Iban,
    string? Name,
    string? Address,
    string? PostalCode,
    string? City
);
