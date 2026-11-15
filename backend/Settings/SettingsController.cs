using backend.Accounting;
using backend.Invoicing;
using backend.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Settings;

[ApiController]
[Route("api/settings")]
public class SettingsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSettings() =>
        await db.Settings.FindAsync(Settings.SingletonId) is { } settings
            ? Ok(settings)
            : NotFound();

    [HttpPut]
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

    [HttpGet("text-templates")]
    public async Task<IActionResult> GetTextTemplates() =>
        Ok(await db.TextTemplates.OrderBy(t => t.Subject).ThenBy(t => t.Id).ToListAsync());

    [HttpPost("text-templates")]
    public async Task<IActionResult> CreateTextTemplate(TextTemplateInput input)
    {
        if (TextTemplateRules.Validate(input.Subject, input.Text, input.Greetings) is { } error)
        {
            return BadRequest(new { error });
        }
        var template = ApplyInput(new TextTemplate(), input);
        db.TextTemplates.Add(template);
        await db.SaveChangesAsync();
        return Created($"/api/settings/text-templates/{template.Id}", template);
    }

    [HttpPut("text-templates/{id:int}")]
    public async Task<IActionResult> UpdateTextTemplate(int id, TextTemplateInput input)
    {
        if (TextTemplateRules.Validate(input.Subject, input.Text, input.Greetings) is { } error)
        {
            return BadRequest(new { error });
        }
        var template = await db.TextTemplates.FindAsync(id);
        if (template is null)
        {
            return NotFound();
        }
        ApplyInput(template, input);
        await db.SaveChangesAsync();
        return Ok(template);
    }

    [HttpDelete("text-templates/{id:int}")]
    public async Task<IActionResult> DeleteTextTemplate(int id)
    {
        var template = await db.TextTemplates.FindAsync(id);
        if (template is null)
        {
            return NotFound();
        }
        db.TextTemplates.Remove(template);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // Copies an already validated Textvorlage input onto the entity, trimmed.
    private static TextTemplate ApplyInput(TextTemplate template, TextTemplateInput input)
    {
        template.Subject = input.Subject!.Trim();
        template.Text = input.Text!.Trim();
        template.Greetings = input.Greetings!.Trim();
        return template;
    }
}

public record TextTemplateInput(string? Subject, string? Text, string? Greetings);

public record SettingsUpdate(
    decimal OpeningBalance,
    DateOnly StartDate,
    string? Iban,
    string? Name,
    string? Address,
    string? PostalCode,
    string? City
);
