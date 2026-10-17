using NodaTime;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Work.Domain.Models.CadenceMetrics;

namespace Wayd.Work.Domain.Tests.Sut.Models.CadenceMetrics;

public class TeamCadenceMetricsTests
{
    private static readonly LocalDate Monday = new(2026, 1, 5);

    private static CadencePeriod Period(int index, SizingMethod sizingMethod = SizingMethod.StoryPoints, bool isPartial = false, bool isCurrent = false)
    {
        var start = Monday.PlusDays(index * 14);
        var end = start.PlusDays(13);
        return new CadencePeriod(start, end, sizingMethod, isPartial, isCurrent,
            start.AtStartOfDayInZone(DateTimeZone.Utc).ToInstant(),
            end.PlusDays(1).AtStartOfDayInZone(DateTimeZone.Utc).ToInstant());
    }

    private static CompletedWorkItem DoneIn(CadencePeriod period, double? storyPoints, double? effort = null) =>
        new(period.From.Plus(Duration.FromHours(30)), storyPoints, effort, null);

    [Fact]
    public void Measure_CountsAndSumsEachPeriodsCompletedWork()
    {
        // Arrange
        var first = Period(0);
        var second = Period(1);
        CompletedWorkItem[] completions = [DoneIn(first, 3), DoneIn(first, 5), DoneIn(first, null), DoneIn(second, 8)];

        // Act
        var metrics = TeamCadenceMetrics.Measure(SizingMethod.StoryPoints, [second, first], completions);

        // Assert
        metrics.Periods[1].CompletedCount.Should().Be(3);
        metrics.Periods[1].CompletedEstimate.Should().Be(8);
        metrics.Periods[1].UnestimatedCount.Should().Be(1);
        metrics.Periods[0].CompletedCount.Should().Be(1);
        metrics.ByCount!.Average.Should().Be(2);
        metrics.ByEstimate!.Average.Should().Be(8);
    }

    [Fact]
    public void Measure_LeavesTheCurrentAndPartialPeriodsOutOfTheAverages()
    {
        // Arrange
        var current = Period(2, isCurrent: true);
        var ended = Period(1);
        var partial = Period(0, isPartial: true);
        CompletedWorkItem[] completions = [DoneIn(current, 1), DoneIn(ended, 2), DoneIn(ended, 2), DoneIn(partial, 1)];

        // Act
        var metrics = TeamCadenceMetrics.Measure(SizingMethod.StoryPoints, [current, ended, partial], completions);

        // Assert
        metrics.Periods.Select(p => p.InAverages).Should().Equal(false, true, false);
        metrics.ByCount!.Average.Should().Be(2);
        metrics.ByCount.Sprints.Should().Be(1);
    }

    [Fact]
    public void Measure_NeverAveragesEstimatesFromAnotherSizingMethod()
    {
        // Arrange
        var inPoints = Period(1);
        var inEffort = Period(0, SizingMethod.Effort);
        CompletedWorkItem[] completions = [DoneIn(inPoints, 5), DoneIn(inEffort, 1, effort: 40)];

        // Act
        var metrics = TeamCadenceMetrics.Measure(SizingMethod.StoryPoints, [inPoints, inEffort], completions);

        // Assert
        metrics.Periods[1].CompletedEstimate.Should().Be(40);
        metrics.Periods[1].InEstimateAverages.Should().BeFalse();
        metrics.ByEstimate!.Average.Should().Be(5);
        metrics.ByCount!.Sprints.Should().Be(2);
    }

    [Fact]
    public void Measure_UnderCount_MeasuresTheEstimateByCountAcrossEveryPeriod()
    {
        // Arrange
        var inCount = Period(1, SizingMethod.Count);
        var inPoints = Period(0);
        CompletedWorkItem[] completions = [DoneIn(inCount, null), DoneIn(inPoints, 3), DoneIn(inPoints, 3)];

        // Act
        var metrics = TeamCadenceMetrics.Measure(SizingMethod.Count, [inCount, inPoints], completions);

        // Assert
        metrics.ByEstimate.Should().Be(metrics.ByCount);
        metrics.ByEstimate!.Average.Should().Be(1.5);
    }

    [Fact]
    public void Measure_WithNoPeriods_HasNoAverages()
    {
        // Act
        var metrics = TeamCadenceMetrics.Measure(SizingMethod.Count, [], []);

        // Assert
        metrics.Periods.Should().BeEmpty();
        metrics.ByCount.Should().BeNull();
        metrics.ByEstimate.Should().BeNull();
    }
}
