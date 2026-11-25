using backend.Accounting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.IncomeStatement;

[ApiController]
[Route("api")]
public class IncomeStatementController(AppDbContext db, IncomeStatementConfig config, ChartOfAccounts chart, ISystemClock clock)
    : ControllerBase
{
    [HttpGet("income-statement")]
    public async Task<IActionResult> GetIncomeStatement([FromQuery] string? view)
    {
        var viewError = IncomeStatementViewRules.Validate(view);
        if (viewError is not null)
        {
            return BadRequest(new { error = viewError });
        }

        var settings = await db.Settings.FindAsync(backend.Settings.Settings.SingletonId);
        if (settings is null)
        {
            return BadRequest(new { error = "Kein Startdatum gesetzt. Bitte legen Sie das Startdatum in den Einstellungen fest." });
        }

        var today = clock.Today();
        // Entries outside the period (before the Start Date or after today) are left out.
        var entries = await db
            .JournalEntries.Include(e => e.Transaction)
            .Where(e => e.Transaction.Date >= settings.StartDate)
            .Where(e => e.Transaction.Date <= today)
            .ToListAsync();

        var result = IncomeStatementCalculator.Compute(
            config,
            chart,
            entries,
            settings.StartDate,
            today,
            IncomeStatementViewRules.For(view)
        );
        return Ok(result);
    }
}
