using FluentAssertions;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Sut.Models;

public class ProjectTaskTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;

    public ProjectTaskTests()
    {
        _dateTimeProvider = new TestingDateTimeProvider(new FakeClock(DateTime.UtcNow.ToInstant()));
    }

    #region UpdateProjectKey Tests

    [Fact]
    public void UpdateProjectKey_ShouldUpdateTaskKeyAndPreserveNumber()
    {
        // Arrange
        var originalProjectKey = new ProjectKey("ORIG");
        var taskNumber = 123;
        var task = new ProjectTaskFaker().WithKey(new ProjectTaskKey(originalProjectKey, taskNumber)).Generate();

        var newProjectKey = new ProjectKey("NEWKEY");

        // Act
        var result = task.UpdateProjectKey(newProjectKey);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task.Number.Should().Be(taskNumber);
        task.Key.Value.Should().Be($"{newProjectKey.Value}-{taskNumber}");
    }

    [Fact]
    public void UpdateProjectKey_ShouldBeNoOp_WhenProjectKeyIsUnchanged()
    {
        // Arrange
        var projectKey = new ProjectKey("SAME");
        var taskNumber = 7;
        var task = new ProjectTaskFaker().WithKey(new ProjectTaskKey(projectKey, taskNumber)).Generate();
        var originalTaskKeyValue = task.Key.Value;

        // Act
        var result = task.UpdateProjectKey(projectKey);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task.Key.Value.Should().Be(originalTaskKeyValue);
    }

    #endregion UpdateProjectKey Tests

    #region ChangeOrder Tests

    [Fact]
    public void ChangeOrder_ShouldSucceed_WhenOrderIsValid()
    {
        // Arrange
        var task = new ProjectTaskFaker().Generate();

        // Act
        var result = task.ChangeOrder(5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task.Order.Should().Be(5);
    }

    [Fact]
    public void ChangeOrder_ShouldFail_WhenOrderIsZero()
    {
        // Arrange
        var task = new ProjectTaskFaker().Generate();

        // Act
        var result = task.ChangeOrder(0);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Order must be greater than 0.");
    }

    [Fact]
    public void ChangeOrder_ShouldFail_WhenOrderIsNegative()
    {
        // Arrange
        var task = new ProjectTaskFaker().Generate();

        // Act
        var result = task.ChangeOrder(-5);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Order must be greater than 0.");
    }

    [Fact]
    public void ChangeOrder_ShouldAllowUpdatingMultipleTimes()
    {
        // Arrange
        var task = new ProjectTaskFaker().Generate();

        // Act
        var result1 = task.ChangeOrder(5);
        var result2 = task.ChangeOrder(10);
        var result3 = task.ChangeOrder(3);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        result3.IsSuccess.Should().BeTrue();
        task.Order.Should().Be(3);
    }

    #endregion ChangeOrder Tests

    #region ChangeParent Tests

    [Fact]
    public void ChangeParent_ShouldSucceed_WhenSettingNewParent()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var parentTaskId = Guid.NewGuid();
        var childTaskId = Guid.NewGuid();

        var parentTask = new ProjectTaskFaker().WithId(parentTaskId).WithProjectId(projectId).Generate();
        var childTask = new ProjectTaskFaker().WithId(childTaskId).WithProjectId(projectId).Generate();

        // Act
        var result = childTask.ChangeParent(parentTask.Id, 1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        childTask.ParentId.Should().Be(parentTask.Id);
        childTask.Order.Should().Be(1);
    }

    [Fact]
    public void ChangeParent_ShouldSucceed_WhenRemovingParent()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var childTask = new ProjectTaskFaker().WithProjectId(projectId).WithParentId(parentId).Generate();

        // Act
        var result = childTask.ChangeParent(null, 1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        childTask.ParentId.Should().BeNull();
        childTask.Order.Should().Be(1);
    }

    [Fact]
    public void ChangeParent_ShouldSucceed_WhenChangingToAnotherParent()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var originalParentId = Guid.NewGuid();
        var newParentId = Guid.NewGuid();
        var childTask = new ProjectTaskFaker().WithProjectId(projectId).WithParentId(originalParentId).Generate();

        // Act
        var result = childTask.ChangeParent(newParentId, 3);

        // Assert
        result.IsSuccess.Should().BeTrue();
        childTask.ParentId.Should().Be(newParentId);
        childTask.Order.Should().Be(3);
    }

    [Fact]
    public void ChangeParent_ShouldFail_WhenSettingSelfAsParent()
    {
        // Arrange
        var task = new ProjectTaskFaker().Generate();

        // Act
        var result = task.ChangeParent(task.Id, 1);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A task cannot be its own parent.");
    }

    [Fact]
    public void ChangeParent_ShouldFail_WhenNewParentIsDirectChild()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var parentTaskId = Guid.NewGuid();
        var childTaskId = Guid.NewGuid();

        var parentTask = new ProjectTaskFaker().WithId(parentTaskId).WithProjectId(projectId).Generate();
        var childTask = new ProjectTaskFaker().WithId(childTaskId).WithProjectId(projectId).WithParentId(parentTaskId).Generate();

        parentTask.AddChild(childTask);

        // Act
        var result = parentTask.ChangeParent(childTask.Id, 1);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A task cannot be moved under one of its descendants.");
    }

    [Fact]
    public void ChangeParent_ShouldFail_WhenNewParentIsGrandchild()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var grandparentId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();

        var grandparentTask = new ProjectTaskFaker().WithId(grandparentId).WithProjectId(projectId).Generate();
        var parentTask = new ProjectTaskFaker().WithId(parentId).WithProjectId(projectId).WithParentId(grandparentId).Generate();
        var childTask = new ProjectTaskFaker().WithId(childId).WithProjectId(projectId).WithParentId(parentId).Generate();

        grandparentTask.AddChild(parentTask);
        parentTask.AddChild(childTask);

        // Act
        var result = grandparentTask.ChangeParent(childTask.Id, 1);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A task cannot be moved under one of its descendants.");
    }

    [Fact]
    public void ChangeParent_ShouldFail_WhenNewParentIsDeepDescendant()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var rootId = Guid.NewGuid();
        var level1Id = Guid.NewGuid();
        var level2Id = Guid.NewGuid();
        var level3Id = Guid.NewGuid();

        var rootTask = new ProjectTaskFaker().WithId(rootId).WithProjectId(projectId).Generate();
        var level1Task = new ProjectTaskFaker().WithId(level1Id).WithProjectId(projectId).WithParentId(rootId).Generate();
        var level2Task = new ProjectTaskFaker().WithId(level2Id).WithProjectId(projectId).WithParentId(level1Id).Generate();
        var level3Task = new ProjectTaskFaker().WithId(level3Id).WithProjectId(projectId).WithParentId(level2Id).Generate();

        rootTask.AddChild(level1Task);
        level1Task.AddChild(level2Task);
        level2Task.AddChild(level3Task);

        // Act
        var result = rootTask.ChangeParent(level3Task.Id, 1);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A task cannot be moved under one of its descendants.");
    }

    [Fact]
    public void ChangeParent_ShouldSucceed_WhenMovingToSibling()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var sibling1Id = Guid.NewGuid();
        var sibling2Id = Guid.NewGuid();

        var parentTask = new ProjectTaskFaker().WithId(parentId).WithProjectId(projectId).Generate();
        var sibling1 = new ProjectTaskFaker().WithId(sibling1Id).WithProjectId(projectId).WithParentId(parentId).WithOrder(1).Generate();
        var sibling2 = new ProjectTaskFaker().WithId(sibling2Id).WithProjectId(projectId).WithParentId(parentId).WithOrder(2).Generate();

        parentTask.AddChild(sibling1);
        parentTask.AddChild(sibling2);

        // Act
        var result = sibling1.ChangeParent(sibling2.Id, 1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sibling1.ParentId.Should().Be(sibling2.Id);
        sibling1.Order.Should().Be(1);
    }

    [Fact]
    public void ChangeParent_ShouldSucceed_WhenMovingToUncle()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var grandparentId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var uncleId = Guid.NewGuid();
        var childId = Guid.NewGuid();

        var grandparent = new ProjectTaskFaker().WithId(grandparentId).WithProjectId(projectId).Generate();
        var parent = new ProjectTaskFaker().WithId(parentId).WithProjectId(projectId).WithParentId(grandparentId).Generate();
        var uncle = new ProjectTaskFaker().WithId(uncleId).WithProjectId(projectId).WithParentId(grandparentId).Generate();
        var child = new ProjectTaskFaker().WithId(childId).WithProjectId(projectId).WithParentId(parentId).Generate();

        grandparent.AddChild(parent);
        grandparent.AddChild(uncle);
        parent.AddChild(child);

        // Act
        var result = child.ChangeParent(uncle.Id, 1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        child.ParentId.Should().Be(uncle.Id);
    }

    [Fact]
    public void ChangeParent_ShouldFail_WhenOrderIsZero()
    {
        // Arrange
        var task = new ProjectTaskFaker().Generate();
        var newParentId = Guid.NewGuid();

        // Act
        var result = task.ChangeParent(newParentId, 0);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Order must be greater than 0.");
    }

    [Fact]
    public void ChangeParent_ShouldFail_WhenOrderIsNegative()
    {
        // Arrange
        var task = new ProjectTaskFaker().Generate();
        var newParentId = Guid.NewGuid();

        // Act
        var result = task.ChangeParent(newParentId, -5);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Order must be greater than 0.");
    }

    [Fact]
    public void ChangeParent_ShouldUpdateOrder_WhenChangingParent()
    {
        // Arrange
        var task = new ProjectTaskFaker().WithOrder(5).Generate();
        var newParentId = Guid.NewGuid();

        // Act
        var result = task.ChangeParent(newParentId, 10);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task.ParentId.Should().Be(newParentId);
        task.Order.Should().Be(10);
    }

    [Fact]
    public void ChangeParent_ShouldUpdateOrder_WhenRemovingParent()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var task = new ProjectTaskFaker().WithParentId(parentId).WithOrder(3).Generate();

        // Act
        var result = task.ChangeParent(null, 7);

        // Assert
        result.IsSuccess.Should().BeTrue();
        task.ParentId.Should().BeNull();
        task.Order.Should().Be(7);
    }

    [Fact]
    public void ChangeParent_ShouldSetParentId_EvenWhenOrderValidationFails()
    {
        // Arrange
        var originalParentId = Guid.NewGuid();
        var newParentId = Guid.NewGuid();
        var task = new ProjectTaskFaker().WithParentId(originalParentId).WithOrder(5).Generate();

        // Act
        var result = task.ChangeParent(newParentId, 0);

        // Assert
        result.IsFailure.Should().BeTrue();
        // Note: In current implementation, ParentId is set before order validation
        // This test documents current behavior - ParentId IS changed even when order fails
        task.ParentId.Should().Be(newParentId);
    }

    #endregion ChangeParent Tests

    #region Date Rollup and Shifting Tests

    [Fact]
    public void RecalculateDatesFromChildren_ShouldExpandParentTask_ToFitDatedChildren()
    {
        // Arrange
        var parentTask = new ProjectTaskFaker().Generate();
        var childRange = new FlexibleDateRange(new LocalDate(2026, 6, 5), new LocalDate(2026, 6, 12));
        var childTask = new ProjectTaskFaker().WithPlannedDateRange(childRange).Generate();
        parentTask.AddChild(childTask);

        // Act
        parentTask.RecalculateDatesFromChildren();

        // Assert
        parentTask.PlannedDateRange.Should().NotBeNull();
        parentTask.PlannedDateRange!.Start.Should().Be(new LocalDate(2026, 6, 5));
        parentTask.PlannedDateRange.End.Should().Be(new LocalDate(2026, 6, 12));
    }

    [Fact]
    public void ShiftDates_ShouldOffsetParentAndChildren_BySpecifiedDays()
    {
        // Arrange
        var parentRange = new FlexibleDateRange(new LocalDate(2026, 6, 5), new LocalDate(2026, 6, 12));
        var parentTask = new ProjectTaskFaker().WithPlannedDateRange(parentRange).Generate();

        var childRange = new FlexibleDateRange(new LocalDate(2026, 6, 7), new LocalDate(2026, 6, 10));
        var childTask = new ProjectTaskFaker().WithPlannedDateRange(childRange).Generate();
        parentTask.AddChild(childTask);

        // Act
        parentTask.ShiftDates(5);

        // Assert
        parentTask.PlannedDateRange!.Start.Should().Be(new LocalDate(2026, 6, 10));
        parentTask.PlannedDateRange.End.Should().Be(new LocalDate(2026, 6, 17));

        childTask.PlannedDateRange!.Start.Should().Be(new LocalDate(2026, 6, 12));
        childTask.PlannedDateRange.End.Should().Be(new LocalDate(2026, 6, 15));
    }

    [Fact]
    public void ApplyPlannedDates_ShouldFail_WhenProposedRangeExcludesChild()
    {
        // Arrange
        var parentRange = new FlexibleDateRange(new LocalDate(2026, 6, 5), new LocalDate(2026, 6, 12));
        var parentTask = new ProjectTaskFaker().WithPlannedDateRange(parentRange).Generate();

        var childRange = new FlexibleDateRange(new LocalDate(2026, 6, 5), new LocalDate(2026, 6, 10));
        var childTask = new ProjectTaskFaker().WithPlannedDateRange(childRange).Generate();
        parentTask.AddChild(childTask);

        // Act
        var shrunkRange = new FlexibleDateRange(new LocalDate(2026, 6, 6), new LocalDate(2026, 6, 12)); // Excludes child start on June 5
        var result = parentTask.ApplyPlannedDates(shrunkRange, null, false);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("falls outside the selected range");
    }

    [Fact]
    public void HasAnyDatedChildren_ShouldBeTrue_WhenChildHasPlannedRange()
    {
        // Arrange
        var parentTask = new ProjectTaskFaker().Generate();
        var childRange = new FlexibleDateRange(new LocalDate(2026, 6, 5), new LocalDate(2026, 6, 12));
        var childTask = new ProjectTaskFaker().WithPlannedDateRange(childRange).Generate();
        parentTask.AddChild(childTask);

        // Act
        var hasDatedChildren = parentTask.HasAnyDatedChildren();

        // Assert
        hasDatedChildren.Should().BeTrue();
    }

    #endregion Date Rollup and Shifting Tests
}