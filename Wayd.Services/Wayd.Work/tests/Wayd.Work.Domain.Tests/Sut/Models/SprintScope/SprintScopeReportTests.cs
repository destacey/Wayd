using NodaTime;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Models.SprintScope;
using Wayd.Work.Domain.Tests.Data;

namespace Wayd.Work.Domain.Tests.Sut.Models.SprintScope;

public class SprintScopeReportTests
{
    private static readonly DateTimeZone Chicago = DateTimeZoneProviders.Tzdb["America/Chicago"];
    private static readonly SprintSchedule ChicagoSchedule = new(Chicago, CommitmentGraceDays: 1, SizingMethod.StoryPoints);

    // Sprint 1 is planned Monday 14 September to Friday 25 September; sprint 2 starts Monday 28 September.
    private static readonly LocalDate Sprint1Start = new(2026, 9, 14);
    private static readonly LocalDate Sprint1LastDay = new(2026, 9, 25);
    private static readonly LocalDate Sprint2Start = new(2026, 9, 28);

    private readonly Guid _teamId = Guid.NewGuid();

    private Iteration NewSprint(LocalDate start, LocalDate end, int key, Instant? started = null, Instant? completed = null) =>
        new IterationFaker()
            .AsSprint()
            .WithKey(key)
            .WithTeamId(_teamId)
            .WithDateRange(new IterationDateRange(start, end))
            .WithStarted(started)
            .WithCompleted(completed)
            .Generate();

    private TeamSprintTimeline Timeline(params Iteration[] sprints) =>
        new(_teamId, sprints, new TeamSprintSchedules([new SprintSchedulePeriod(new LocalDate(2026, 1, 1), null, ChicagoSchedule)], ChicagoSchedule));

    private static Instant At(LocalDate date, int hour = 0) =>
        date.At(new LocalTime(hour, 0)).InZoneLeniently(Chicago).ToInstant();

    private (Iteration Sprint1, Iteration Sprint2, SprintScopeWindow Window) TwoSprints()
    {
        var sprint1 = NewSprint(Sprint1Start, Sprint1LastDay, 1);
        var sprint2 = NewSprint(Sprint2Start, Sprint2Start.PlusDays(11), 2);
        return (sprint1, sprint2, SprintScopeWindow.For(Timeline(sprint1, sprint2), sprint1));
    }

    private static SprintScopeReport Build(SprintScopeWindow window, params ItemHistory[] items) =>
        SprintScopeReport.Build(window, SizingMethod.StoryPoints, items.SelectMany(i => i.Periods));

    [Fact]
    public void Build_ItemInTheSprintAtTheStartAndDoneAtTheEnd_IsCommittedAndCompleted()
    {
        // Arrange
        var (sprint1, _, window) = TwoSprints();
        var item = new ItemHistory()
            .Then(At(Sprint1Start, 9), sprint1.Id, WorkStatusCategory.Proposed)
            .Then(At(Sprint1Start.PlusDays(4), 9), sprint1.Id, WorkStatusCategory.Done);

        // Act
        var report = Build(window, item);

        // Assert
        var result = report.Items.Should().ContainSingle().Subject;
        result.Entry.Should().Be(SprintScopeEntry.Committed);
        result.Outcome.Should().Be(SprintScopeOutcome.Completed);
        result.LeftAt.Should().BeNull();
        report.Totals.SayDoCount.Should().Be(1);
    }

    [Fact]
    public void Build_ItemAddedOnDay3_IsAdded()
    {
        // Arrange
        var (sprint1, _, window) = TwoSprints();
        var added = At(Sprint1Start.PlusDays(2), 10);
        var item = new ItemHistory()
            .Then(At(Sprint1Start.PlusDays(-7)), null, WorkStatusCategory.Proposed)
            .Then(added, sprint1.Id, WorkStatusCategory.Proposed);

        // Act
        var report = Build(window, item);

        // Assert
        var result = report.Items.Should().ContainSingle().Subject;
        result.Entry.Should().Be(SprintScopeEntry.Added);
        result.EnteredAt.Should().Be(added);
        result.Outcome.Should().Be(SprintScopeOutcome.CarriedOver);
        report.Totals.Added.Count.Should().Be(1);
        report.Totals.Committed.Count.Should().Be(0);
        report.Totals.SayDoCount.Should().BeNull();
    }

    [Fact]
    public void Build_ItemDescopedOnDay5_IsDescoped()
    {
        // Arrange
        var (sprint1, _, window) = TwoSprints();
        var left = At(Sprint1Start.PlusDays(4), 11);
        var item = new ItemHistory()
            .Then(At(Sprint1Start.PlusDays(-1)), sprint1.Id, WorkStatusCategory.Active)
            .Then(left, null, WorkStatusCategory.Active);

        // Act
        var report = Build(window, item);

        // Assert
        var result = report.Items.Should().ContainSingle().Subject;
        result.Entry.Should().Be(SprintScopeEntry.Committed);
        result.Outcome.Should().Be(SprintScopeOutcome.Descoped);
        result.LeftAt.Should().Be(left);
        report.Totals.SayDoCount.Should().Be(0);
    }

    [Fact]
    public void Build_UnfinishedItemsBulkMovedToTheNextSprintOnTheLastDay_AreCarriedOver()
    {
        // Arrange
        var (sprint1, sprint2, window) = TwoSprints();
        var moved = At(Sprint1LastDay, 16);
        var items = Enumerable.Range(0, 3)
            .Select(_ => new ItemHistory()
                .Then(At(Sprint1Start.PlusDays(-1)), sprint1.Id, WorkStatusCategory.Active)
                .Then(moved, sprint2.Id, WorkStatusCategory.Active))
            .ToArray();

        // Act
        var report = Build(window, items);

        // Assert
        report.Items.Should().HaveCount(3).And.OnlyContain(i => i.Outcome == SprintScopeOutcome.CarriedOver && i.LeftAt == moved);
        report.Totals.CarriedOver.Count.Should().Be(3);
    }

    [Fact]
    public void Build_UnfinishedItemMovedToTheNextSprintBeforeTheLastDay_IsDescoped()
    {
        // Arrange
        var (sprint1, sprint2, window) = TwoSprints();
        var item = new ItemHistory()
            .Then(At(Sprint1Start.PlusDays(-1)), sprint1.Id, WorkStatusCategory.Active)
            .Then(At(Sprint1LastDay.PlusDays(-1), 16), sprint2.Id, WorkStatusCategory.Active);

        // Act
        var report = Build(window, item);

        // Assert
        report.Items.Should().ContainSingle().Which.Outcome.Should().Be(SprintScopeOutcome.Descoped);
    }

    [Fact]
    public void Build_UnfinishedItemMovedToTheBacklogOnTheLastDay_IsDescoped()
    {
        // Arrange
        var (sprint1, _, window) = TwoSprints();
        var item = new ItemHistory()
            .Then(At(Sprint1Start.PlusDays(-1)), sprint1.Id, WorkStatusCategory.Active)
            .Then(At(Sprint1LastDay, 16), null, WorkStatusCategory.Active);

        // Act
        var report = Build(window, item);

        // Assert
        report.Items.Should().ContainSingle().Which.Outcome.Should().Be(SprintScopeOutcome.Descoped);
    }

    [Fact]
    public void Build_UnfinishedItemStillInTheSprintAtTheEnd_IsCarriedOver()
    {
        // Arrange
        var (sprint1, _, window) = TwoSprints();
        var item = new ItemHistory()
            .Then(At(Sprint1Start.PlusDays(-1)), sprint1.Id, WorkStatusCategory.Active);

        // Act
        var report = Build(window, item);

        // Assert
        report.Items.Should().ContainSingle().Which.Outcome.Should().Be(SprintScopeOutcome.CarriedOver);
    }

    [Fact]
    public void Build_ItemSetToRemovedMidSprint_IsCompletedAsRemoved()
    {
        // Arrange
        var (sprint1, _, window) = TwoSprints();
        var item = new ItemHistory()
            .Then(At(Sprint1Start.PlusDays(-1)), sprint1.Id, WorkStatusCategory.Proposed, storyPoints: 5)
            .Then(At(Sprint1Start.PlusDays(5), 10), sprint1.Id, WorkStatusCategory.Removed, storyPoints: 5);

        // Act
        var report = Build(window, item);

        // Assert
        var result = report.Items.Should().ContainSingle().Subject;
        result.Outcome.Should().Be(SprintScopeOutcome.Removed);
        result.IsCompleted.Should().BeTrue();
        report.Totals.Completed.Should().Be(new SprintScopeMeasure(1, 5));
        report.Totals.Removed.Should().Be(new SprintScopeMeasure(1, 5));
        report.Totals.SayDoEstimate.Should().Be(1);
    }

    [Fact]
    public void Build_ItemDeletedMidSprint_IsInNoCategory()
    {
        // Arrange — a deleted item's history is deleted with it, so only the item that remains has periods
        var (sprint1, _, window) = TwoSprints();
        var remaining = new ItemHistory()
            .Then(At(Sprint1Start.PlusDays(-1)), sprint1.Id, WorkStatusCategory.Active);

        // Act
        var report = Build(window, remaining);

        // Assert
        report.Items.Should().ContainSingle().Which.WorkItemId.Should().Be(remaining.WorkItemId);
        report.Totals.Total.Count.Should().Be(1);
    }

    [Fact]
    public void Build_TeamThatPlannedOnTheFridayBeforeAMondayHoliday_CarriesOverAndCommitsAtTheActualStart()
    {
        // Arrange — sprint 1 is planned through Sunday 27 September; Monday 28 is a holiday, so the team
        // moves its unfinished work and starts sprint 2 on Friday 25 at 15:00, which completes sprint 1
        var started = At(Sprint1LastDay, 15);
        var sprint1 = NewSprint(Sprint1Start, Sprint1LastDay.PlusDays(2), 1, started: At(Sprint1Start, 9), completed: started);
        var sprint2 = NewSprint(Sprint2Start, Sprint2Start.PlusDays(13), 2, started: started);
        var timeline = Timeline(sprint1, sprint2);

        var carried = new ItemHistory()
            .Then(At(Sprint1Start.PlusDays(-1)), sprint1.Id, WorkStatusCategory.Active)
            .Then(At(Sprint1LastDay, 14), sprint2.Id, WorkStatusCategory.Active);
        var addedAfterPlanning = new ItemHistory()
            .Then(At(Sprint2Start, 10), sprint2.Id, WorkStatusCategory.Proposed);

        // Act
        var sprint1Report = Build(SprintScopeWindow.For(timeline, sprint1), carried, addedAfterPlanning);
        var sprint2Report = Build(SprintScopeWindow.For(timeline, sprint2), carried, addedAfterPlanning);

        // Assert
        sprint1Report.Items.Should().ContainSingle().Which.Outcome.Should().Be(SprintScopeOutcome.CarriedOver);

        sprint2Report.Items.Should().HaveCount(2);
        sprint2Report.Items.Single(i => i.WorkItemId == carried.WorkItemId).Entry.Should().Be(SprintScopeEntry.Committed);
        sprint2Report.Items.Single(i => i.WorkItemId == addedAfterPlanning.WorkItemId).Entry.Should().Be(SprintScopeEntry.Added);
    }

    [Fact]
    public void Build_TypeChangedOutOfTheRequirementTierMidSprint_IsDescoped()
    {
        // Arrange
        var (sprint1, _, window) = TwoSprints();
        var item = new ItemHistory()
            .Then(At(Sprint1Start.PlusDays(-1)), sprint1.Id, WorkStatusCategory.Active)
            .Then(At(Sprint1Start.PlusDays(3), 10), sprint1.Id, WorkStatusCategory.Active, isRequirement: false);

        // Act
        var report = Build(window, item);

        // Assert
        report.Items.Should().ContainSingle().Which.Outcome.Should().Be(SprintScopeOutcome.Descoped);
    }

    [Fact]
    public void Build_TypeChangedIntoTheRequirementTierMidSprint_IsAdded()
    {
        // Arrange
        var (sprint1, _, window) = TwoSprints();
        var item = new ItemHistory()
            .Then(At(Sprint1Start.PlusDays(-1)), sprint1.Id, WorkStatusCategory.Active, isRequirement: false)
            .Then(At(Sprint1Start.PlusDays(3), 10), sprint1.Id, WorkStatusCategory.Active);

        // Act
        var report = Build(window, item);

        // Assert
        report.Items.Should().ContainSingle().Which.Entry.Should().Be(SprintScopeEntry.Added);
    }

    [Fact]
    public void Build_CommittedItemTakenOutAndPutBack_IsStillCommitted()
    {
        // Arrange
        var (sprint1, _, window) = TwoSprints();
        var item = new ItemHistory()
            .Then(At(Sprint1Start.PlusDays(-1)), sprint1.Id, WorkStatusCategory.Active)
            .Then(At(Sprint1Start.PlusDays(2)), null, WorkStatusCategory.Active)
            .Then(At(Sprint1Start.PlusDays(4)), sprint1.Id, WorkStatusCategory.Done);

        // Act
        var report = Build(window, item);

        // Assert
        var result = report.Items.Should().ContainSingle().Subject;
        result.Entry.Should().Be(SprintScopeEntry.Committed);
        result.Outcome.Should().Be(SprintScopeOutcome.Completed);
    }

    [Fact]
    public void Build_ItemOnlyInTheSprintOutsideTheWindow_IsExcluded()
    {
        // Arrange — taken out during the grace period, before the commitment point
        var (sprint1, _, window) = TwoSprints();
        var item = new ItemHistory()
            .Then(At(Sprint1Start.PlusDays(-1)), sprint1.Id, WorkStatusCategory.Active)
            .Then(At(Sprint1Start, 11), null, WorkStatusCategory.Active);

        // Act
        var report = Build(window, item);

        // Assert
        report.Items.Should().BeEmpty();
    }

    [Fact]
    public void Build_ItemReestimatedDuringTheSprint_SayDoUsesTheCommittedEstimate()
    {
        // Arrange
        var (sprint1, _, window) = TwoSprints();
        var reestimated = new ItemHistory()
            .Then(At(Sprint1Start.PlusDays(-1)), sprint1.Id, WorkStatusCategory.Active, storyPoints: 3)
            .Then(At(Sprint1Start.PlusDays(3)), sprint1.Id, WorkStatusCategory.Done, storyPoints: 8);
        var unfinished = new ItemHistory()
            .Then(At(Sprint1Start.PlusDays(-1)), sprint1.Id, WorkStatusCategory.Active, storyPoints: 3);

        // Act
        var report = Build(window, reestimated, unfinished);

        // Assert
        report.Totals.Committed.Should().Be(new SprintScopeMeasure(2, 6));
        report.Totals.CompletedOfCommitted.Should().Be(new SprintScopeMeasure(1, 3));
        report.Totals.Completed.Should().Be(new SprintScopeMeasure(1, 8));
        report.Totals.SayDoEstimate.Should().Be(0.5);
    }

    [Fact]
    public void Build_ItemWithNoEstimate_CountsAsUnestimated()
    {
        // Arrange
        var (sprint1, _, window) = TwoSprints();
        var item = new ItemHistory()
            .Then(At(Sprint1Start.PlusDays(-1)), sprint1.Id, WorkStatusCategory.Active, storyPoints: null);

        // Act
        var report = Build(window, item);

        // Assert
        report.Totals.Unestimated.Should().Be(1);
        report.Totals.Committed.Should().Be(new SprintScopeMeasure(1, 0));
        report.Totals.SayDoEstimate.Should().BeNull();
    }

    [Fact]
    public void Build_ZeroLengthPeriod_IsIgnored()
    {
        // Arrange — clock skew left a period that holds at no instant
        var (sprint1, _, window) = TwoSprints();
        var at = At(Sprint1Start.PlusDays(3), 10);
        var item = new ItemHistory()
            .Then(At(Sprint1Start.PlusDays(-1)), null, WorkStatusCategory.Active)
            .Then(at, sprint1.Id, WorkStatusCategory.Active)
            .Then(at, null, WorkStatusCategory.Active);

        // Act
        var report = Build(window, item);

        // Assert
        report.Items.Should().BeEmpty();
    }

    /// <summary>One work item's contiguous history, each state holding until the next begins.</summary>
    private sealed class ItemHistory
    {
        private readonly List<SprintScopePeriod> _periods = [];

        public Guid WorkItemId { get; } = Guid.NewGuid();

        public IReadOnlyList<SprintScopePeriod> Periods => _periods;

        public ItemHistory Then(Instant from, Guid? iterationId, WorkStatusCategory status, double? storyPoints = 3, bool isRequirement = true)
        {
            if (_periods.Count > 0)
                _periods[^1] = _periods[^1] with { ValidTo = from };

            _periods.Add(new SprintScopePeriod(WorkItemId, from, null, iterationId, isRequirement, status, storyPoints, null, null));
            return this;
        }
    }
}
