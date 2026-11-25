using backend.Accounting;

namespace backend.IncomeStatement;

/// <summary>
/// Pure Income Statement rules. No database, no clock: the caller supplies the Entries of the period.
/// Per Account, Ertrag is credits minus debits and Aufwand is debits minus credits, using the signed
/// Entry amounts and the Account's Category.
/// </summary>
public static class IncomeStatementCalculator
{
    public static IncomeStatementResult Compute(
        IncomeStatementConfig config,
        ChartOfAccounts chart,
        IEnumerable<Entry> entries
    )
    {
        // Credits/debits per account number, following the signed Entry amounts.
        var credits = new Dictionary<int, decimal>();
        var debits = new Dictionary<int, decimal>();
        foreach (var entry in entries)
        {
            if (entry.Amount >= 0)
            {
                credits[entry.AccountNumber] = (credits.TryGetValue(entry.AccountNumber, out var c) ? c : 0m) + entry.Amount;
            }
            else
            {
                debits[entry.AccountNumber] = (debits.TryGetValue(entry.AccountNumber, out var d) ? d : 0m) - entry.Amount;
            }
        }

        decimal totalRevenue = 0m;
        decimal totalExpense = 0m;
        var sections = new List<SectionResult>();
        foreach (var section in config.Sections)
        {
            decimal sectionTotal = 0m;
            var groups = new List<GroupResult>();
            foreach (var group in section.Groups)
            {
                decimal groupTotal = 0m;
                var lines = new List<LineResult>();
                foreach (var line in group.Lines)
                {
                    decimal amount = 0m;
                    for (var number = line.From; number <= line.Last; number++)
                    {
                        var account = chart.Find(number);
                        if (account is null)
                        {
                            continue;
                        }
                        // Ertrag counts credits minus debits; Aufwand counts debits minus credits.
                        amount +=
                            account.Category == Category.Income
                                ? (credits.TryGetValue(number, out var c) ? c : 0m) - (debits.TryGetValue(number, out var d) ? d : 0m)
                                : (debits.TryGetValue(number, out var d2) ? d2 : 0m) - (credits.TryGetValue(number, out var c2) ? c2 : 0m);
                    }
                    groupTotal += amount;
                    lines.Add(new LineResult(line.Label, amount));
                }
                sectionTotal += groupTotal;
                groups.Add(new GroupResult(group.Label, groupTotal, lines));
            }
            if (section.Label == "Ertrag")
            {
                totalRevenue = sectionTotal;
            }
            else
            {
                totalExpense = sectionTotal;
            }
            sections.Add(new SectionResult(section.Label, sectionTotal, groups));
        }

        return new IncomeStatementResult(sections, totalRevenue, totalExpense, totalRevenue - totalExpense);
    }
}

/// <summary>The whole report tree: sections with groups and lines, section totals, and the overall result.</summary>
public record IncomeStatementResult(
    IReadOnlyList<SectionResult> Sections,
    decimal TotalRevenue,
    decimal TotalExpense,
    decimal OverallResult
);

public record SectionResult(string Label, decimal Total, IReadOnlyList<GroupResult> Groups);

public record GroupResult(string Label, decimal Total, IReadOnlyList<LineResult> Lines);

public record LineResult(string Label, decimal Amount);
