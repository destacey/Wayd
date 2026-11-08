using NodaTime;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Tests.Shared;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;

namespace Wayd.Work.Domain.Tests.Sut.Models;

public class WorkItemDependencyTests
{
    private static readonly DateTimeZone Chicago = DateTimeZoneProviders.Tzdb["America/Chicago"];
    private static readonly DateTimeZone Tokyo = DateTimeZoneProviders.Tzdb["Asia/Tokyo"];

    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly WorkItemDependencyFaker _dependencyFaker;
    private readonly WorkItemFaker _workItemFaker;

    public WorkItemDependencyTests()
    {
        _dateTimeProvider = new(new DateTime(2025, 04, 01, 11, 0, 0));

        _dependencyFaker = new WorkItemDependencyFaker();
        _workItemFaker = new WorkItemFaker();
    }

    [Theory]
    [InlineData(StatusCategory.Proposed, DependencyState.ToDo)]
    [InlineData(StatusCategory.Active, DependencyState.InProgress)]
    [InlineData(StatusCategory.Done, DependencyState.Done)]
    [InlineData(StatusCategory.Removed, DependencyState.Removed)]
    public void Create_State_MapsStatusCategoriesCorrectly(StatusCategory sourceStatus, DependencyState expectedState)
    {
        // Arrange
        var now = _dateTimeProvider.Now;

        var source = _workItemFaker.WithStatusCategory(sourceStatus).Generate();
        var target = _workItemFaker.WithStatusCategory(StatusCategory.Active).Generate();


        // Act
        var dep = WorkItemDependency.Create(DependencyWorkItemInfo.Create(source.Id, source.StatusCategory), DependencyWorkItemInfo.Create(target.Id, target.StatusCategory), now, null, null, null, null);

        // Assert
        Assert.Equal(expectedState, dep.State);
    }

    [Fact]
    public void Create_WithRemovedOn_StateIsRemoved()
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var today = _dateTimeProvider.Today;

        var source = _workItemFaker.WithStatusCategory(StatusCategory.Active).Generate();
        var target = _workItemFaker.WithStatusCategory(StatusCategory.Active).Generate();
        var removedOn = now.Plus(Duration.FromDays(1));

        // Act
        var dep = WorkItemDependency.Create(DependencyWorkItemInfo.Create(source.Id, source.StatusCategory), DependencyWorkItemInfo.Create(target.Id, target.StatusCategory), now, null, removedOn, Guid.NewGuid(), null);

        // Assert
        Assert.Equal(DependencyState.Deleted, dep.State);
    }

    [Fact]
    public void Create_WithRemovedOnAndDonePredecessor_HealthIsUnknown()
    {
        // Arrange
        var now = _dateTimeProvider.Now;

        var source = _workItemFaker.WithStatusCategory(StatusCategory.Done).Generate();
        var target = _workItemFaker.WithStatusCategory(StatusCategory.Done).Generate();
        var removedOn = now.Plus(Duration.FromDays(1));

        // Act
        var dep = WorkItemDependency.Create(DependencyWorkItemInfo.Create(source.Id, source.StatusCategory), DependencyWorkItemInfo.Create(target.Id, target.StatusCategory), now, null, removedOn, Guid.NewGuid(), null);

        // Assert
        Assert.Equal(DependencyState.Deleted, dep.State);
        Assert.Equal(DependencyPlanningHealth.Unknown, dep.Health);
    }

    [Theory]
    [MemberData(nameof(HealthTestCases))]
    public void Create_Health_VariousCases(StatusCategory sourceStatusCategory, StatusCategory targetStatusCategory, int? sourceOffsetDays, int? targetOffsetDays, DependencyPlanningHealth expected)
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var today = _dateTimeProvider.Today;

        var source = _workItemFaker.WithStatusCategory(sourceStatusCategory).Generate();
        var target = _workItemFaker.WithStatusCategory(targetStatusCategory).Generate();

        var sourcePlannedOn = sourceOffsetDays.HasValue
            ? today.PlusDays(sourceOffsetDays.Value)
            : (LocalDate?)null;
        var targetPlannedOn = targetOffsetDays.HasValue
            ? today.PlusDays(targetOffsetDays.Value)
            : (LocalDate?)null;

        // Act
        var dep = _dependencyFaker.WithSource(source).WithTarget(target).WithSourcePlannedOn(sourcePlannedOn).WithTargetPlannedOn(targetPlannedOn).WithCreatedOn(now).Generate();

        // Assert
        Assert.Equal(expected, dep.Health);
    }

    public static IEnumerable<object[]> HealthTestCases()
    {
        // a done predecessor is Closed whatever the successor (ignores planned dates)
        yield return new object[] { StatusCategory.Done, StatusCategory.Proposed, -10, -5, DependencyPlanningHealth.Closed };
        yield return new object[] { StatusCategory.Done, StatusCategory.Active, -10, -5, DependencyPlanningHealth.Closed };
        yield return new object[] { StatusCategory.Done, StatusCategory.Done, -10, -5, DependencyPlanningHealth.Closed };
        yield return new object[] { StatusCategory.Done, StatusCategory.Removed, -10, -5, DependencyPlanningHealth.Closed };

        // a removed predecessor is Unhealthy while the successor is open, Closed once it is done or removed
        yield return new object[] { StatusCategory.Removed, StatusCategory.Proposed, -10, -5, DependencyPlanningHealth.Unhealthy };
        yield return new object[] { StatusCategory.Removed, StatusCategory.Active, -10, -5, DependencyPlanningHealth.Unhealthy };
        yield return new object[] { StatusCategory.Removed, StatusCategory.Done, -10, -5, DependencyPlanningHealth.Closed };
        yield return new object[] { StatusCategory.Removed, StatusCategory.Removed, -10, -5, DependencyPlanningHealth.Closed };

        // both proposed and unplanned, -> AtRisk
        yield return new object[] { StatusCategory.Proposed, StatusCategory.Proposed, null!, null!, DependencyPlanningHealth.AtRisk };

        // if the source is not done but the target is active/done/removed, -> Unhealthy
        yield return new object[] { StatusCategory.Proposed, StatusCategory.Active, null!, null!, DependencyPlanningHealth.Unhealthy };
        yield return new object[] { StatusCategory.Proposed, StatusCategory.Done, null!, null!, DependencyPlanningHealth.Unhealthy };
        yield return new object[] { StatusCategory.Proposed, StatusCategory.Removed, null!, null!, DependencyPlanningHealth.Unhealthy };
        yield return new object[] { StatusCategory.Active, StatusCategory.Active, null!, null!, DependencyPlanningHealth.Unhealthy };
        yield return new object[] { StatusCategory.Active, StatusCategory.Done, null!, null!, DependencyPlanningHealth.Unhealthy };
        yield return new object[] { StatusCategory.Active, StatusCategory.Removed, null!, null!, DependencyPlanningHealth.Unhealthy };

        // predecessor before successor -> Healthy
        yield return new object[] { StatusCategory.Proposed, StatusCategory.Proposed, 1, 2, DependencyPlanningHealth.Healthy };

        // predecessor equal successor -> Healthy
        yield return new object[] { StatusCategory.Proposed, StatusCategory.Proposed, 5, 5, DependencyPlanningHealth.Healthy };

        // predecessor after successor -> Unhealthy
        yield return new object[] { StatusCategory.Proposed, StatusCategory.Proposed, 5, 2, DependencyPlanningHealth.Unhealthy };

        // predecessor planned and successor unplanned -> Healthy
        yield return new object[] { StatusCategory.Proposed, StatusCategory.Proposed, 3, null!, DependencyPlanningHealth.Healthy };
    }

    [Fact]
    public void UpdateSourceInfo_RecomputesHealth()
    {
        // Arrange: start with no planned dates -> AtRisk
        var now = _dateTimeProvider.Now;
        var today = _dateTimeProvider.Today;

        var source = _workItemFaker.WithProposedState().Generate();
        var target = _workItemFaker.WithStatusCategory(StatusCategory.Proposed).Generate();
        var dep = _dependencyFaker.WithSource(source).WithTarget(target).WithCreatedOn(now).Generate();

        Assert.Equal(DependencyPlanningHealth.AtRisk, dep.Health);

        // Act: add source planned date
        var sourcePlannedOn = today.PlusDays(5);
        var sourceInfo = new DependencyWorkItemInfo
        {
            WorkItemId = source.Id,
            StatusCategory = StatusCategory.Proposed,
            PlannedOn = sourcePlannedOn
        };
        dep.UpdateSourceInfo(sourceInfo);

        // Assert: now healthy (source planned, target unplanned)
        Assert.Equal(DependencyPlanningHealth.Healthy, dep.Health);
        Assert.Equal(sourcePlannedOn, dep.SourcePlannedOn);
    }

    [Fact]
    public void UpdateTargetInfo_RecomputesHealth()
    {
        // Arrange: predecessor after successor -> Unhealthy
        var now = _dateTimeProvider.Now;
        var today = _dateTimeProvider.Today;

        var source = _workItemFaker.WithProposedState().Generate();
        var target = _workItemFaker.WithProposedState().Generate();
        var sourcePlannedOn = today.PlusDays(5);
        var targetPlannedOn = today.PlusDays(2);
        var dep = _dependencyFaker.WithSource(source).WithTarget(target).WithSourcePlannedOn(sourcePlannedOn).WithTargetPlannedOn(targetPlannedOn).WithCreatedOn(now).Generate();

        Assert.Equal(DependencyPlanningHealth.Unhealthy, dep.Health);

        // Act: move target planned date after source
        var newTargetPlannedOn = today.PlusDays(10);
        var targetInfo = new DependencyWorkItemInfo
        {
            WorkItemId = target.Id,
            StatusCategory = StatusCategory.Proposed,
            PlannedOn = newTargetPlannedOn
        };
        dep.UpdateTargetInfo(targetInfo);

        // Assert: now healthy
        Assert.Equal(DependencyPlanningHealth.Healthy, dep.Health);
        Assert.Equal(newTargetPlannedOn, dep.TargetPlannedOn);
    }

    [Fact]
    public void UpdateSourceAndTargetInfo_UpdatesBothDates_RecomputesHealth()
    {
        // Arrange: start with no planned dates -> AtRisk
        var now = _dateTimeProvider.Now;
        var today = _dateTimeProvider.Today;

        var source = _workItemFaker.WithProposedState().Generate();
        var target = _workItemFaker.WithStatusCategory(StatusCategory.Proposed).Generate();
        var dep = _dependencyFaker.WithSource(source).WithTarget(target).WithCreatedOn(now).Generate();

        Assert.Equal(DependencyPlanningHealth.AtRisk, dep.Health);
        Assert.Null(dep.SourcePlannedOn);
        Assert.Null(dep.TargetPlannedOn);

        // Act: update both planned dates (source before target) using the combined method
        var sourcePlannedOn = today.PlusDays(5);
        var targetPlannedOn = today.PlusDays(10);
        var sourceInfo = new DependencyWorkItemInfo
        {
            WorkItemId = source.Id,
            StatusCategory = StatusCategory.Proposed,
            PlannedOn = sourcePlannedOn
        };
        var targetInfo = new DependencyWorkItemInfo
        {
            WorkItemId = target.Id,
            StatusCategory = StatusCategory.Proposed,
            PlannedOn = targetPlannedOn
        };
        dep.UpdateSourceAndTargetInfo(sourceInfo, targetInfo);

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
        var today = _dateTimeProvider.Today;

        var source = _workItemFaker.WithProposedState().Generate();
        var target = _workItemFaker.WithStatusCategory(StatusCategory.Proposed).Generate();
        var sourcePlannedOn = today.PlusDays(5);
        var targetPlannedOn = today.PlusDays(10);
        var dep = _dependencyFaker.WithSource(source).WithTarget(target).WithSourcePlannedOn(sourcePlannedOn).WithTargetPlannedOn(targetPlannedOn).WithCreatedOn(now).Generate();

        Assert.Equal(DependencyPlanningHealth.Healthy, dep.Health);

        // Act: update to unhealthy configuration (source after target) using the combined method
        var newSourcePlannedOn = today.PlusDays(15);
        var newTargetPlannedOn = today.PlusDays(7);
        var sourceInfo = new DependencyWorkItemInfo
        {
            WorkItemId = source.Id,
            StatusCategory = StatusCategory.Proposed,
            PlannedOn = newSourcePlannedOn
        };
        var targetInfo = new DependencyWorkItemInfo
        {
            WorkItemId = target.Id,
            StatusCategory = StatusCategory.Proposed,
            PlannedOn = newTargetPlannedOn
        };
        dep.UpdateSourceAndTargetInfo(sourceInfo, targetInfo);

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
        var today = _dateTimeProvider.Today;

        var source = _workItemFaker.WithProposedState().Generate();
        var target = _workItemFaker.WithStatusCategory(StatusCategory.Proposed).Generate();
        var sourcePlannedOn = today.PlusDays(5);
        var targetPlannedOn = today.PlusDays(10);
        var dep = _dependencyFaker.WithSource(source).WithTarget(target).WithSourcePlannedOn(sourcePlannedOn).WithTargetPlannedOn(targetPlannedOn).WithCreatedOn(now).Generate();

        Assert.Equal(DependencyPlanningHealth.Healthy, dep.Health);

        // Act: clear both planned dates using the combined method
        var sourceInfo = new DependencyWorkItemInfo
        {
            WorkItemId = source.Id,
            StatusCategory = StatusCategory.Proposed,
            PlannedOn = null
        };
        var targetInfo = new DependencyWorkItemInfo
        {
            WorkItemId = target.Id,
            StatusCategory = StatusCategory.Proposed,
            PlannedOn = null
        };
        dep.UpdateSourceAndTargetInfo(sourceInfo, targetInfo);

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
        var today = _dateTimeProvider.Today;

        var source = _workItemFaker.WithProposedState().Generate();
        var target = _workItemFaker.WithStatusCategory(StatusCategory.Proposed).Generate();
        var dep = _dependencyFaker.WithSource(source).WithTarget(target).WithCreatedOn(now).Generate();

        Assert.Equal(DependencyPlanningHealth.AtRisk, dep.Health);

        // Act: set only source planned date
        var sourcePlannedOn = today.PlusDays(5);
        var sourceInfo = new DependencyWorkItemInfo
        {
            WorkItemId = source.Id,
            StatusCategory = StatusCategory.Proposed,
            PlannedOn = sourcePlannedOn
        };
        dep.UpdateSourceInfo(sourceInfo);

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
        var today = _dateTimeProvider.Today;

        var source = _workItemFaker.WithStatusCategory(StatusCategory.Active).Generate();
        var target = _workItemFaker.WithStatusCategory(StatusCategory.Active).Generate();
        var dep = _dependencyFaker.WithSource(source).WithTarget(target).WithCreatedOn(now).Generate();

        // Act: use wrong source ID
        var sourceInfo = new DependencyWorkItemInfo
        {
            WorkItemId = Guid.NewGuid(), // Wrong ID
            StatusCategory = StatusCategory.Active,
            PlannedOn = today.PlusDays(5)
        };
        var targetInfo = new DependencyWorkItemInfo
        {
            WorkItemId = target.Id,
            StatusCategory = StatusCategory.Active,
            PlannedOn = today.PlusDays(10)
        };
        var result = dep.UpdateSourceAndTargetInfo(sourceInfo, targetInfo);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("does not match existing SourceId", result.Error);
    }

    [Fact]
    public void UpdateSourceAndTargetInfo_WithInvalidTargetId_ReturnsFailure()
    {
        // Arrange
        var now = _dateTimeProvider.Now;
        var today = _dateTimeProvider.Today;

        var source = _workItemFaker.WithStatusCategory(StatusCategory.Active).Generate();
        var target = _workItemFaker.WithStatusCategory(StatusCategory.Active).Generate();
        var dep = _dependencyFaker.WithSource(source).WithTarget(target).WithCreatedOn(now).Generate();

        // Act: use wrong target ID
        var sourceInfo = new DependencyWorkItemInfo
        {
            WorkItemId = source.Id,
            StatusCategory = StatusCategory.Active,
            PlannedOn = today.PlusDays(5)
        };
        var targetInfo = new DependencyWorkItemInfo
        {
            WorkItemId = Guid.NewGuid(), // Wrong ID
            StatusCategory = StatusCategory.Active,
            PlannedOn = today.PlusDays(10)
        };
        var result = dep.UpdateSourceAndTargetInfo(sourceInfo, targetInfo);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("does not match existing TargetId", result.Error);
    }

    [Fact]
    public void UpdateSourceAndTargetInfo_ChangingStatusCategory_UpdatesStateAndHealth()
    {
        // Arrange: start with both active
        var now = _dateTimeProvider.Now;

        var source = _workItemFaker.WithStatusCategory(StatusCategory.Active).Generate();
        var target = _workItemFaker.WithStatusCategory(StatusCategory.Active).Generate();
        var dep = _dependencyFaker.WithSource(source).WithTarget(target).WithCreatedOn(now).Generate();

        Assert.Equal(DependencyState.InProgress, dep.State);

        // Act: mark source as Done
        var sourceInfo = new DependencyWorkItemInfo
        {
            WorkItemId = source.Id,
            StatusCategory = StatusCategory.Done,
            PlannedOn = null
        };
        var targetInfo = new DependencyWorkItemInfo
        {
            WorkItemId = target.Id,
            StatusCategory = StatusCategory.Active,
            PlannedOn = null
        };
        dep.UpdateSourceAndTargetInfo(sourceInfo, targetInfo);

        // Assert: state should be Done and health should be Closed
        Assert.Equal(DependencyState.Done, dep.State);
        Assert.Equal(DependencyPlanningHealth.Closed, dep.Health);
    }

    [Fact]
    public void UpdateSourceInfo_WithRecalculateFalse_DoesNotRecalculate()
    {
        // Arrange: start with unhealthy (predecessor after successor)
        var now = _dateTimeProvider.Now;
        var today = _dateTimeProvider.Today;

        var source = _workItemFaker.WithProposedState().Generate();
        var target = _workItemFaker.WithStatusCategory(StatusCategory.Proposed).Generate();
        var sourcePlannedOn = today.PlusDays(10);
        var targetPlannedOn = today.PlusDays(5);
        var dep = _dependencyFaker.WithSource(source).WithTarget(target).WithSourcePlannedOn(sourcePlannedOn).WithTargetPlannedOn(targetPlannedOn).WithCreatedOn(now).Generate();

        Assert.Equal(DependencyPlanningHealth.Unhealthy, dep.Health);

        // Act: update source to earlier date but skip recalculation
        var newSourcePlannedOn = today.PlusDays(1);
        var sourceInfo = new DependencyWorkItemInfo
        {
            WorkItemId = source.Id,
            StatusCategory = StatusCategory.Proposed,
            PlannedOn = newSourcePlannedOn
        };
        dep.UpdateSourceInfo(sourceInfo, recalculate: false);

        // Assert: source is updated but health is NOT recalculated yet (still Unhealthy)
        Assert.Equal(newSourcePlannedOn, dep.SourcePlannedOn);
        Assert.Equal(DependencyPlanningHealth.Unhealthy, dep.Health);

        // Act: now manually recalculate
        dep.CalculateStateAndHealth();

        // Assert: health is now Healthy
        Assert.Equal(DependencyPlanningHealth.Healthy, dep.Health);
    }

    [Fact]
    public void UpdateTargetInfo_WithRecalculateFalse_DoesNotRecalculate()
    {
        // Arrange: start with unhealthy (predecessor after successor)
        var now = _dateTimeProvider.Now;
        var today = _dateTimeProvider.Today;

        var source = _workItemFaker.WithProposedState().Generate();
        var target = _workItemFaker.WithStatusCategory(StatusCategory.Proposed).Generate();
        var sourcePlannedOn = today.PlusDays(10);
        var targetPlannedOn = today.PlusDays(5);
        var dep = _dependencyFaker.WithSource(source).WithTarget(target).WithSourcePlannedOn(sourcePlannedOn).WithTargetPlannedOn(targetPlannedOn).WithCreatedOn(now).Generate();

        Assert.Equal(DependencyPlanningHealth.Unhealthy, dep.Health);

        // Act: update target to later date but skip recalculation
        var newTargetPlannedOn = today.PlusDays(15);
        var targetInfo = new DependencyWorkItemInfo
        {
            WorkItemId = target.Id,
            StatusCategory = StatusCategory.Proposed,
            PlannedOn = newTargetPlannedOn
        };
        dep.UpdateTargetInfo(targetInfo, recalculate: false);

        // Assert: target is updated but health is NOT recalculated yet (still Unhealthy)
        Assert.Equal(newTargetPlannedOn, dep.TargetPlannedOn);
        Assert.Equal(DependencyPlanningHealth.Unhealthy, dep.Health);

        // Act: now manually recalculate
        dep.CalculateStateAndHealth();

        // Assert: health is now Healthy
        Assert.Equal(DependencyPlanningHealth.Healthy, dep.Health);
    }

    [Fact]
    public void Create_WhenOneEndsSprintHasEndedInItsTeamsZone_CountsThatEndAsUnplanned()
    {
        // Arrange — 01:00 UTC on Sep 28 is still the evening of Sep 27 in Chicago and already Sep 28 in Tokyo,
        // so a sprint whose last planned day is Sep 27 is still on in Chicago and has ended in Tokyo
        var now = Instant.FromUtc(2026, 9, 28, 1, 0);
        var lastDay = new LocalDate(2026, 9, 27);

        var source = DependencyWorkItemInfo.Create(Guid.NewGuid(), StatusCategory.Proposed, lastDay, Chicago, now);
        var target = DependencyWorkItemInfo.Create(Guid.NewGuid(), StatusCategory.Proposed, lastDay, Tokyo, now);

        // Act
        var dep = WorkItemDependency.Create(source, target, now, null, null, null, null);

        // Assert — predecessor planned and successor unplanned -> Healthy
        Assert.Equal(lastDay, dep.SourcePlannedOn);
        Assert.Null(dep.TargetPlannedOn);
        Assert.Equal(DependencyPlanningHealth.Healthy, dep.Health);
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
        var today = _dateTimeProvider.Today;

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