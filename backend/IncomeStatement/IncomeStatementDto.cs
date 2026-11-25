namespace backend.IncomeStatement;

/// <summary>
/// The report tree returned by /api/income-statement. The calculator returns these records
/// directly, so there is no separate result-to-DTO mapping.
/// </summary>
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
