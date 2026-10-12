using NodaTime;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Common.Domain.Settings;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Models.SprintMetrics;
using Wayd.Work.Domain.Models.SprintScope;
using Wayd.Work.Domain.Tests.Data;

namespace Wayd.Work.Domain.Tests.Sut.Models.SprintMetrics;

public class TeamSprintMetricsTests
{
    private static readonly SprintSchedule UtcSchedule = new(DateTimeZone.Utc, CommitmentGraceDays: 1, SizingMethod.StoryPoints);
    private static readonly ResolvedSprintType Standard = new(SprintType.Standard, SprintTypeSource.Default);
    private static readonly ResolvedSprintType NonStandard = new(SprintType.NonStandard, SprintTypeSource.Team);

    // Sprint 1 starts Monday 5 January 2026; each sprint is two weeks, back to back.
    private static readonly LocalDate FirstStart = new(2026, 1, 5);

    private readonly Guid _teamId = Guid.NewGuid();

    private Iteration NewSprint(int number) =>
        new IterationFaker()
            .AsSprint()
            .WithKey(number)
            .WithTeamId(_teamId)
            .WithDateRange(new IterationDateRange(FirstStart.PlusDays((number - 1) * 14), FirstStart.PlusDays(number * 14 - 1)))
            .Generate();

    private TeamSprintTimeline Timeline(int sprints) =>
        new(_teamId, Enumerable.Range(1, sprints).Select(NewSprint), new TeamSprintSchedules([], UtcSchedule));

    private static Instant StartOf(int number) =>
        FirstStart.PlusDays((number - 1) * 14).AtMidnight().InUtc().ToInstant();

    /// <summary>A sprint's scope of three-point items: so many committed, so many of those completed, and so many added and completed.</summary>
    private static SprintScopeTotals Totals(int committed, int completedOfCommitted, int addedCompleted = 0)
    {
        SprintScopeItem Item(SprintScopeEntry entry, SprintScopeOutcome outcome) => new(Guid.NewGuid(), entry, null, outcome, null, 3, 3);

        return SprintScopeTotals.Of(
        [
            .. Enumerable.Range(0, completedOfCommitted).Select(_ => Item(SprintScopeEntry.Committed, SprintScopeOutcome.Completed)),
            .. Enumerable.Range(0, committed - completedOfCommitted).Select(_ => Item(SprintScopeEntry.Committed, SprintScopeOutcome.CarriedOver)),
            .. Enumerable.Range(0, addedCompleted).Select(_ => Item(SprintScopeEntry.Added, SprintScopeOutcome.Completed)),
        ]);
    }

    [Fact]
    public void Candidates_AreTheCompletedSprintsMostRecentFirst()
    {
        // Arrange — read during sprint 5, so sprints 1 to 4 have ended
        var timeline = Timeline(6);
        var sut = new TeamSprintMetrics(3, SizingMethod.StoryPoints);

        // Act
        var candidates = sut.Candidates(timeline, StartOf(5).Plus(Duration.FromDays(2)));

        // Assert
        candidates.Select(s => s.Key).Should().Equal(4, 3, 2, 1);
    }

    [Fact]
    public void Candidates_LongHistory_LooksBackNoFurtherThanThreeTimesTheWindow()
    {
        // Arrange
        var timeline = Timeline(20);
        var sut = new TeamSprintMetrics(3, SizingMethod.StoryPoints);

        // Act
        var candidates = sut.Candidates(timeline, StartOf(21));

        // Assert
        candidates.Should().HaveCount(3 * TeamSprintMetrics.LookbackFactor);
        candidates[0].Key.Should().Be(20);
    }

    [Theory]
    [InlineData(1, SprintMetricsSettings.MinSprintWindow)]
    [InlineData(40, SprintMetricsSettings.MaxSprintWindow)]
    [InlineData(7, 7)]
    public void Constructor_HoldsTheSizeToTheAllowedWindow(int requested, int expected)
    {
        // Act
        var sut = new TeamSprintMetrics(requested, SizingMethod.StoryPoints);

        // Assert
        sut.Size.Should().Be(expected);
    }

    [Fact]
    public void Admit_NonStandardSprint_IsExcludedAndNotAdmitted()
    {
        // Arrange
        var sprint = NewSprint(1);
        var sut = new TeamSprintMetrics(3, SizingMethod.StoryPoints);

        // Act
        var admitted = sut.Admit(sprint, NonStandard);

        // Assert
        admitted.Should().BeFalse();
        sut.Excluded.Should().ContainSingle().Which.Should().Be(new ExcludedSprint(sprint, SprintMetricsExclusion.NonStandard));
        sut.Sprints.Should().BeEmpty();
    }

    [Fact]
    public void Admit_StandardSprint_IsAdmittedAndNotYetCounted()
    {
        // Arrange
        var sut = new TeamSprintMetrics(3, SizingMethod.StoryPoints);

        // Act
        var admitted = sut.Admit(NewSprint(1), Standard);

        // Assert
        admitted.Should().BeTrue();
        sut.Excluded.Should().BeEmpty();
        sut.Sprints.Should().BeEmpty();
    }

    [Fact]
    public void Assess_HistoryIncomplete_IsExcludedWhateverItsTotals()
    {
        // Arrange
        var sprint = NewSprint(1);
        var sut = new TeamSprintMetrics(3, SizingMethod.StoryPoints);

        // Act
        sut.Assess(sprint, SizingMethod.StoryPoints, historyIncomplete: true, Totals(committed: 4, completedOfCommitted: 4));

        // Assert
        sut.Excluded.Should().ContainSingle().Which.Reason.Should().Be(SprintMetricsExclusion.HistoryIncomplete);
        sut.Sprints.Should().BeEmpty();
    }

    [Fact]
    public void Assess_NothingCommitted_IsExcluded()
    {
        // Arrange
        var sprint = NewSprint(1);
        var sut = new TeamSprintMetrics(3, SizingMethod.StoryPoints);

        // Act
        sut.Assess(sprint, SizingMethod.StoryPoints, historyIncomplete: false, Totals(committed: 0, completedOfCommitted: 0, addedCompleted: 2));

        // Assert
        sut.Excluded.Should().ContainSingle().Which.Reason.Should().Be(SprintMetricsExclusion.NoCommitment);
        sut.Sprints.Should().BeEmpty();
    }

    [Fact]
    public void Assess_UntilFull_CountsTheWindowAndNoMore()
    {
        // Arrange
        var sut = new TeamSprintMetrics(3, SizingMethod.StoryPoints);

        // Act
        foreach (var number in new[] { 5, 4, 3 })
            sut.Assess(NewSprint(number), SizingMethod.StoryPoints, false, Totals(4, 2));

        // Assert
        sut.IsFull.Should().BeTrue();
        sut.Sprints.Select(s => s.Sprint.Key).Should().Equal(5, 4, 3);
        sut.Invoking(s => s.Assess(NewSprint(2), SizingMethod.StoryPoints, false, Totals(4, 2)))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ByCount_AveragesEachSprintsValueAndGivesItsRange()
    {
        // Arrange — velocities 4, 2 and 3 items; say/do 100%, 50% and 50%; the last made up to 75% predictability by added work
        var sut = new TeamSprintMetrics(3, SizingMethod.StoryPoints);
        sut.Assess(NewSprint(3), SizingMethod.StoryPoints, false, Totals(committed: 4, completedOfCommitted: 4));
        sut.Assess(NewSprint(2), SizingMethod.StoryPoints, false, Totals(committed: 4, completedOfCommitted: 2));
        sut.Assess(NewSprint(1), SizingMethod.StoryPoints, false, Totals(committed: 4, completedOfCommitted: 2, addedCompleted: 1));

        // Act
        var summaries = sut.ByCount;

        // Assert
        summaries.Velocity!.Average.Should().Be(3);
        summaries.Velocity.Min.Should().Be(2);
        summaries.Velocity.Max.Should().Be(4);
        summaries.SayDo!.Average.Should().BeApproximately(2.0 / 3, 1e-9);
        summaries.Predictability!.Average.Should().Be(0.75);
        summaries.Predictability.Min.Should().Be(0.5);
        summaries.Predictability.Max.Should().Be(1);
    }

    [Fact]
    public void ByEstimate_SumsEstimatesNotItems()
    {
        // Arrange
        var sut = new TeamSprintMetrics(3, SizingMethod.StoryPoints);
        sut.Assess(NewSprint(2), SizingMethod.StoryPoints, false, Totals(committed: 4, completedOfCommitted: 4));
        sut.Assess(NewSprint(1), SizingMethod.StoryPoints, false, Totals(committed: 4, completedOfCommitted: 2));

        // Act
        var summaries = sut.ByEstimate;

        // Assert
        summaries.Velocity!.Average.Should().Be(9);
        summaries.Velocity.Min.Should().Be(6);
        summaries.Velocity.Max.Should().Be(12);
    }

    [Fact]
    public void ByEstimate_SizingMethodChangedInTheWindow_LeavesOutSprintsInTheOldEstimate()
    {
        // Arrange — the team moved from Effort to Story Points after sprint 1
        var sut = new TeamSprintMetrics(3, SizingMethod.StoryPoints);
        sut.Assess(NewSprint(3), SizingMethod.StoryPoints, false, Totals(committed: 4, completedOfCommitted: 4));
        sut.Assess(NewSprint(2), SizingMethod.StoryPoints, false, Totals(committed: 4, completedOfCommitted: 2));
        sut.Assess(NewSprint(1), SizingMethod.Effort, false, Totals(committed: 10, completedOfCommitted: 10));

        // Act
        var byEstimate = sut.ByEstimate;
        var byCount = sut.ByCount;

        // Assert
        sut.SizingMethod.Should().Be(SizingMethod.StoryPoints);
        sut.IsInReportEstimate(sut.Sprints[2]).Should().BeFalse();
        byEstimate.Velocity!.Sprints.Should().Be(2);
        byEstimate.Velocity.Average.Should().Be(9);
        byCount.Velocity!.Sprints.Should().Be(3);
    }

    [Fact]
    public void Summaries_NoSprintCounted_AreEmptyInTheTeamsSizingMethod()
    {
        // Arrange
        var sut = new TeamSprintMetrics(3, SizingMethod.StoryPoints);

        // Act
        var summaries = sut.ByCount;

        // Assert
        sut.SizingMethod.Should().Be(SizingMethod.StoryPoints);
        summaries.Velocity.Should().BeNull();
        summaries.Predictability.Should().BeNull();
        summaries.SayDo.Should().BeNull();
    }

    [Fact]
    public void SizingMethod_MostRecentCountedSprintInAnOlderEstimate_IsStillTheTeamsNow()
    {
        // Arrange — the team moved to Story Points, and its only sprint since was left out
        var sut = new TeamSprintMetrics(3, SizingMethod.StoryPoints);
        sut.Assess(NewSprint(2), SizingMethod.StoryPoints, false, Totals(committed: 0, completedOfCommitted: 0));
        sut.Assess(NewSprint(1), SizingMethod.Effort, false, Totals(committed: 4, completedOfCommitted: 4));

        // Act
        var byEstimate = sut.ByEstimate;

        // Assert
        sut.SizingMethod.Should().Be(SizingMethod.StoryPoints);
        sut.IsInReportEstimate(sut.Sprints[0]).Should().BeFalse();
        byEstimate.Velocity.Should().BeNull();
        sut.ByCount.Velocity!.Sprints.Should().Be(1);
    }

    [Fact]
    public void ByEstimate_TeamSizesByCount_IsByCountAcrossEverySprint()
    {
        // Arrange — the team moved from Story Points to counting work items
        var sut = new TeamSprintMetrics(3, SizingMethod.Count);
        sut.Assess(NewSprint(2), SizingMethod.Count, false, Totals(committed: 4, completedOfCommitted: 4));
        sut.Assess(NewSprint(1), SizingMethod.StoryPoints, false, Totals(committed: 4, completedOfCommitted: 2));

        // Act
        var byEstimate = sut.ByEstimate;

        // Assert
        byEstimate.Should().Be(sut.ByCount);
        byEstimate.Velocity!.Sprints.Should().Be(2);
        byEstimate.Velocity.Average.Should().Be(3);
    }
}
