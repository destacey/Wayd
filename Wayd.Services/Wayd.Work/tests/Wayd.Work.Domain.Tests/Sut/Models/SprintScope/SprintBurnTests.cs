using NodaTime;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Models.SprintScope;
using Wayd.Work.Domain.Tests.Data;

namespace Wayd.Work.Domain.Tests.Sut.Models.SprintScope;

public class SprintBurnTests
{
    private static readonly DateTimeZone Chicago = DateTimeZoneProviders.Tzdb["America/Chicago"];
    private static readonly SprintSchedule ChicagoSchedule = new(Chicago, CommitmentGraceDays: 1, SizingMethod.StoryPoints);

    // Sprint 1 is planned Monday 14 September to Friday 25 September; sprint 2 starts Monday 28 September.
    // Its commitment point is the end of Monday 14th, and it ends at the end of Friday 25th.
    private static readonly LocalDate Sprint1Start = new(2026, 9, 14);
    private static readonly LocalDate Sprint1LastDay = new(2026, 9, 25);
    private static readonly LocalDate Sprint2Start = new(2026, 9, 28);

    private static readonly Instant Later = At(new LocalDate(2027, 1, 4));

    private readonly Guid _teamId = Guid.NewGuid();

    private Iteration NewSprint(LocalDate start, LocalDate end, int key) =>
        new IterationFaker()
            .AsSprint()
            .WithKey(key)
            .WithTeamId(_teamId)
            .WithDateRange(new IterationDateRange(start, end))
            .Generate();

    private (Iteration Sprint1, Iteration Sprint2, SprintScopeWindow Window) TwoSprints()
    {
        var sprint1 = NewSprint(Sprint1Start, Sprint1LastDay, 1);
        var sprint2 = NewSprint(Sprint2Start, Sprint2Start.PlusDays(11), 2);
        var timeline = new TeamSprintTimeline(_teamId, [sprint1, sprint2],
            new TeamSprintSchedules([new SprintSchedulePeriod(new LocalDate(2026, 1, 1), null, ChicagoSchedule)], ChicagoSchedule));
        return (sprint1, sprint2, SprintScopeWindow.For(timeline, sprint1));
    }

    private static Instant At(LocalDate date, int hour = 0) =>
        date.At(new LocalTime(hour, 0)).InZoneLeniently(Chicago).ToInstant();

    private static SprintBurn Build(SprintScopeWindow window, Instant now, params ScopeItemHistory[] items) =>
        SprintBurn.Build(window, SizingMethod.StoryPoints, items.SelectMany(i => i.Periods), now);

    private static SprintBurnPoint On(SprintBurn burn, LocalDate day) => burn.Points.Last(p => p.Day == day);

    [Fact]
    public void Build_EndedSprint_ReadsAtTheCommitmentPointEachDayAndTheEnd()
    {
        // Arrange
        var (_, _, window) = TwoSprints();

        // Act
        var burn = Build(window, Later);

        // Assert — the commitment point at the end of the 14th, then the end of the 15th to the 25th
        burn.Points.Select(p => p.At).Should().Equal(
            new[] { window.Start }.Concat(Enumerable.Range(1, 11).Select(d => At(Sprint1Start.PlusDays(d + 1)))));
        burn.Points.Select(p => p.Day).Should().Equal(
            new[] { Sprint1Start.PlusDays(1) }.Concat(Enumerable.Range(1, 11).Select(d => Sprint1Start.PlusDays(d))));
    }

    [Fact]
    public void Build_RunningSprint_EndsWithAReadingNow()
    {
        // Arrange
        var (_, _, window) = TwoSprints();
        var now = At(Sprint1Start.PlusDays(4), 15);

        // Act
        var burn = Build(window, now);

        // Assert
        burn.Points[^1].At.Should().Be(now);
        burn.Points[^1].Day.Should().Be(Sprint1Start.PlusDays(4));
        burn.Points.Should().HaveCount(5);
    }

    [Fact]
    public void Build_BeforeTheCommitmentPoint_HasNoReadings()
    {
        // Arrange
        var (_, _, window) = TwoSprints();

        // Act
        var burn = Build(window, At(Sprint1Start, 10));

        // Assert
        burn.Points.Should().BeEmpty();
    }

    [Fact]
    public void Build_AgreesWithTheScopeReportAtTheStartAndTheEnd()
    {
        // Arrange — committed and done, committed and descoped, added and carried over, re-estimated, and done then moved out
        var (sprint1, sprint2, window) = TwoSprints();
        ScopeItemHistory[] items =
        [
            new ScopeItemHistory()
                .Then(At(Sprint1Start.PlusDays(-1)), sprint1.Id, WorkStatusCategory.Active, storyPoints: 5)
                .Then(At(Sprint1Start.PlusDays(3)), sprint1.Id, WorkStatusCategory.Done, storyPoints: 5),
            new ScopeItemHistory()
                .Then(At(Sprint1Start.PlusDays(-1)), sprint1.Id, WorkStatusCategory.Active, storyPoints: 3)
                .Then(At(Sprint1Start.PlusDays(4)), null, WorkStatusCategory.Active, storyPoints: 3),
            new ScopeItemHistory()
                .Then(At(Sprint1Start.PlusDays(2)), sprint1.Id, WorkStatusCategory.Active, storyPoints: 2)
                .Then(At(Sprint1LastDay, 16), sprint2.Id, WorkStatusCategory.Active, storyPoints: 2),
            new ScopeItemHistory()
                .Then(At(Sprint1Start.PlusDays(-1)), sprint1.Id, WorkStatusCategory.Active, storyPoints: 1)
                .Then(At(Sprint1Start.PlusDays(5)), sprint1.Id, WorkStatusCategory.Active, storyPoints: 8),
            new ScopeItemHistory()
                .Then(At(Sprint1Start.PlusDays(-1)), sprint1.Id, WorkStatusCategory.Active, storyPoints: 2)
                .Then(At(Sprint1Start.PlusDays(6)), sprint1.Id, WorkStatusCategory.Done, storyPoints: 2)
                .Then(At(Sprint1Start.PlusDays(7)), null, WorkStatusCategory.Done, storyPoints: 2),
        ];
        var totals = SprintScopeReport.Build(window, SizingMethod.StoryPoints, items.SelectMany(i => i.Periods), Later).Totals;

        // Act
        var burn = Build(window, Later, items);

        // Assert
        burn.Committed.Should().Be(totals.Committed);
        burn.Points[0].Scope.Should().Be(totals.Committed);
        var end = burn.Points[^1];
        end.Completed.Should().Be(totals.Completed);
        end.Scope.Should().Be(new SprintScopeMeasure(
            totals.Total.Count - totals.Descoped.Count,
            totals.Total.Estimate - totals.Descoped.Estimate));
    }

    [Fact]
    public void Build_AddedWork_StepsScopeUpOnTheDayItIsAdded()
    {
        // Arrange
        var (sprint1, _, window) = TwoSprints();
        var committed = new ScopeItemHistory()
            .Then(At(Sprint1Start.PlusDays(-1)), sprint1.Id, WorkStatusCategory.Active, storyPoints: 5);
        var added = new ScopeItemHistory()
            .Then(At(Sprint1Start.PlusDays(3), 10), sprint1.Id, WorkStatusCategory.Proposed, storyPoints: 3);

        // Act
        var burn = Build(window, Later, committed, added);

        // Assert
        On(burn, Sprint1Start.PlusDays(2)).Scope.Should().Be(new SprintScopeMeasure(1, 5));
        On(burn, Sprint1Start.PlusDays(3)).Scope.Should().Be(new SprintScopeMeasure(2, 8));
    }

    [Fact]
    public void Build_DescopedWork_StepsScopeDownOnTheDayItLeaves()
    {
        // Arrange
        var (sprint1, _, window) = TwoSprints();
        var kept = new ScopeItemHistory()
            .Then(At(Sprint1Start.PlusDays(-1)), sprint1.Id, WorkStatusCategory.Active, storyPoints: 5);
        var descoped = new ScopeItemHistory()
            .Then(At(Sprint1Start.PlusDays(-1)), sprint1.Id, WorkStatusCategory.Active, storyPoints: 3)
            .Then(At(Sprint1Start.PlusDays(4), 11), null, WorkStatusCategory.Active, storyPoints: 3);

        // Act
        var burn = Build(window, Later, kept, descoped);

        // Assert
        On(burn, Sprint1Start.PlusDays(3)).Scope.Estimate.Should().Be(8);
        On(burn, Sprint1Start.PlusDays(4)).Scope.Estimate.Should().Be(5);
    }

    [Fact]
    public void Build_ReopenedWork_CountsAsRemainingAgain()
    {
        // Arrange
        var (sprint1, _, window) = TwoSprints();
        var item = new ScopeItemHistory()
            .Then(At(Sprint1Start.PlusDays(-1)), sprint1.Id, WorkStatusCategory.Active, storyPoints: 5)
            .Then(At(Sprint1Start.PlusDays(2), 12), sprint1.Id, WorkStatusCategory.Done, storyPoints: 5)
            .Then(At(Sprint1Start.PlusDays(4), 12), sprint1.Id, WorkStatusCategory.Active, storyPoints: 5);

        // Act
        var burn = Build(window, Later, item);

        // Assert
        On(burn, Sprint1Start.PlusDays(3)).Completed.Estimate.Should().Be(5);
        On(burn, Sprint1Start.PlusDays(4)).Completed.Estimate.Should().Be(0);
        On(burn, Sprint1Start.PlusDays(4)).Scope.Estimate.Should().Be(5);
    }

    [Fact]
    public void Build_WorkCompletedThenMovedOut_StaysCompleted()
    {
        // Arrange
        var (sprint1, _, window) = TwoSprints();
        var item = new ScopeItemHistory()
            .Then(At(Sprint1Start.PlusDays(-1)), sprint1.Id, WorkStatusCategory.Active, storyPoints: 5)
            .Then(At(Sprint1Start.PlusDays(2), 12), sprint1.Id, WorkStatusCategory.Done, storyPoints: 5)
            .Then(At(Sprint1Start.PlusDays(4), 12), null, WorkStatusCategory.Done, storyPoints: 5);

        // Act
        var burn = Build(window, Later, item);

        // Assert
        burn.Points[^1].Scope.Should().Be(new SprintScopeMeasure(1, 5));
        burn.Points[^1].Completed.Should().Be(new SprintScopeMeasure(1, 5));
    }

    [Fact]
    public void Build_ReestimatedWork_ChangesScopeOnTheDayItIsReestimated()
    {
        // Arrange
        var (sprint1, _, window) = TwoSprints();
        var item = new ScopeItemHistory()
            .Then(At(Sprint1Start.PlusDays(-1)), sprint1.Id, WorkStatusCategory.Active, storyPoints: 3)
            .Then(At(Sprint1Start.PlusDays(5), 9), sprint1.Id, WorkStatusCategory.Active, storyPoints: 8);

        // Act
        var burn = Build(window, Later, item);

        // Assert
        On(burn, Sprint1Start.PlusDays(4)).Scope.Estimate.Should().Be(3);
        On(burn, Sprint1Start.PlusDays(5)).Scope.Estimate.Should().Be(8);
        burn.Committed.Estimate.Should().Be(3);
    }
}
