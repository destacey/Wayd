using NodaTime;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Tests.Shared;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;

namespace Wayd.Work.Domain.Tests.Sut.Models;

public class DependencyWorkItemInfoTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly WorkItemFaker _workItemFaker;
    private readonly WorkIterationFaker _workIterationFaker;

    public DependencyWorkItemInfoTests()
    {
        _dateTimeProvider = new(new DateTime(2025, 04, 01, 11, 0, 0));
        _workItemFaker = new WorkItemFaker();
        _workIterationFaker = new WorkIterationFaker();
    }

    [Fact]
    public void Create_WithNoIteration_ReturnsInfoWithNullPlannedOn()
    {
        // Arrange
        var workItem = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).Generate();

        // Act
        var info = DependencyWorkItemInfo.Create(workItem);

        // Assert
        Assert.NotNull(info);
        Assert.Equal(workItem.Id, info.WorkItemId);
        Assert.Equal(workItem.StatusCategory, info.StatusCategory);
        Assert.Null(info.PlannedOn);
    }

    [Fact]
    public void Create_WithSprintIteration_ReturnsInfoWithIterationEndDate()
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var iteration = _workIterationFaker.WithEndDate(now.Plus(Duration.FromDays(5)), IterationState.Active, IterationType.Sprint).Generate();

        var workItem = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).WithIterationId(iteration.Id).Generate();
        workItem.Iteration = iteration;

        // Act
        var info = DependencyWorkItemInfo.Create(workItem, now);

        // Assert
        Assert.NotNull(info);
        Assert.Equal(workItem.Id, info.WorkItemId);
        Assert.Equal(workItem.StatusCategory, info.StatusCategory);
        Assert.Equal(iteration.DateRange.End, info.PlannedOn);
    }

    [Fact]
    public void Create_WithNonSprintIteration_ReturnsInfoWithNullPlannedOn()
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var iteration = _workIterationFaker.WithEndDate(now.Plus(Duration.FromDays(5)), IterationState.Active, IterationType.Iteration).Generate();

        var workItem = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).WithIterationId(iteration.Id).Generate();
        workItem.Iteration = iteration;

        // Act
        var info = DependencyWorkItemInfo.Create(workItem, now);

        // Assert
        Assert.NotNull(info);
        Assert.Equal(workItem.Id, info.WorkItemId);
        Assert.Equal(workItem.StatusCategory, info.StatusCategory);
        Assert.Null(info.PlannedOn);
    }

    [Fact]
    public void Create_WithCompletedSprint_ReturnsInfoWithNullPlannedOn()
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var iteration = _workIterationFaker.WithEndDate(now.Plus(Duration.FromDays(5)), IterationState.Completed, IterationType.Sprint).Generate();

        var workItem = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).WithIterationId(iteration.Id).Generate();
        workItem.Iteration = iteration;

        // Act
        var info = DependencyWorkItemInfo.Create(workItem, now);

        // Assert
        Assert.NotNull(info);
        Assert.Equal(workItem.Id, info.WorkItemId);
        Assert.Equal(workItem.StatusCategory, info.StatusCategory);
        Assert.Null(info.PlannedOn);
    }

    [Fact]
    public void Create_WithPastSprint_ReturnsInfoWithNullPlannedOn()
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var iteration = _workIterationFaker.WithEndDate(now.Minus(Duration.FromDays(5)), IterationState.Active, IterationType.Sprint).Generate();

        var workItem = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).WithIterationId(iteration.Id).Generate();
        workItem.Iteration = iteration;

        // Act
        var info = DependencyWorkItemInfo.Create(workItem, now);

        // Assert
        Assert.NotNull(info);
        Assert.Equal(workItem.Id, info.WorkItemId);
        Assert.Equal(workItem.StatusCategory, info.StatusCategory);
        Assert.Null(info.PlannedOn);
    }

    [Fact]
    public void Create_WithSprintEndingAtNow_ReturnsInfoWithNullPlannedOn()
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var iteration = _workIterationFaker.WithEndDate(now, IterationState.Active, IterationType.Sprint).Generate();

        var workItem = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).WithIterationId(iteration.Id).Generate();
        workItem.Iteration = iteration;

        // Act
        var info = DependencyWorkItemInfo.Create(workItem, now);

        // Assert
        Assert.NotNull(info);
        Assert.Equal(workItem.Id, info.WorkItemId);
        Assert.Equal(workItem.StatusCategory, info.StatusCategory);
        Assert.Null(info.PlannedOn);
    }

    [Fact]
    public void Create_WithFutureSprint_ReturnsInfoWithIterationEndDate()
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var iteration = _workIterationFaker.WithEndDate(now.Plus(Duration.FromDays(1)), IterationState.Future, IterationType.Sprint).Generate();

        var workItem = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).WithIterationId(iteration.Id).Generate();
        workItem.Iteration = iteration;

        // Act
        var info = DependencyWorkItemInfo.Create(workItem, now);

        // Assert
        Assert.NotNull(info);
        Assert.Equal(workItem.Id, info.WorkItemId);
        Assert.Equal(workItem.StatusCategory, info.StatusCategory);
        Assert.Equal(iteration.DateRange.End, info.PlannedOn);
    }

    [Fact]
    public void Create_WithoutNowParameter_IncludesPastSprintDate()
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var iteration = _workIterationFaker.WithEndDate(now.Minus(Duration.FromDays(5)), IterationState.Active, IterationType.Sprint).Generate();

        var workItem = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).WithIterationId(iteration.Id).Generate();
        workItem.Iteration = iteration;

        // Act - calling without 'now' parameter should not filter by date
        var info = DependencyWorkItemInfo.Create(workItem);

        // Assert
        Assert.NotNull(info);
        Assert.Equal(workItem.Id, info.WorkItemId);
        Assert.Equal(workItem.StatusCategory, info.StatusCategory);
        Assert.Equal(iteration.DateRange.End, info.PlannedOn);
    }

    [Theory]
    [InlineData(WorkStatusCategory.Proposed)]
    [InlineData(WorkStatusCategory.Active)]
    [InlineData(WorkStatusCategory.Done)]
    [InlineData(WorkStatusCategory.Removed)]
    public void Create_PreservesStatusCategory(WorkStatusCategory statusCategory)
    {
        // Arrange
        var workItem = _workItemFaker.WithStatusCategory(statusCategory).Generate();

        // Act
        var info = DependencyWorkItemInfo.Create(workItem);

        // Assert
        Assert.Equal(statusCategory, info.StatusCategory);
    }

    [Fact]
    public void Create_WithActiveSprint_ReturnsInfoWithIterationEndDate()
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var iteration = _workIterationFaker.WithEndDate(now.Plus(Duration.FromDays(7)), IterationState.Active, IterationType.Sprint).Generate();

        var workItem = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).WithIterationId(iteration.Id).Generate();
        workItem.Iteration = iteration;

        // Act
        var info = DependencyWorkItemInfo.Create(workItem, now);

        // Assert
        Assert.NotNull(info);
        Assert.Equal(iteration.DateRange.End, info.PlannedOn);
    }

    [Fact]
    public void Create_WithMultipleConditionsNotMet_ReturnsNullPlannedOn()
    {
        // Arrange - Create a completed, past, non-sprint iteration (all filters should exclude it)
        var now = _dateTimeProvider.Now;
        var iteration = _workIterationFaker.WithEndDate(now.Minus(Duration.FromDays(5)), IterationState.Completed, IterationType.Iteration).Generate();

        var workItem = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).WithIterationId(iteration.Id).Generate();
        workItem.Iteration = iteration;

        // Act
        var info = DependencyWorkItemInfo.Create(workItem, now);

        // Assert
        Assert.NotNull(info);
        Assert.Null(info.PlannedOn);
    }

    [Fact]
    public void Create_IsRecordType_AllowsWithSyntax()
    {
        // Arrange
        var workItem = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).Generate();
        var originalInfo = DependencyWorkItemInfo.Create(workItem);

        // Act - Records support 'with' syntax for non-destructive mutation
        var modifiedInfo = originalInfo with { PlannedOn = _dateTimeProvider.Now };

        // Assert
        Assert.NotEqual(originalInfo.PlannedOn, modifiedInfo.PlannedOn);
        Assert.Equal(originalInfo.WorkItemId, modifiedInfo.WorkItemId);
        Assert.Equal(originalInfo.StatusCategory, modifiedInfo.StatusCategory);
    }
}