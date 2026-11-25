using backend.Accounting;
using backend.Invoicing;
using backend.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Settings;

[ApiController]
[Route("api/settings")]
public class SettingsController(AppDbContext db, IEmailSender emailSender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSettings() =>
        await db.Settings.FindAsync(Settings.SingletonId) is { } settings
            ? Ok(SettingsResponse.From(settings))
            : NotFound();

    [HttpPut]
    public async Task<IActionResult> UpdateSettings(SettingsUpdate update)
    {
        var earliest = await db.Transactions.AnyAsync()
            ? await db.Transactions.MinAsync(t => t.Date)
            : (DateOnly?)null;
        var error =
            SettingsRules.Validate(
                update.OpeningBalance,
                update.StartDate,
                update.Iban,
                update.Name,
                update.Address,
                update.PostalCode,
                update.City,
                earliest
            ) ?? SmtpRules.Validate(update.Smtp);
        if (error is not null)
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
        ApplySmtp(settings, update.Smtp);
        // Write-only like the SMTP password: an empty field keeps the stored key.
        if (!string.IsNullOrWhiteSpace(update.EPostApiKey))
        {
            settings.EPostApiKey = update.EPostApiKey.Trim();
        }

        await db.SaveChangesAsync();
        return Ok(SettingsResponse.From(settings));
    }

    [HttpPost("smtp/test")]
    public async Task<IActionResult> SendTestEmail(TestEmailRequest request, CancellationToken ct)
    {
        if (SmtpRules.IsEmpty(request.Smtp))
        {
            return BadRequest(new { error = "Die SMTP-Einstellungen sind unvollständig." });
        }
        if (SmtpRules.Validate(request.Smtp) is { } error)
        {
            return BadRequest(new { error });
        }
        if (!SmtpRules.IsEmailAddress(request.Receiver))
        {
            return BadRequest(new { error = "Der Empfänger ist keine gültige E-Mail-Adresse." });
        }

        var smtp = request.Smtp!;
        // An empty password means "use the stored one", as on save.
        var password = string.IsNullOrEmpty(smtp.Password)
            ? (await db.Settings.FindAsync(Settings.SingletonId))?.SmtpPassword
            : smtp.Password;
        var connection = new SmtpConnection(
            smtp.Host!.Trim(),
            smtp.Port!.Value,
            smtp.Security!.Value,
            NullIfBlank(smtp.Username),
            password,
            smtp.FromAddress!.Trim(),
            NullIfBlank(smtp.FromName)
        );

        return await emailSender.SendTestAsync(connection, request.Receiver!.Trim(), ct) is { } failure
            ? BadRequest(new { error = $"Die Test-E-Mail konnte nicht gesendet werden: {failure}" })
            : Ok();
    }

    // An empty SMTP group clears everything including the password; an empty password otherwise keeps the stored one.
    private static void ApplySmtp(Settings settings, SmtpInput? smtp)
    {
        if (SmtpRules.IsEmpty(smtp))
        {
            settings.SmtpHost = null;
            settings.SmtpPort = null;
            settings.SmtpSecurity = null;
            settings.SmtpUsername = null;
            settings.SmtpPassword = null;
            settings.SmtpFromAddress = null;
            settings.SmtpFromName = null;
            return;
        }
        settings.SmtpHost = smtp!.Host!.Trim();
        settings.SmtpPort = smtp.Port;
        settings.SmtpSecurity = smtp.Security;
        settings.SmtpUsername = NullIfBlank(smtp.Username);
        if (!string.IsNullOrEmpty(smtp.Password))
        {
            settings.SmtpPassword = smtp.Password;
        }
        settings.SmtpFromAddress = smtp.FromAddress!.Trim();
        settings.SmtpFromName = NullIfBlank(smtp.FromName);
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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

    // Copies an already validated Text Template input onto the entity, trimmed.
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
    string? City,
    SmtpInput? Smtp = null,
    string? EPostApiKey = null
);

public record TestEmailRequest(string? Receiver, SmtpInput? Smtp);

public record SettingsResponse(
    decimal OpeningBalance,
    DateOnly StartDate,
    string Iban,
    string Name,
    string Address,
    string PostalCode,
    string City,
    SmtpResponse Smtp,
    bool EmailConfigured,
    bool EPostConfigured
)
{
    public static SettingsResponse From(Settings s) =>
        new(
            s.OpeningBalance,
            s.StartDate,
            s.Iban,
            s.Name,
            s.Address,
            s.PostalCode,
            s.City,
            new SmtpResponse(
                s.SmtpHost,
                s.SmtpPort,
                s.SmtpSecurity,
                s.SmtpUsername,
                !string.IsNullOrEmpty(s.SmtpPassword),
                s.SmtpFromAddress,
                s.SmtpFromName
            ),
            s.SmtpHost is not null,
            !string.IsNullOrEmpty(s.EPostApiKey)
        );
}
