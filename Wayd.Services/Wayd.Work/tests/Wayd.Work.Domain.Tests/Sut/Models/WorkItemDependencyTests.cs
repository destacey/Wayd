using NodaTime;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Tests.Shared;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;

namespace Wayd.Work.Domain.Tests.Sut.Models;

public class WorkItemDependencyTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly WorkItemDependencyFaker _dependencyFaker;
    private readonly WorkItemFaker _workItemFaker;
    private readonly WorkIterationFaker _workIterationFaker;

    public WorkItemDependencyTests()
    {
        _dateTimeProvider = new(new DateTime(2025, 04, 01, 11, 0, 0));

        _dependencyFaker = new WorkItemDependencyFaker(_dateTimeProvider.Now);
        _workItemFaker = new WorkItemFaker();
        _workIterationFaker = new WorkIterationFaker();
    }

    [Theory]
    [InlineData(WorkStatusCategory.Proposed, DependencyState.ToDo)]
    [InlineData(WorkStatusCategory.Active, DependencyState.InProgress)]
    [InlineData(WorkStatusCategory.Done, DependencyState.Done)]
    [InlineData(WorkStatusCategory.Removed, DependencyState.Removed)]
    public void Create_State_MapsStatusCategoriesCorrectly(WorkStatusCategory sourceStatus, DependencyState expectedState)
    {
        // Arrange
        var now = _dateTimeProvider.Now;

        var source = _workItemFaker.WithStatusCategory(sourceStatus).Generate();
        var target = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).Generate();


        // Act
        var dep = WorkItemDependency.Create(DependencyWorkItemInfo.Create(source), DependencyWorkItemInfo.Create(target), now, null, null, null, null, now);

        // Assert
        Assert.Equal(expectedState, dep.State);
    }

    [Fact]
    public void Create_WithRemovedOn_StateIsRemoved()
    {
        // Arrange
        var now = _dateTimeProvider.Now;

        var source = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).Generate();
        var target = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).Generate();
        var removedOn = now.Plus(Duration.FromDays(1));

        // Act
        var dep = WorkItemDependency.Create(DependencyWorkItemInfo.Create(source), DependencyWorkItemInfo.Create(target), now, null, removedOn, Guid.NewGuid(), null, now);

        // Assert
        Assert.Equal(DependencyState.Deleted, dep.State);
    }

    [Theory]
    [MemberData(nameof(HealthTestCases))]
    public void Create_Health_VariousCases(WorkStatusCategory sourceStatusCategory, WorkStatusCategory targetStatusCategory, int? sourceOffsetDays, int? targetOffsetDays, DependencyPlanningHealth expected)
    {
        // Arrange
        var now = _dateTimeProvider.Now;

        var source = _workItemFaker.WithStatusCategory(sourceStatusCategory).Generate();
        var target = _workItemFaker.WithStatusCategory(targetStatusCategory).Generate();

        var sourcePlannedOn = sourceOffsetDays.HasValue
            ? now.Plus(Duration.FromDays(sourceOffsetDays.Value))
            : (Instant?)null;
        var targetPlannedOn = targetOffsetDays.HasValue
            ? now.Plus(Duration.FromDays(targetOffsetDays.Value))
            : (Instant?)null;

        // Act
        var dep = _dependencyFaker.WithSource(source).WithTarget(target).WithSourcePlannedOn(sourcePlannedOn).WithTargetPlannedOn(targetPlannedOn).WithCreatedOn(now).Generate();

        // Assert
        Assert.Equal(expected, dep.Health);
    }

    public static IEnumerable<object[]> HealthTestCases()
    {
        // state Done should always be Healthy (ignores planned dates)
        yield return new object[] { WorkStatusCategory.Done, WorkStatusCategory.Proposed, -10, -5, DependencyPlanningHealth.Healthy };
        yield return new object[] { WorkStatusCategory.Done, WorkStatusCategory.Active, -10, -5, DependencyPlanningHealth.Healthy };
        yield return new object[] { WorkStatusCategory.Done, WorkStatusCategory.Done, -10, -5, DependencyPlanningHealth.Healthy };
        yield return new object[] { WorkStatusCategory.Done, WorkStatusCategory.Removed, -10, -5, DependencyPlanningHealth.Healthy };

        // state Removed should always be Unhealthy (ignores planned dates)
        yield return new object[] { WorkStatusCategory.Removed, WorkStatusCategory.Proposed, -10, -5, DependencyPlanningHealth.Unhealthy };
        yield return new object[] { WorkStatusCategory.Removed, WorkStatusCategory.Active, -10, -5, DependencyPlanningHealth.Unhealthy };
        yield return new object[] { WorkStatusCategory.Removed, WorkStatusCategory.Done, -10, -5, DependencyPlanningHealth.Unhealthy };
        yield return new object[] { WorkStatusCategory.Removed, WorkStatusCategory.Removed, -10, -5, DependencyPlanningHealth.Unhealthy };

        // both proposed and unplanned, -> AtRisk
        yield return new object[] { WorkStatusCategory.Proposed, WorkStatusCategory.Proposed, null!, null!, DependencyPlanningHealth.AtRisk };

        // if the source is not done but the target is active/done/removed, -> Unhealthy
        yield return new object[] { WorkStatusCategory.Proposed, WorkStatusCategory.Active, null!, null!, DependencyPlanningHealth.Unhealthy };
        yield return new object[] { WorkStatusCategory.Proposed, WorkStatusCategory.Done, null!, null!, DependencyPlanningHealth.Unhealthy };
        yield return new object[] { WorkStatusCategory.Proposed, WorkStatusCategory.Removed, null!, null!, DependencyPlanningHealth.Unhealthy };
        yield return new object[] { WorkStatusCategory.Active, WorkStatusCategory.Active, null!, null!, DependencyPlanningHealth.Unhealthy };
        yield return new object[] { WorkStatusCategory.Active, WorkStatusCategory.Done, null!, null!, DependencyPlanningHealth.Unhealthy };
        yield return new object[] { WorkStatusCategory.Active, WorkStatusCategory.Removed, null!, null!, DependencyPlanningHealth.Unhealthy };

        // both planned in the past (or at now) -> treated as unplanned -> AtRisk
        yield return new object[] { WorkStatusCategory.Proposed, WorkStatusCategory.Proposed, -2, -1, DependencyPlanningHealth.AtRisk };
        yield return new object[] { WorkStatusCategory.Proposed, WorkStatusCategory.Proposed, 0, 0, DependencyPlanningHealth.AtRisk };

        // predecessor before successor -> Healthy
        yield return new object[] { WorkStatusCategory.Proposed, WorkStatusCategory.Proposed, 1, 2, DependencyPlanningHealth.Healthy };

        // predecessor equal successor -> Healthy
        yield return new object[] { WorkStatusCategory.Proposed, WorkStatusCategory.Proposed, 5, 5, DependencyPlanningHealth.Healthy };

        // predecessor after successor -> Unhealthy
        yield return new object[] { WorkStatusCategory.Proposed, WorkStatusCategory.Proposed, 5, 2, DependencyPlanningHealth.Unhealthy };

        // predecessor planned and successor unplanned -> Healthy
        yield return new object[] { WorkStatusCategory.Proposed, WorkStatusCategory.Proposed, 3, null!, DependencyPlanningHealth.Healthy };
    }

    [Fact]
    public void UpdateSourceInfo_RecomputesHealth()
    {
        // Arrange: start with no planned dates -> AtRisk
        var now = _dateTimeProvider.Now;

        var source = _workItemFaker.WithProposedState().Generate();
        var target = _workItemFaker.WithStatusCategory(WorkStatusCategory.Proposed).Generate();
        var dep = _dependencyFaker.WithSource(source).WithTarget(target).WithCreatedOn(now).Generate();

        Assert.Equal(DependencyPlanningHealth.AtRisk, dep.Health);

        // Act: add source planned date
        var sourcePlannedOn = now.Plus(Duration.FromDays(5));
        var sourceInfo = new DependencyWorkItemInfo
        {
            WorkItemId = source.Id,
            StatusCategory = WorkStatusCategory.Proposed,
            PlannedOn = sourcePlannedOn
        };
        dep.UpdateSourceInfo(sourceInfo, now);

        // Assert: now healthy (source planned, target unplanned)
        Assert.Equal(DependencyPlanningHealth.Healthy, dep.Health);
        Assert.Equal(sourcePlannedOn, dep.SourcePlannedOn);
    }

    [Fact]
    public void UpdateTargetInfo_RecomputesHealth()
    {
        // Arrange: predecessor after successor -> Unhealthy
        var now = _dateTimeProvider.Now;

        var source = _workItemFaker.WithProposedState().Generate();
        var target = _workItemFaker.WithProposedState().Generate();
        var sourcePlannedOn = now.Plus(Duration.FromDays(5));
        var targetPlannedOn = now.Plus(Duration.FromDays(2));
        var dep = _dependencyFaker.WithSource(source).WithTarget(target).WithSourcePlannedOn(sourcePlannedOn).WithTargetPlannedOn(targetPlannedOn).WithCreatedOn(now).Generate();

        Assert.Equal(DependencyPlanningHealth.Unhealthy, dep.Health);

        // Act: move target planned date after source
        var newTargetPlannedOn = now.Plus(Duration.FromDays(10));
        var targetInfo = new DependencyWorkItemInfo
        {
            WorkItemId = target.Id,
            StatusCategory = WorkStatusCategory.Proposed,
            PlannedOn = newTargetPlannedOn
        };
        dep.UpdateTargetInfo(targetInfo, now);

        // Assert: now healthy
        Assert.Equal(DependencyPlanningHealth.Healthy, dep.Health);
        Assert.Equal(newTargetPlannedOn, dep.TargetPlannedOn);
    }

    [Fact]
    public void UpdateSourceAndTargetInfo_UpdatesBothDates_RecomputesHealth()
    {
        // Arrange: start with no planned dates -> AtRisk
        var now = _dateTimeProvider.Now;

        var source = _workItemFaker.WithProposedState().Generate();
        var target = _workItemFaker.WithStatusCategory(WorkStatusCategory.Proposed).Generate();
        var dep = _dependencyFaker.WithSource(source).WithTarget(target).WithCreatedOn(now).Generate();

        Assert.Equal(DependencyPlanningHealth.AtRisk, dep.Health);
        Assert.Null(dep.SourcePlannedOn);
        Assert.Null(dep.TargetPlannedOn);

        // Act: update both planned dates (source before target) using the combined method
        var sourcePlannedOn = now.Plus(Duration.FromDays(5));
        var targetPlannedOn = now.Plus(Duration.FromDays(10));
        var sourceInfo = new DependencyWorkItemInfo
        {
            WorkItemId = source.Id,
            StatusCategory = WorkStatusCategory.Proposed,
            PlannedOn = sourcePlannedOn
        };
        var targetInfo = new DependencyWorkItemInfo
        {
            WorkItemId = target.Id,
            StatusCategory = WorkStatusCategory.Proposed,
            PlannedOn = targetPlannedOn
        };
        dep.UpdateSourceAndTargetInfo(sourceInfo, targetInfo, now);

        // Assert: both dates updated and health is Healthy
        Assert.Equal(DependencyPlanningHealth.Healthy, dep.Health);
        Assert.Equal(sourcePlannedOn, dep.SourcePlannedOn);
        Assert.Equal(targetPlannedOn, dep.TargetPlannedOn);
    }

    [Fact]
    public void UpdateSourceAndTargetInfo_WithUnhealthyDates_CorrectlyComputesHealth()
    {
        // Arrange: start with healthy planned dates
        var now = _dateTimeProvider.Now;

        var source = _workItemFaker.WithProposedState().Generate();
        var target = _workItemFaker.WithStatusCategory(WorkStatusCategory.Proposed).Generate();
        var sourcePlannedOn = now.Plus(Duration.FromDays(5));
        var targetPlannedOn = now.Plus(Duration.FromDays(10));
        var dep = _dependencyFaker.WithSource(source).WithTarget(target).WithSourcePlannedOn(sourcePlannedOn).WithTargetPlannedOn(targetPlannedOn).WithCreatedOn(now).Generate();

        Assert.Equal(DependencyPlanningHealth.Healthy, dep.Health);

        // Act: update to unhealthy configuration (source after target) using the combined method
        var newSourcePlannedOn = now.Plus(Duration.FromDays(15));
        var newTargetPlannedOn = now.Plus(Duration.FromDays(7));
        var sourceInfo = new DependencyWorkItemInfo
        {
            WorkItemId = source.Id,
            StatusCategory = WorkStatusCategory.Proposed,
            PlannedOn = newSourcePlannedOn
        };
        var targetInfo = new DependencyWorkItemInfo
        {
            WorkItemId = target.Id,
            StatusCategory = WorkStatusCategory.Proposed,
            PlannedOn = newTargetPlannedOn
        };
        dep.UpdateSourceAndTargetInfo(sourceInfo, targetInfo, now);

        // Assert: health is now Unhealthy
        Assert.Equal(DependencyPlanningHealth.Unhealthy, dep.Health);
        Assert.Equal(newSourcePlannedOn, dep.SourcePlannedOn);
        Assert.Equal(newTargetPlannedOn, dep.TargetPlannedOn);
    }

    [Fact]
    public void UpdateSourceAndTargetInfo_ClearingBothDates_SetsToAtRisk()
    {
        // Arrange: start with healthy planned dates
        var now = _dateTimeProvider.Now;

        var source = _workItemFaker.WithProposedState().Generate();
        var target = _workItemFaker.WithStatusCategory(WorkStatusCategory.Proposed).Generate();
        var sourcePlannedOn = now.Plus(Duration.FromDays(5));
        var targetPlannedOn = now.Plus(Duration.FromDays(10));
        var dep = _dependencyFaker.WithSource(source).WithTarget(target).WithSourcePlannedOn(sourcePlannedOn).WithTargetPlannedOn(targetPlannedOn).WithCreatedOn(now).Generate();

        Assert.Equal(DependencyPlanningHealth.Healthy, dep.Health);

        // Act: clear both planned dates using the combined method
        var sourceInfo = new DependencyWorkItemInfo
        {
            WorkItemId = source.Id,
            StatusCategory = WorkStatusCategory.Proposed,
            PlannedOn = null
        };
        var targetInfo = new DependencyWorkItemInfo
        {
            WorkItemId = target.Id,
            StatusCategory = WorkStatusCategory.Proposed,
            PlannedOn = null
        };
        dep.UpdateSourceAndTargetInfo(sourceInfo, targetInfo, now);

        // Assert: health is AtRisk when both are unplanned
        Assert.Equal(DependencyPlanningHealth.AtRisk, dep.Health);
        Assert.Null(dep.SourcePlannedOn);
        Assert.Null(dep.TargetPlannedOn);
    }

    [Fact]
    public void UpdateSourceInfo_OnlySourcePlanned_SetsToHealthy()
    {
        // Arrange: start with both unplanned -> AtRisk
        var now = _dateTimeProvider.Now;

        var source = _workItemFaker.WithProposedState().Generate();
        var target = _workItemFaker.WithStatusCategory(WorkStatusCategory.Proposed).Generate();
        var dep = _dependencyFaker.WithSource(source).WithTarget(target).WithCreatedOn(now).Generate();

        Assert.Equal(DependencyPlanningHealth.AtRisk, dep.Health);

        // Act: set only source planned date
        var sourcePlannedOn = now.Plus(Duration.FromDays(5));
        var sourceInfo = new DependencyWorkItemInfo
        {
            WorkItemId = source.Id,
            StatusCategory = WorkStatusCategory.Proposed,
            PlannedOn = sourcePlannedOn
        };
        dep.UpdateSourceInfo(sourceInfo, now);

        // Assert: predecessor planned and successor unplanned -> Healthy
        Assert.Equal(DependencyPlanningHealth.Healthy, dep.Health);
        Assert.Equal(sourcePlannedOn, dep.SourcePlannedOn);
        Assert.Null(dep.TargetPlannedOn);
    }

    [Fact]
    public void UpdateSourceAndTargetInfo_WithInvalidSourceId_ReturnsFailure()
    {
        // Arrange
        var now = _dateTimeProvider.Now;

        var source = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).Generate();
        var target = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).Generate();
        var dep = _dependencyFaker.WithSource(source).WithTarget(target).WithCreatedOn(now).Generate();

        // Act: use wrong source ID
        var sourceInfo = new DependencyWorkItemInfo
        {
            WorkItemId = Guid.NewGuid(), // Wrong ID
            StatusCategory = WorkStatusCategory.Active,
            PlannedOn = now.Plus(Duration.FromDays(5))
        };
        var targetInfo = new DependencyWorkItemInfo
        {
            WorkItemId = target.Id,
            StatusCategory = WorkStatusCategory.Active,
            PlannedOn = now.Plus(Duration.FromDays(10))
        };
        var result = dep.UpdateSourceAndTargetInfo(sourceInfo, targetInfo, now);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("does not match existing SourceId", result.Error);
    }

    [Fact]
    public void UpdateSourceAndTargetInfo_WithInvalidTargetId_ReturnsFailure()
    {
        // Arrange
        var now = _dateTimeProvider.Now;

        var source = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).Generate();
        var target = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).Generate();
        var dep = _dependencyFaker.WithSource(source).WithTarget(target).WithCreatedOn(now).Generate();

        // Act: use wrong target ID
        var sourceInfo = new DependencyWorkItemInfo
        {
            WorkItemId = source.Id,
            StatusCategory = WorkStatusCategory.Active,
            PlannedOn = now.Plus(Duration.FromDays(5))
        };
        var targetInfo = new DependencyWorkItemInfo
        {
            WorkItemId = Guid.NewGuid(), // Wrong ID
            StatusCategory = WorkStatusCategory.Active,
            PlannedOn = now.Plus(Duration.FromDays(10))
        };
        var result = dep.UpdateSourceAndTargetInfo(sourceInfo, targetInfo, now);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("does not match existing TargetId", result.Error);
    }

    [Fact]
    public void UpdateSourceAndTargetInfo_ChangingStatusCategory_UpdatesStateAndHealth()
    {
        // Arrange: start with both active
        var now = _dateTimeProvider.Now;

        var source = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).Generate();
        var target = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).Generate();
        var dep = _dependencyFaker.WithSource(source).WithTarget(target).WithCreatedOn(now).Generate();

        Assert.Equal(DependencyState.InProgress, dep.State);

        // Act: mark source as Done
        var sourceInfo = new DependencyWorkItemInfo
        {
            WorkItemId = source.Id,
            StatusCategory = WorkStatusCategory.Done,
            PlannedOn = null
        };
        var targetInfo = new DependencyWorkItemInfo
        {
            WorkItemId = target.Id,
            StatusCategory = WorkStatusCategory.Active,
            PlannedOn = null
        };
        dep.UpdateSourceAndTargetInfo(sourceInfo, targetInfo, now);

        // Assert: state should be Done and health should be Healthy
        Assert.Equal(DependencyState.Done, dep.State);
        Assert.Equal(DependencyPlanningHealth.Healthy, dep.Health);
    }

    [Fact]
    public void UpdateSourceInfo_WithRecalculateFalse_DoesNotRecalculate()
    {
        // Arrange: start with unhealthy (predecessor after successor)
        var now = _dateTimeProvider.Now;

        var source = _workItemFaker.WithProposedState().Generate();
        var target = _workItemFaker.WithStatusCategory(WorkStatusCategory.Proposed).Generate();
        var sourcePlannedOn = now.Plus(Duration.FromDays(10));
        var targetPlannedOn = now.Plus(Duration.FromDays(5));
        var dep = _dependencyFaker.WithSource(source).WithTarget(target).WithSourcePlannedOn(sourcePlannedOn).WithTargetPlannedOn(targetPlannedOn).WithCreatedOn(now).Generate();

        Assert.Equal(DependencyPlanningHealth.Unhealthy, dep.Health);

        // Act: update source to earlier date but skip recalculation
        var newSourcePlannedOn = now.Plus(Duration.FromDays(1));
        var sourceInfo = new DependencyWorkItemInfo
        {
            WorkItemId = source.Id,
            StatusCategory = WorkStatusCategory.Proposed,
            PlannedOn = newSourcePlannedOn
        };
        dep.UpdateSourceInfo(sourceInfo, now, recalculate: false);

        // Assert: source is updated but health is NOT recalculated yet (still Unhealthy)
        Assert.Equal(newSourcePlannedOn, dep.SourcePlannedOn);
        Assert.Equal(DependencyPlanningHealth.Unhealthy, dep.Health);

        // Act: now manually recalculate
        dep.CalculateStateAndHealth(now);

        // Assert: health is now Healthy
        Assert.Equal(DependencyPlanningHealth.Healthy, dep.Health);
    }

    [Fact]
    public void UpdateTargetInfo_WithRecalculateFalse_DoesNotRecalculate()
    {
        // Arrange: start with unhealthy (predecessor after successor)
        var now = _dateTimeProvider.Now;

        var source = _workItemFaker.WithProposedState().Generate();
        var target = _workItemFaker.WithStatusCategory(WorkStatusCategory.Proposed).Generate();
        var sourcePlannedOn = now.Plus(Duration.FromDays(10));
        var targetPlannedOn = now.Plus(Duration.FromDays(5));
        var dep = _dependencyFaker.WithSource(source).WithTarget(target).WithSourcePlannedOn(sourcePlannedOn).WithTargetPlannedOn(targetPlannedOn).WithCreatedOn(now).Generate();

        Assert.Equal(DependencyPlanningHealth.Unhealthy, dep.Health);

        // Act: update target to later date but skip recalculation
        var newTargetPlannedOn = now.Plus(Duration.FromDays(15));
        var targetInfo = new DependencyWorkItemInfo
        {
            WorkItemId = target.Id,
            StatusCategory = WorkStatusCategory.Proposed,
            PlannedOn = newTargetPlannedOn
        };
        dep.UpdateTargetInfo(targetInfo, now, recalculate: false);

        // Assert: target is updated but health is NOT recalculated yet (still Unhealthy)
        Assert.Equal(newTargetPlannedOn, dep.TargetPlannedOn);
        Assert.Equal(DependencyPlanningHealth.Unhealthy, dep.Health);

        // Act: now manually recalculate
        dep.CalculateStateAndHealth(now);

        // Assert: health is now Healthy
        Assert.Equal(DependencyPlanningHealth.Healthy, dep.Health);
    }

    [Fact]
    public void Create_WithIterationEndDate_UsesAsPlannedOn()
    {
        // Arrange
        var now = _dateTimeProvider.Now;

        var sourceIteration = _workIterationFaker.WithEndDate(now.Plus(Duration.FromDays(5))).Generate();
        var targetIteration = _workIterationFaker.WithEndDate(now.Plus(Duration.FromDays(10))).Generate();

        var source = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).WithIterationId(sourceIteration.Id).Generate();
        source.Iteration = sourceIteration;
        var target = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).WithIterationId(targetIteration.Id).Generate();
        target.Iteration = targetIteration;

        // Act
        var dep = WorkItemDependency.Create(DependencyWorkItemInfo.Create(source, now), DependencyWorkItemInfo.Create(target, now), now, null, null, null, null, now);

        // Assert: should use iteration end dates as planned dates
        Assert.Equal(sourceIteration.DateRange.End, dep.SourcePlannedOn);
        Assert.Equal(targetIteration.DateRange.End, dep.TargetPlannedOn);
    }

    [Fact]
    public void Create_CompletedIteration_IsFilteredOut()
    {
        // Arrange
        var now = _dateTimeProvider.Now;

        var completedIteration = _workIterationFaker.WithEndDate(now.Plus(Duration.FromDays(5)), IterationState.Completed).Generate();
        var source = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).WithIterationId(completedIteration.Id).Generate();
        source.Iteration = completedIteration;
        var target = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).Generate();

        // Act
        var dep = WorkItemDependency.Create(DependencyWorkItemInfo.Create(source, now), DependencyWorkItemInfo.Create(target, now), now, null, null, null, null, now);

        // Assert: completed iteration should be filtered out
        Assert.Null(dep.SourcePlannedOn);
    }

    [Fact]
    public void Create_PastIteration_IsFilteredOut()
    {
        // Arrange
        var now = _dateTimeProvider.Now;

        var pastIteration = _workIterationFaker.WithEndDate(now.Minus(Duration.FromDays(5))).Generate();
        var source = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).WithIterationId(pastIteration.Id).Generate();
        source.Iteration = pastIteration;
        var target = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).Generate();

        // Act
        var dep = WorkItemDependency.Create(DependencyWorkItemInfo.Create(source, now), DependencyWorkItemInfo.Create(target, now), now, null, null, null, null, now);

        // Assert: past iteration should be filtered out (treated as unplanned)
        Assert.Null(dep.SourcePlannedOn);
    }

    [Fact]
    public void Create_NonSprintIteration_IsFilteredOut()
    {
        // Arrange
        var now = _dateTimeProvider.Now;

        var iteration = _workIterationFaker.WithEndDate(now.Plus(Duration.FromDays(5)), IterationState.Active, IterationType.Iteration).Generate();
        var source = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).WithIterationId(iteration.Id).Generate();
        source.Iteration = iteration;
        var target = _workItemFaker.WithStatusCategory(WorkStatusCategory.Active).Generate();

        // Act
        var dep = WorkItemDependency.Create(DependencyWorkItemInfo.Create(source, now), DependencyWorkItemInfo.Create(target, now), now, null, null, null, null, now);

        // Assert: non-sprint iteration should be filtered out
        Assert.Null(dep.SourcePlannedOn);
    }

    [Fact]
    public void WorkItemLink_Update_SetsAuditFields()
    {
        // Arrange
        var now = _dateTimeProvider.Now;

        var createdBy = Guid.NewGuid();
        var createdOn = now;
        var hierarchy = WorkItemHierarchy.Create(Guid.NewGuid(), Guid.NewGuid(), createdOn, createdBy, null, null, null);

        // Act
        var newCreatedBy = Guid.NewGuid();
        var removedBy = Guid.NewGuid();
        hierarchy.Update(newCreatedBy, removedBy, "a comment");

        // Assert
        Assert.Equal(newCreatedBy, hierarchy.CreatedById);
        Assert.Equal(removedBy, hierarchy.RemovedById);
        Assert.Equal("a comment", hierarchy.Comment);
    }

    [Fact]
    public void WorkItemLink_RemoveLink_SetsRemovedProperties()
    {
        // Arrange
        var now = _dateTimeProvider.Now;

        var createdBy = Guid.NewGuid();
        var createdOn = now;
        var hierarchy = WorkItemHierarchy.Create(Guid.NewGuid(), Guid.NewGuid(), createdOn, createdBy, null, null, null);

        // Act
        var removedOn = now.Plus(Duration.FromDays(1));
        var removedBy = Guid.NewGuid();
        hierarchy.RemoveLink(removedOn, removedBy);

        // Assert
        Assert.Equal(removedOn, hierarchy.RemovedOn);
        Assert.Equal(removedBy, hierarchy.RemovedById);
    }
}