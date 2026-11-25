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
    public async Task<IActionResult> GetIncomeStatement()
    {
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

        var result = IncomeStatementCalculator.Compute(config, chart, entries);
        return Ok(
            new IncomeStatementDto(
                new PeriodDto(settings.StartDate, today),
                result.Sections
                    .Select(
                        s =>
                            new SectionDto(
                                s.Label,
                                s.Total,
                                s.Groups
                                    .Select(
                                        g =>
                                            new GroupDto(
                                                g.Label,
                                                g.Total,
                                                g.Lines.Select(l => new LineDto(l.Label, l.Amount)).ToList()
                                            )
                                    )
                                    .ToList()
                            )
                    )
                    .ToList(),
                result.TotalRevenue,
                result.TotalExpense,
                result.OverallResult
            )
        );
    }
}

public record IncomeStatementDto(
    PeriodDto Period,
    IReadOnlyList<SectionDto> Sections,
    decimal TotalRevenue,
    decimal TotalExpense,
    decimal OverallResult
);

public record PeriodDto(DateOnly From, DateOnly To);

public record SectionDto(string Label, decimal Total, IReadOnlyList<GroupDto> Groups);

public record GroupDto(string Label, decimal Total, IReadOnlyList<LineDto> Lines);

public record LineDto(string Label, decimal Amount);
