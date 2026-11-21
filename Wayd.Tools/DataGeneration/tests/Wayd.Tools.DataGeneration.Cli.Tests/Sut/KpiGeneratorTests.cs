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

    private static (GeneratedOrg Org, GeneratedPpm? Ppm, GeneratedKpis Data) Generate(KpiOptions? options = null, int seed = 1234, bool withPpm = true)
    {
        var context = Context(seed);
        var org = new OrgGenerator(new OrgOptions { ValueStreams = 3, Teams = 18 }, context).Generate();
        var ppm = withPpm ? new PpmGenerator(org.Structure, new PpmOptions(), context).Generate() : null;

        return (org, ppm, new KpiGenerator(org.Structure, options ?? new KpiOptions(), context, ppm).Generate());
    }

    private static readonly (GeneratedOrg Org, GeneratedPpm? Ppm, GeneratedKpis Data) _default = Generate();

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
        var byTeam = Data.Kpis.Where(k => k.Subject == KpiSubject.Team).GroupBy(k => k.SubjectHandle).ToDictionary(g => g.Key, g => g.Count());

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
        Data.Kpis.GroupBy(k => (k.Subject, k.SubjectHandle)).Should().AllSatisfy(g => g.Select(k => k.Name).Should().OnlyHaveUniqueItems());
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

            k.Category.Should().BeOneOf(Data.Categories);
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
    public void Generate_RecordsWholeMonthsThatNeverOverlap_ForAPerPeriodKpi_EndingBeforeThisMonth()
    {
        // Arrange
        var thisMonth = new DateOnly(_asOf.Year, _asOf.Month, 1);
        var perPeriod = Data.Kpis.Where(k => k.WindowKind == KpiVocabulary.PerPeriod).Select(k => k.Handle).ToHashSet();

        // Act
        var byKpi = Data.Measurements.Where(m => perPeriod.Contains(m.KpiHandle)).GroupBy(m => m.KpiHandle);

        // Assert
        perPeriod.Should().NotBeEmpty();
        byKpi.Should().AllSatisfy(history =>
        {
            var ordered = history.OrderBy(m => m.PeriodStart).ToList();
            ordered.Should().AllSatisfy(m =>
            {
                m.AsOf.Should().BeNull();
                m.MeasuredAt.Should().BeNull();
                m.PeriodStart!.Value.Day.Should().Be(1);
                m.PeriodEnd.Should().Be(m.PeriodStart.Value.AddMonths(1).AddDays(-1));
                m.PeriodEnd!.Value.Should().BeBefore(thisMonth);
            });
            ordered.Zip(ordered.Skip(1)).Should().AllSatisfy(pair => pair.Second.PeriodStart!.Value.Should().BeAfter(pair.First.PeriodEnd!.Value));
        });
    }

    [Fact]
    public void Generate_CoversEveryWindowKind_AndARollingAverage()
    {
        // Arrange & Act
        var kinds = Data.Kpis.Select(k => k.WindowKind).Distinct().ToList();

        // Assert
        kinds.Should().BeEquivalentTo([KpiVocabulary.PerPeriod, KpiVocabulary.Cumulative, KpiVocabulary.Rolling, KpiVocabulary.PointInTime]);
        Data.Kpis.Should().Contain(k => k.RollingAverageOf != null);
        Data.Kpis.Where(k => k.RollingAverageOf != null).Should().AllSatisfy(k => k.WindowKind.Should().Be(KpiVocabulary.PerPeriod));
    }

    [Fact]
    public void Generate_RecordsACumulativeOrRollingKpi_AsOfDistinctDays_FromItsAnchor()
    {
        // Arrange
        var windowed = Data.Kpis.Where(k => k.WindowKind is KpiVocabulary.Cumulative or KpiVocabulary.Rolling).ToList();

        // Act & Assert
        windowed.Should().NotBeEmpty();
        windowed.Should().AllSatisfy(k =>
        {
            var history = Data.Measurements.Where(m => m.KpiHandle == k.Handle).ToList();
            history.Should().AllSatisfy(m =>
            {
                m.PeriodStart.Should().BeNull();
                m.MeasuredAt.Should().BeNull();
                m.AsOf.Should().NotBeNull();
            });
            history.Select(m => m.AsOf).Should().OnlyHaveUniqueItems();

            if (k.WindowKind == KpiVocabulary.Cumulative)
            {
                k.WindowAnchorDate.Should().NotBeNull();
                history.Should().AllSatisfy(m => m.AsOf!.Value.Should().BeOnOrAfter(k.WindowAnchorDate!.Value));
            }
            else
            {
                k.WindowRollingCount.Should().BePositive();
                k.WindowRollingUnit.Should().Be("Days");
            }
        });
    }

    [Fact]
    public void Generate_TakesPointInTimeReadingsAtDistinctMoments_WithTheirOffset()
    {
        // Arrange
        var handles = Data.Kpis.Where(k => k.WindowKind == KpiVocabulary.PointInTime).Select(k => k.Handle).ToHashSet();

        // Act
        var readings = Data.Measurements.Where(m => handles.Contains(m.KpiHandle)).ToList();

        // Assert
        readings.Should().NotBeEmpty().And.AllSatisfy(m =>
        {
            m.PeriodStart.Should().BeNull();
            m.AsOf.Should().BeNull();
            m.MeasuredAt.Should().NotBeNull();
        });
        readings.GroupBy(m => m.KpiHandle).Should().AllSatisfy(g => g.Select(m => m.MeasuredAt).Should().OnlyHaveUniqueItems());
    }

    [Fact]
    public void Generate_TakesTheBaselineOverTheFirstThreeMonths()
    {
        // Arrange & Act
        var kpi = Data.Kpis.First(k => k.WindowKind == KpiVocabulary.PerPeriod);
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
        var lastDay = Data.Measurements.Max(m => m.LastDay);

        // Act
        var closed = Data.Kpis.Where(k => k.IsClosed).ToList();

        // Assert — a dropped KPI's history stops before the last month the open ones reach
        closed.Should().NotBeEmpty();
        closed.Count.Should().BeLessThan(Data.Kpis.Count / 3);
        closed.Should().AllSatisfy(k =>
        {
            var history = Data.Measurements.Where(m => m.KpiHandle == k.Handle).ToList();
            history.Should().HaveCountGreaterThanOrEqualTo(3);
            history.Max(m => m.LastDay).Should().BeBefore(lastDay);
        });
    }

    [Fact]
    public void Generate_IsTheSameForTheSameSeed()
    {
        // Arrange & Act
        var again = Generate().Data;

        // Assert
        again.Kpis.Should().Equal(Data.Kpis);
        again.Measurements.Should().Equal(Data.Measurements);
        again.Checkpoints.Should().Equal(Data.Checkpoints);
    }

    [Fact]
    public void Generate_PlansSomeKpis_WithQuarterlyCheckpointsTheAggregateCanHold()
    {
        // Arrange
        var byHandle = Data.Kpis.ToDictionary(k => k.Handle);

        // Act
        var plans = Data.Checkpoints.GroupBy(c => c.KpiHandle).ToList();

        // Assert
        plans.Should().NotBeEmpty();
        plans.Count.Should().BeLessThan(Data.Kpis.Count);
        plans.Should().AllSatisfy(plan =>
        {
            var kpi = byHandle[plan.Key];
            plan.Select(c => c.Date).Should().OnlyHaveUniqueItems();
            plan.Should().AllSatisfy(c =>
            {
                c.Date.AddDays(1).Day.Should().Be(1);
                (c.Date.Month % 3).Should().Be(0);
                c.Date.Should().BeAfter(kpi.BaselineWindowEnd);
                c.Label.Should().Be($"Q{c.Date.Month / 3} {c.Date.Year}");
                // A KPI with a threshold may still get none at a checkpoint that sits on its range's edge.
                if (kpi.AtRiskKind is null)
                    c.AtRiskValue.Should().BeNull();
                else if (c.AtRiskValue is null)
                    return;
                else if (kpi.Direction == "Increase")
                    c.AtRiskValue.Should().BeLessThan(c.PlannedValue).And.BeGreaterThanOrEqualTo(0);
                else
                    c.AtRiskValue.Should().BeGreaterThan(c.PlannedValue);
            });
            // The last checkpoint is the target, rounded to the KPI's precision.
            plan.OrderBy(c => c.Date).Last().PlannedValue.Should().BeApproximately(Resolved(kpi), 0.5);
        });
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
        var few = Generate(new KpiOptions { KpisPerTeam = 1 }).Data.Kpis.Count(k => k.Subject == KpiSubject.Team);
        var many = Generate(new KpiOptions { KpisPerTeam = 5 }).Data.Kpis.Count(k => k.Subject == KpiSubject.Team);

        // Assert
        many.Should().BeGreaterThan(few * 3);
    }

    [Fact]
    public void Generate_LeavesTheTeamsKpisUnchanged_WhetherOrNotPpmWasGenerated()
    {
        // Arrange & Act
        var without = Generate(withPpm: false).Data;

        // Assert
        Data.Kpis.Where(k => k.Subject == KpiSubject.Team).Should().Equal(without.Kpis);
        var teamHandles = without.Kpis.Select(k => k.Handle).ToHashSet();
        Data.Measurements.Where(m => teamHandles.Contains(m.KpiHandle)).Should().Equal(without.Measurements);
        Data.Checkpoints.Where(c => teamHandles.Contains(c.KpiHandle)).Should().Equal(without.Checkpoints);
        without.Categories.Should().Equal(KpiVocabulary.Categories);
    }

    [Fact]
    public void Generate_GivesEveryLiveProjectProgramAndPortfolioItsKpis_AndNothingElse()
    {
        // Arrange
        var ppm = _default.Ppm!;
        bool Live(string status) => status is "Active" or "Completed";
        var expected = ppm.Portfolios.Where(p => p.ActivatedOn is not null && p.Status == "Active").Select(p => (KpiSubject.Portfolio, p.Name))
            .Concat(ppm.Programs.Where(p => p.ActivatedOn is not null && Live(p.Status)).Select(p => (KpiSubject.Program, p.Name)))
            .Concat(ppm.Projects.Where(p => p.ActivatedOn is not null && Live(p.Status)).Select(p => (KpiSubject.Project, p.Key)))
            .ToHashSet();

        // Act
        var bySubject = Data.Kpis
            .Where(k => k.Subject != KpiSubject.Team)
            .GroupBy(k => (k.Subject, k.SubjectHandle))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Assert
        bySubject.Should().NotBeEmpty();
        bySubject.Keys.Should().Contain(k => k.Item1 == KpiSubject.Project);
        bySubject.Keys.Should().Contain(k => k.Item1 == KpiSubject.Program);
        bySubject.Keys.Should().Contain(k => k.Item1 == KpiSubject.Portfolio);
        bySubject.Keys.Should().BeSubsetOf(expected);
        bySubject.Values.Should().AllSatisfy(kpis =>
        {
            kpis.Should().HaveCount(KpiGenerator.KpisPerPpmRecord);
            kpis.Select(k => k.Name).Should().OnlyHaveUniqueItems();
        });
        Data.Categories.Should().Equal([.. KpiVocabulary.Categories, .. KpiVocabulary.PpmCategories]);
    }

    [Fact]
    public void Generate_MeasuresAProjectOnlyWhileItWasLive()
    {
        // Arrange
        var projects = _default.Ppm!.Projects.ToDictionary(p => p.Key);
        var kpis = Data.Kpis.Where(k => k.Subject == KpiSubject.Project).ToDictionary(k => k.Handle);

        // Act
        var measured = Data.Measurements
            .Where(m => kpis.ContainsKey(m.KpiHandle))
            .Select(m => (Project: projects[kpis[m.KpiHandle].SubjectHandle], Measurement: m))
            .ToList();

        // Assert
        measured.Should().NotBeEmpty();
        measured.Should().AllSatisfy(x =>
        {
            x.Measurement.LastDay.Should().BeAfter(x.Project.ActivatedOn!.Value);
            if (x.Project.Status == "Completed" && (x.Project.ClosedOn ?? x.Project.End) is { } closed)
                x.Measurement.LastDay.Should().BeOnOrBefore(new DateOnly(closed.Year, closed.Month, 1).AddMonths(1).AddDays(-1));
        });
    }
}
