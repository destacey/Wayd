using NodaTime;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkItems.Dtos;

public sealed class SprintWorkItemMetricsDtoTests
{
    private static readonly Instant BaseInstant = Instant.FromUtc(2024, 1, 1, 0, 0, 0);

    /// <summary>
    /// Builds a Done work item with a known cycle time. We don't go through
    /// <see cref="WorkItem.CreateExternal"/> because all this DTO needs is a few
    /// fields — the existing faker writes them directly via reflection.
    /// </summary>
    private static WorkItem DoneItem(double cycleTimeDays, double? storyPoints = null)
    {
        var activated = BaseInstant;
        var done = activated.Plus(Duration.FromTimeSpan(TimeSpan.FromDays(cycleTimeDays)));

        return new WorkItemFaker()
            .WithDoneState()
            .WithActivatedTimestamp(activated).WithDoneTimestamp(done)
            .RuleFor(w => w.StoryPoints, storyPoints)
            .Generate();
    }

    private static WorkItem ProposedItem(double? storyPoints = null) =>
        new WorkItemFaker()
            .WithProposedState()
            .RuleFor(w => w.StoryPoints, storyPoints)
            .Generate();

    private static WorkItem ActiveItem(double? storyPoints = null) =>
        new WorkItemFaker()
            .WithActiveState()
            .RuleFor(w => w.StoryPoints, storyPoints)
            .Generate();

    /// <summary>Done item but with the timestamps stripped — should not contribute to cycle time.</summary>
    private static WorkItem DoneItemMissingTimestamps()
    {
        var item = new WorkItemFaker().WithDoneState().Generate();
        // Faker assigns these; strip them via reflection-equivalent (init-only setters
        // are honored by Bogus). Easiest way: rebuild with the rule overridden.
        return new WorkItemFaker()
            .WithDoneState()
            .RuleFor(w => w.ActivatedTimestamp, (Instant?)null)
            .RuleFor(w => w.DoneTimestamp, (Instant?)null)
            .Generate();
    }

    /// <summary>Done item where Done timestamp precedes Activated — invalid, must be skipped.</summary>
    private static WorkItem DoneItemWithInvertedTimestamps()
    {
        var activated = BaseInstant.Plus(Duration.FromTimeSpan(TimeSpan.FromDays(5)));
        var done = BaseInstant; // earlier than activated
        return new WorkItemFaker()
            .WithDoneState()
            .RuleFor(w => w.ActivatedTimestamp, (Instant?)activated)
            .RuleFor(w => w.DoneTimestamp, (Instant?)done)
            .Generate();
    }

    private static WorkItem RemovedItem(double? storyPoints = null)
    {
        // The Removed status counts as "completed" but is not used for cycle time.
        return new WorkItemFaker()
            .RuleFor(w => w.StatusCategory, WorkStatusCategory.Removed)
            .RuleFor(w => w.ActivatedTimestamp, (Instant?)null)
            .RuleFor(w => w.DoneTimestamp, (Instant?)null)
            .RuleFor(w => w.StoryPoints, storyPoints)
            .Generate();
    }

    [Fact]
    public void FromWorkItems_WithEmptyList_ReturnsEmptyShape()
    {
        var sprintId = Guid.NewGuid();

        var result = SprintWorkItemMetricsDto.FromWorkItems(sprintId, SizingMethod.StoryPoints, []);

        Assert.Equal(sprintId, result.SprintId);
        Assert.Equal(SizingMethod.StoryPoints, result.SizingMethod);
        Assert.Equal(0, result.TotalWorkItems);
        Assert.Equal(0d, result.TotalEstimate);
        Assert.Equal(0, result.CompletedWorkItems);
        Assert.Equal(0, result.InProgressWorkItems);
        Assert.Equal(0, result.NotStartedWorkItems);
        Assert.Equal(0, result.UnestimatedWorkItems);
        Assert.Equal(0, result.CycleTime.WorkItemsCount);
        Assert.Equal(0d, result.CycleTime.TotalCycleTimeDays);
        Assert.Null(result.CycleTime.AverageCycleTimeDays);
    }

    [Fact]
    public void FromWorkItems_BucketsByStatusCategory()
    {
        var items = new List<WorkItem>
        {
            ProposedItem(storyPoints: 3),
            ActiveItem(storyPoints: 5),
            ActiveItem(storyPoints: 2),
            DoneItem(cycleTimeDays: 4, storyPoints: 8),
            RemovedItem(storyPoints: 1),
        };

        var result = SprintWorkItemMetricsDto.FromWorkItems(Guid.NewGuid(), SizingMethod.StoryPoints, items);

        Assert.Equal(5, result.TotalWorkItems);
        Assert.Equal(1, result.NotStartedWorkItems);
        Assert.Equal(2, result.InProgressWorkItems);
        // Completed = Done + Removed
        Assert.Equal(2, result.CompletedWorkItems);
    }

    [Fact]
    public void FromWorkItems_SumsStoryPointsByBucket()
    {
        var items = new List<WorkItem>
        {
            ProposedItem(storyPoints: 3),
            ActiveItem(storyPoints: 5),
            ActiveItem(storyPoints: 2),
            DoneItem(cycleTimeDays: 4, storyPoints: 8),
            RemovedItem(storyPoints: 1),
        };

        var result = SprintWorkItemMetricsDto.FromWorkItems(Guid.NewGuid(), SizingMethod.StoryPoints, items);

        Assert.Equal(3 + 5 + 2 + 8 + 1, result.TotalEstimate);
        Assert.Equal(3, result.NotStartedEstimate);
        Assert.Equal(5 + 2, result.InProgressEstimate);
        Assert.Equal(8 + 1, result.CompletedEstimate);
    }

    [Fact]
    public void FromWorkItems_TreatsNullStoryPointsAsUnestimated()
    {
        var items = new List<WorkItem>
        {
            ProposedItem(storyPoints: null),
            ActiveItem(storyPoints: 5),
            DoneItem(cycleTimeDays: 1, storyPoints: null),
        };

        var result = SprintWorkItemMetricsDto.FromWorkItems(Guid.NewGuid(), SizingMethod.StoryPoints, items);

        Assert.Equal(2, result.UnestimatedWorkItems);
    }

    [Fact]
    public void FromWorkItems_CountsZeroAsAnEstimate()
    {
        var items = new List<WorkItem>
        {
            ProposedItem(storyPoints: 0),
            ActiveItem(storyPoints: 5),
        };

        var result = SprintWorkItemMetricsDto.FromWorkItems(Guid.NewGuid(), SizingMethod.StoryPoints, items);

        Assert.Equal(0, result.UnestimatedWorkItems);
        Assert.Equal(5d, result.TotalEstimate);
    }

    [Fact]
    public void FromWorkItems_UnderEffort_SumsEffortAndIgnoresStoryPoints()
    {
        var items = new List<WorkItem>
        {
            new WorkItemFaker().WithProposedState().WithStoryPoints(3).WithEffort(8).Generate(),
            new WorkItemFaker().WithActiveState().WithStoryPoints(5).Generate(),
        };

        var result = SprintWorkItemMetricsDto.FromWorkItems(Guid.NewGuid(), SizingMethod.Effort, items);

        Assert.Equal(SizingMethod.Effort, result.SizingMethod);
        Assert.Equal(8d, result.TotalEstimate);
        Assert.Equal(8d, result.NotStartedEstimate);
        Assert.Equal(0d, result.InProgressEstimate);
        Assert.Equal(1, result.UnestimatedWorkItems);
    }

    [Fact]
    public void FromWorkItems_UnderSize_SumsSize()
    {
        var items = new List<WorkItem>
        {
            new WorkItemFaker().WithActiveState().WithSize(20).WithStoryPoints(2).Generate(),
            new WorkItemFaker().WithActiveState().WithSize(13).Generate(),
        };

        var result = SprintWorkItemMetricsDto.FromWorkItems(Guid.NewGuid(), SizingMethod.Size, items);

        Assert.Equal(33d, result.InProgressEstimate);
        Assert.Equal(0, result.UnestimatedWorkItems);
    }

    [Fact]
    public void FromWorkItems_UnderCount_EstimatesEqualCountsWithNothingUnestimated()
    {
        var items = new List<WorkItem>
        {
            ProposedItem(storyPoints: null),
            ActiveItem(storyPoints: 5),
            DoneItem(cycleTimeDays: 1, storyPoints: 8),
        };

        var result = SprintWorkItemMetricsDto.FromWorkItems(Guid.NewGuid(), SizingMethod.Count, items);

        Assert.Equal(3d, result.TotalEstimate);
        Assert.Equal(1d, result.NotStartedEstimate);
        Assert.Equal(1d, result.InProgressEstimate);
        Assert.Equal(1d, result.CompletedEstimate);
        Assert.Equal(0, result.UnestimatedWorkItems);
    }

    [Fact]
    public void FromWorkItems_OnlyDoneItemsWithValidTimestampsContributeToCycleTime()
    {
        var items = new List<WorkItem>
        {
            ProposedItem(),                          // not Done — skipped
            ActiveItem(),                            // not Done — skipped
            RemovedItem(),                           // Removed (not Done) — skipped
            DoneItemMissingTimestamps(),             // Done but no timestamps — skipped
            DoneItemWithInvertedTimestamps(),        // Done <= Activated — skipped
            DoneItem(cycleTimeDays: 4),
        };

        var result = SprintWorkItemMetricsDto.FromWorkItems(Guid.NewGuid(), SizingMethod.Count, items);

        Assert.Equal(1, result.CycleTime.WorkItemsCount);
        Assert.Equal(4d, result.CycleTime.TotalCycleTimeDays, precision: 10);
        Assert.Equal(4d, result.CycleTime.AverageCycleTimeDays);
    }

    [Fact]
    public void FromWorkItems_CycleTimeSumsCorrectlyAcrossMultipleDoneItems()
    {
        var items = new List<WorkItem>
        {
            DoneItem(cycleTimeDays: 1),
            DoneItem(cycleTimeDays: 2),
            DoneItem(cycleTimeDays: 5),
        };

        var result = SprintWorkItemMetricsDto.FromWorkItems(Guid.NewGuid(), SizingMethod.Count, items);

        Assert.Equal(3, result.CycleTime.WorkItemsCount);
        Assert.Equal(8d, result.CycleTime.TotalCycleTimeDays, precision: 10);
        // Sanity: convenience average matches Σ/count.
        Assert.Equal(8d / 3d, result.CycleTime.AverageCycleTimeDays!.Value, precision: 10);
    }

    [Fact]
    public void FromWorkItems_PreservesSprintIdOnTheDto()
    {
        var sprintId = Guid.NewGuid();

        var result = SprintWorkItemMetricsDto.FromWorkItems(sprintId, SizingMethod.Count, [DoneItem(1)]);

        Assert.Equal(sprintId, result.SprintId);
    }
}