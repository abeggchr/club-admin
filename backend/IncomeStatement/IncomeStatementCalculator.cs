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
        IEnumerable<Entry> entries,
        DateOnly from,
        DateOnly to,
        IncomeStatementView view = IncomeStatementView.All
    )
    {
        // Credits/debits per account number, following the signed Entry amounts.
        var credits = Accumulate(entries, entry => entry.Amount >= 0);
        var debits = Accumulate(entries, entry => entry.Amount < 0, amount => -amount);

        // The trailing "Nicht zugeordnet" group collects Accounts with Entries that no configured line
        // covers; its amounts count toward the section totals so the report reconciles with the Journal.
        const string unmappedLabel = "Nicht zugeordnet";

        // Simplified view: a line stays when any of its Accounts has at least one Entry in the period,
        // even if it nets to 0.00; groups left without lines are hidden.
        Func<Line, bool> keepLine = view == IncomeStatementView.Simplified
            ? line =>
                Enumerable.Range(line.From, line.Last - line.From + 1).Any(number =>
                    credits.ContainsKey(number) || debits.ContainsKey(number)
                )
            : _ => true;

        decimal totalRevenue = 0m;
        decimal totalExpense = 0m;
        var sections = new List<SectionDto>();
        foreach (var section in config.Sections)
        {
            decimal sectionTotal = 0m;
            var groups = new List<GroupDto>();
            foreach (var group in section.Groups)
            {
                decimal groupTotal = 0m;
                var lines = new List<LineDto>();
                foreach (var line in group.Lines.Where(keepLine))
                {
                    decimal amount = 0m;
                    for (var number = line.From; number <= line.Last; number++)
                    {
                        amount += LineAmount(chart.Find(number), credits, debits);
                    }
                    groupTotal += amount;
                    lines.Add(new LineDto(line.Label, amount));
                }

                if (lines.Count == 0)
                {
                    continue;
                }
                sectionTotal += groupTotal;
                groups.Add(new GroupDto(group.Label, groupTotal, lines));
            }

            // The trailing group for this section's Category, hidden when it has no non-zero line.
            var unmapped = UnmappedEntries(config, chart, entries, section.Category);
            if (unmapped.Count > 0)
            {
                decimal unmappedTotal = 0m;
                var unmappedLines = new List<LineDto>();
                foreach (var (number, amount) in unmapped.OrderBy(kv => kv.Key))
                {
                    if (amount == 0m)
                    {
                        continue;
                    }
                    unmappedTotal += amount;
                    unmappedLines.Add(new LineDto(chart.Find(number)!.Name, amount));
                }
                if (unmappedLines.Count > 0)
                {
                    sectionTotal += unmappedTotal;
                    groups.Add(new GroupDto(unmappedLabel, unmappedTotal, unmappedLines));
                }
            }

            if (section.Category == Category.Income)
            {
                totalRevenue = sectionTotal;
            }
            else
            {
                totalExpense = sectionTotal;
            }
            sections.Add(new SectionDto(section.Label, sectionTotal, groups));
        }

        return new IncomeStatementResult(new PeriodDto(from, to), sections, totalRevenue, totalExpense, totalRevenue - totalExpense);
    }

    /// <summary>
    /// The per-Account contribution toward the report, signed for the Account's Category:
    /// Ertrag counts credits minus debits, Aufwand counts debits minus credits.
    /// </summary>
    private static decimal LineAmount(Account? account, Dictionary<int, decimal> credits, Dictionary<int, decimal> debits)
    {
        if (account is null)
        {
            return 0m;
        }
        var credit = credits.TryGetValue(account.Number, out var c) ? c : 0m;
        var debit = debits.TryGetValue(account.Number, out var d) ? d : 0m;
        return account.Category == Category.Income ? credit - debit : debit - credit;
    }

    /// <summary>Sums the (optionally sign-flipped) Entry amounts per account number.</summary>
    private static Dictionary<int, decimal> Accumulate(
        IEnumerable<Entry> entries,
        Func<Entry, bool> select,
        Func<decimal, decimal>? map = null
    )
    {
        var result = new Dictionary<int, decimal>();
        foreach (var entry in entries.Where(select))
        {
            var amount = map is null ? entry.Amount : map(entry.Amount);
            result[entry.AccountNumber] = (result.TryGetValue(entry.AccountNumber, out var sum) ? sum : 0m) + amount;
        }
        return result;
    }

    /// <summary>
    /// Entries on Accounts of the section's Category that no configured line covers: a positive
    /// value means the Account contributes to the section total (credits for Ertrag, debits for
    /// Aufwand); the signed Entry amounts are summed directly.
    /// </summary>
    private static Dictionary<int, decimal> UnmappedEntries(
        IncomeStatementConfig config,
        ChartOfAccounts chart,
        IEnumerable<Entry> entries,
        Category category
    )
    {
        var unmapped = new Dictionary<int, decimal>();
        var mappedNumbers = new HashSet<int>();
        foreach (var section in config.Sections)
        {
            foreach (var group in section.Groups)
            {
                foreach (var line in group.Lines)
                {
                    for (var number = line.From; number <= line.Last; number++)
                    {
                        mappedNumbers.Add(number);
                    }
                }
            }
        }
        foreach (var entry in entries)
        {
            if (mappedNumbers.Contains(entry.AccountNumber))
            {
                continue;
            }
            var account = chart.Find(entry.AccountNumber);
            if (account is null || account.Category != category)
            {
                continue;
            }
            // Ertrag counts credits minus debits, Aufwand counts debits minus credits.
            var contribution = account.Category == Category.Income ? entry.Amount : -entry.Amount;
            unmapped[entry.AccountNumber] = (unmapped.TryGetValue(entry.AccountNumber, out var amount) ? amount : 0m) + contribution;
        }
        return unmapped;
    }
}

/// <summary>The whole report: sections with groups and lines, section totals, and the overall result.</summary>
public record IncomeStatementResult(
    PeriodDto Period,
    IReadOnlyList<SectionDto> Sections,
    decimal TotalRevenue,
    decimal TotalExpense,
    decimal OverallResult
);

/// <summary>The view of the report: simplified shows only lines with Entries in the period, all shows every configured line.</summary>
public enum IncomeStatementView
{
    Simplified,
    All,
}

/// <summary>Reads the view from the request: validates "simplified"/"all" like the other rules, or maps the valid raw value to its view.</summary>
public static class IncomeStatementViewRules
{
    public static string? Validate(string? raw) =>
        raw is null or "" or "simplified" or "all" ? null : $"Unbekannte Ansicht '{raw}'. Bitte 'simplified' oder 'all' verwenden.";

    public static IncomeStatementView For(string? raw) => raw == "all" ? IncomeStatementView.All : IncomeStatementView.Simplified;
}
