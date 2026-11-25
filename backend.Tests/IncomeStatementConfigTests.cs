using backend.IncomeStatement;

namespace backend.Tests;

/// <summary>
/// The Income Statement layout loader: order preservation, ranges, and structure errors.
/// </summary>
public class IncomeStatementConfigTests
{
    [Fact]
    public void Key_order_is_preserved_and_ranges_are_recognized()
    {
        var config = IncomeStatementConfig.Parse(
            """
            Erfolgsrechnung:
              Ertrag:
                I. Beiträge:
                  Jahresbeiträge: 3400
              Aufwand:
                I. Anlässe:
                  Anlässe: 4000
                II. Diverse:
                  Diverse Einnahmen: 3610-3620
            """
        );

        var sections = config.Sections;
        Assert.Equal(2, sections.Count);
        Assert.Equal("Ertrag", sections[0].Label);
        Assert.Equal("Aufwand", sections[1].Label);

        var group = sections[0].Groups[0];
        Assert.Equal("I. Beiträge", group.Label);
        Assert.Equal(new Line("Jahresbeiträge", 3400), Assert.Single(group.Lines));
        Assert.False(group.Lines[0].IsRange);

        var range = sections[1].Groups[1].Lines[0];
        Assert.Equal("Diverse Einnahmen", range.Label);
        Assert.Equal(3610, range.From);
        Assert.Equal(3620, range.Last);
        Assert.True(range.IsRange);
    }

    [Fact]
    public void The_checked_in_file_loads()
    {
        var config = IncomeStatementConfig.Load(
            Path.Combine(AppContext.BaseDirectory, "IncomeStatement", "income-statement.yaml")
        );

        Assert.Equal(["Ertrag", "Aufwand"], config.Sections.Select(s => s.Label));
        Assert.Equal(3, config.Sections[0].Groups.Count);
        Assert.Equal(2, config.Sections[1].Groups.Count);
        Assert.Equal(
            18,
            config.Sections[0].Groups.Sum(g => g.Lines.Count) + config.Sections[1].Groups.Sum(g => g.Lines.Count)
        );
    }

    [Theory]
    [InlineData("not: yaml: [")]
    [InlineData("Erfolgsrechnung:\n  Ertrag: x")]
    [InlineData("Erfolgsrechnung: 1")]
    [InlineData("")]
    [InlineData("Etwas anderes: {}")]
    public void Malformed_file_is_rejected(string yaml)
    {
        Assert.Throws<IncomeStatementConfigException>(() => IncomeStatementConfig.Parse(yaml));
    }

    [Fact]
    public void An_unknown_single_reference_is_rejected()
    {
        // 'abc' does not parse to a number or a range, so it is rejected here.
        Assert.Throws<IncomeStatementConfigException>(
            () =>
                IncomeStatementConfig.Parse(
                    "Erfolgsrechnung:\n  Ertrag:\n    I. X:\n      Zeile: abc"
                )
        );
    }

    [Theory]
    [InlineData("Erfolgsrechnung:\n  Ertrag:\n    I. Beiträge:\n      Jahresbeiträge: 34a0")]
    [InlineData("Erfolgsrechnung:\n  Ertrag:\n    I. Beiträge:\n      Jahresbeiträge: -5")]
    public void An_invalid_reference_is_rejected(string yaml)
    {
        Assert.Throws<IncomeStatementConfigException>(() => IncomeStatementConfig.Parse(yaml));
    }
}
