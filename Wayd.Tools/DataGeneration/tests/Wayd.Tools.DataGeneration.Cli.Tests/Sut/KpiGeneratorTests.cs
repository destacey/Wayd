using FluentAssertions;
using Wayd.Tools.DataGeneration.Cli.Generation;

namespace Wayd.Tools.DataGeneration.Cli.Tests.Sut;

/// <summary>
/// The generated KPIs, checked against the rules the KPI import and the KPI aggregate enforce. A row breaking one
/// of them is rejected at seed time, far from the generator code that produced it.
/// </summary>
public class KpiGeneratorTests
{
    private static readonly DateOnly _asOf = new(2026, 6, 15);

    private static GenerationContext Context(int seed = 1234) => new() { AsOf = _asOf, Seed = seed };

    private static (GeneratedOrg Org, GeneratedKpis Data) Generate(KpiOptions? options = null, int seed = 1234)
    {
        var context = Context(seed);
        var org = new OrgGenerator(new OrgOptions { ValueStreams = 3, Teams = 18 }, context).Generate();

        return (org, new KpiGenerator(org.Structure, options ?? new KpiOptions(), context).Generate());
    }

    private static readonly (GeneratedOrg Org, GeneratedKpis Data) _default = Generate();

    private static GeneratedKpis Data => _default.Data;

    private static double Resolved(KpiModel k) =>
        k.TargetKind == "RelativeChange"
            ? k.BaselineValue + (k.Direction == "Increase" ? 1 : -1) * Math.Abs(k.BaselineValue) * k.TargetValue / 100d
            : k.TargetValue;

    [Fact]
    public void Generate_GivesEveryTeamItsKpisAndEveryArtHalfAsMany()
    {
        // Arrange
        var structure = _default.Org.Structure;
        var teams = structure.ValueStreams.SelectMany(v => v.Arts).SelectMany(a => a.Teams).Select(t => t.TeamCode).ToList();
        var arts = structure.ValueStreams.SelectMany(v => v.Arts).Select(a => a.TeamCode).OfType<string>().ToList();

        // Act
        var byTeam = Data.Kpis.GroupBy(k => k.TeamCode).ToDictionary(g => g.Key, g => g.Count());

        // Assert
        teams.Should().NotBeEmpty();
        arts.Should().NotBeEmpty();
        teams.Should().AllSatisfy(code => byTeam.GetValueOrDefault(code).Should().BeInRange(2, 3));
        arts.Should().AllSatisfy(code => byTeam.GetValueOrDefault(code).Should().Be(1));
        byTeam.Keys.Should().BeSubsetOf(teams.Concat(arts));
    }

    [Fact]
    public void Generate_NamesEachKpiOncePerTeam()
    {
        // Arrange & Act
        var handles = Data.Kpis.Select(k => k.Handle).ToList();

        // Assert
        handles.Should().OnlyHaveUniqueItems();
        Data.Kpis.GroupBy(k => k.TeamCode).Should().AllSatisfy(g => g.Select(k => k.Name).Should().OnlyHaveUniqueItems());
    }

    [Fact]
    public void Generate_SetsEveryGoalTheAggregateCanHold()
    {
        // Arrange & Act
        var kpis = Data.Kpis;

        // Assert — the baseline short of the target in the KPI's direction, and an at-risk line between them
        kpis.Should().AllSatisfy(k =>
        {
            var target = Resolved(k);
            if (k.Direction == "Increase")
                k.BaselineValue.Should().BeLessThan(target, k.Handle);
            else
                k.BaselineValue.Should().BeGreaterThan(target, k.Handle);

            if (k.TargetKind == "RelativeChange")
            {
                k.BaselineValue.Should().NotBe(0, k.Handle);
                k.TargetValue.Should().BePositive(k.Handle);
            }

            if (k.AtRiskKind is not null)
            {
                k.AtRiskKind.Should().Be("ProgressToTarget");
                k.AtRiskValue.Should().BeGreaterThan(0).And.BeLessThan(100);
            }

            k.Category.Should().BeOneOf(KpiVocabulary.Categories);
            k.ExpressedAs.Should().BeOneOf("Value", "ChangeFromBaseline", "ProgressToTarget");
        });
    }

    [Fact]
    public void Generate_AimsEachAbsoluteTargetARealStretchPastItsBaseline()
    {
        // Arrange
        var absolute = Data.Kpis.Where(k => k.TargetKind == "Absolute" && k.BaselineValue != 0).ToList();

        // Act — how far the target sits past the baseline, as a share of it, in the KPI's direction
        var stretch = absolute
            .Select(k => (k.Direction == "Increase" ? k.TargetValue - k.BaselineValue : k.BaselineValue - k.TargetValue) / Math.Abs(k.BaselineValue))
            .ToList();

        // Assert — a target one rounding step past the baseline would read as met from the first month
        absolute.Should().NotBeEmpty();
        stretch.Count(s => s >= 0.1).Should().BeGreaterThan(stretch.Count * 9 / 10);
    }

    [Fact]
    public void Generate_RecordsWholeMonthsThatNeverOverlap_EndingBeforeThisMonth()
    {
        // Arrange
        var thisMonth = new DateOnly(_asOf.Year, _asOf.Month, 1);

        // Act
        var byKpi = Data.Measurements.GroupBy(m => m.KpiHandle);

        // Assert
        byKpi.Should().AllSatisfy(history =>
        {
            var ordered = history.OrderBy(m => m.PeriodStart).ToList();
            ordered.Should().AllSatisfy(m =>
            {
                m.PeriodStart.Day.Should().Be(1);
                m.PeriodEnd.Should().Be(m.PeriodStart.AddMonths(1).AddDays(-1));
                m.PeriodEnd.Should().BeBefore(thisMonth);
            });
            ordered.Zip(ordered.Skip(1)).Should().AllSatisfy(pair => pair.Second.PeriodStart.Should().BeAfter(pair.First.PeriodEnd));
        });
    }

    [Fact]
    public void Generate_TakesTheBaselineOverTheFirstThreeMonths()
    {
        // Arrange & Act
        var kpi = Data.Kpis.First();
        var history = Data.Measurements.Where(m => m.KpiHandle == kpi.Handle).OrderBy(m => m.PeriodStart).ToList();

        // Assert
        kpi.BaselineWindowStart.Should().Be(history[0].PeriodStart);
        kpi.BaselineWindowEnd.Should().Be(history[2].PeriodEnd);
        kpi.BaselineValue.Should().BeApproximately(history.Take(3).Average(m => m.Value), 1);
    }

    [Fact]
    public void Generate_ClosesSomeKpisPartway_KeepingTheirHistory()
    {
        // Arrange
        var longest = Data.Measurements.GroupBy(m => m.KpiHandle).Max(g => g.Count());

        // Act
        var closed = Data.Kpis.Where(k => k.IsClosed).ToList();

        // Assert
        closed.Should().NotBeEmpty();
        closed.Count.Should().BeLessThan(Data.Kpis.Count / 3);
        closed.Should().AllSatisfy(k =>
            Data.Measurements.Count(m => m.KpiHandle == k.Handle).Should().BeInRange(6, longest - 1));
    }

    [Fact]
    public void Generate_IsTheSameForTheSameSeed()
    {
        // Arrange & Act
        var again = Generate().Data;

        // Assert
        again.Kpis.Should().Equal(Data.Kpis);
        again.Measurements.Should().Equal(Data.Measurements);
    }

    [Fact]
    public void Generate_ProducesNothing_WhenTeamsCarryNoKpis()
    {
        // Arrange & Act
        var data = Generate(new KpiOptions { KpisPerTeam = 0 }).Data;

        // Assert
        data.Kpis.Should().BeEmpty();
        data.Measurements.Should().BeEmpty();
        data.Categories.Should().Equal(KpiVocabulary.Categories);
    }

    [Fact]
    public void Generate_MoreKpisPerTeam_GivesEveryTeamMore()
    {
        // Arrange & Act
        var few = Generate(new KpiOptions { KpisPerTeam = 1 }).Data.Kpis.Count;
        var many = Generate(new KpiOptions { KpisPerTeam = 5 }).Data.Kpis.Count;

        // Assert
        many.Should().BeGreaterThan(few * 3);
    }
}
