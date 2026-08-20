using FluentAssertions;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Sut.Models;

public class ProjectLifecycleTests
{
    private readonly ProjectLifecycleFaker _lifecycleFaker;

    public ProjectLifecycleTests()
    {
        _lifecycleFaker = new ProjectLifecycleFaker();
    }

    #region Create

    [Fact]
    public void Create_ShouldCreateProposedLifecycleWithoutStages()
    {
        // Act
        var lifecycle = ProjectLifecycle.Create("Standard Waterfall", "Classic lifecycle for traditional projects.");

        // Assert
        lifecycle.Should().NotBeNull();
        lifecycle.Name.Should().Be("Standard Waterfall");
        lifecycle.Description.Should().Be("Classic lifecycle for traditional projects.");
        lifecycle.State.Should().Be(ProjectLifecycleState.Proposed);
        lifecycle.Stages.Should().BeEmpty();
    }

    [Fact]
    public void Create_ShouldCreateProposedLifecycleWithStages()
    {
        // Arrange
        var stages = new[]
        {
            ("Plan", "Define goals and timeline"),
            ("Execute", "Perform the work"),
            ("Deliver", "Release or complete outcome")
        };

        // Act
        var lifecycle = ProjectLifecycle.Create("Lightweight Project", "For smaller efforts.", stages);

        // Assert
        lifecycle.Should().NotBeNull();
        lifecycle.State.Should().Be(ProjectLifecycleState.Proposed);
        lifecycle.Stages.Should().HaveCount(3);
        lifecycle.Stages.Select(p => p.Name).Should().ContainInOrder("Plan", "Execute", "Deliver");
        lifecycle.Stages.Select(p => p.Order).Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public void Create_ShouldTrimNameAndDescription()
    {
        // Act
        var lifecycle = ProjectLifecycle.Create("  Standard Waterfall  ", "  Description with spaces  ");

        // Assert
        lifecycle.Name.Should().Be("Standard Waterfall");
        lifecycle.Description.Should().Be("Description with spaces");
    }

    [Fact]
    public void Create_ShouldThrow_WhenNameIsEmpty()
    {
        // Act
        var act = () => ProjectLifecycle.Create("", "Description");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenDescriptionIsEmpty()
    {
        // Act
        var act = () => ProjectLifecycle.Create("Name", "");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion Create

    #region Update

    [Fact]
    public void Update_ShouldSucceed_WhenProposed()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Old Name", "Old Description");

        // Act
        var result = lifecycle.Update("New Name", "New Description");

        // Assert
        result.IsSuccess.Should().BeTrue();
        lifecycle.Name.Should().Be("New Name");
        lifecycle.Description.Should().Be("New Description");
    }

    [Fact]
    public void Update_ShouldFail_WhenActive()
    {
        // Arrange
        var lifecycle = _lifecycleFaker.AsActiveWithStages(("Stage 1", "Description"));

        // Act
        var result = lifecycle.Update("New Name", "New Description");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("proposed");
    }

    [Fact]
    public void Update_ShouldFail_WhenArchived()
    {
        // Arrange
        var lifecycle = _lifecycleFaker.AsActiveWithStages(("Stage 1", "Description"));
        lifecycle.Archive();

        // Act
        var result = lifecycle.Update("New Name", "New Description");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion Update

    #region State Transitions

    [Fact]
    public void Activate_ShouldSucceed_WhenProposedWithStages()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Test", "Description", [("Stage 1", "Description")]);

        // Act
        var result = lifecycle.Activate();

        // Assert
        result.IsSuccess.Should().BeTrue();
        lifecycle.State.Should().Be(ProjectLifecycleState.Active);
    }

    [Fact]
    public void Activate_ShouldFail_WhenProposedWithoutStages()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Test", "Description");

        // Act
        var result = lifecycle.Activate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("at least one stage");
    }

    [Fact]
    public void Activate_ShouldFail_WhenAlreadyActive()
    {
        // Arrange
        var lifecycle = _lifecycleFaker.AsActiveWithStages(("Stage 1", "Description"));

        // Act
        var result = lifecycle.Activate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("proposed");
    }

    [Fact]
    public void Activate_ShouldFail_WhenArchived()
    {
        // Arrange
        var lifecycle = _lifecycleFaker.AsActiveWithStages(("Stage 1", "Description"));
        lifecycle.Archive();

        // Act
        var result = lifecycle.Activate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Archive_ShouldSucceed_WhenActive()
    {
        // Arrange
        var lifecycle = _lifecycleFaker.AsActiveWithStages(("Stage 1", "Description"));

        // Act
        var result = lifecycle.Archive();

        // Assert
        result.IsSuccess.Should().BeTrue();
        lifecycle.State.Should().Be(ProjectLifecycleState.Archived);
    }

    [Fact]
    public void Archive_ShouldFail_WhenProposed()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Test", "Description");

        // Act
        var result = lifecycle.Archive();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("active");
    }

    [Fact]
    public void Archive_ShouldFail_WhenAlreadyArchived()
    {
        // Arrange
        var lifecycle = _lifecycleFaker.AsActiveWithStages(("Stage 1", "Description"));
        lifecycle.Archive();

        // Act
        var result = lifecycle.Archive();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion State Transitions

    #region CanBeDeleted

    [Fact]
    public void CanBeDeleted_ShouldReturnTrue_WhenProposed()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Test", "Description");

        // Act & Assert
        lifecycle.CanBeDeleted().Should().BeTrue();
    }

    [Fact]
    public void CanBeDeleted_ShouldReturnFalse_WhenActive()
    {
        // Arrange
        var lifecycle = _lifecycleFaker.AsActiveWithStages(("Stage 1", "Description"));

        // Act & Assert
        lifecycle.CanBeDeleted().Should().BeFalse();
    }

    [Fact]
    public void CanBeDeleted_ShouldReturnFalse_WhenArchived()
    {
        // Arrange
        var lifecycle = _lifecycleFaker.AsActiveWithStages(("Stage 1", "Description"));
        lifecycle.Archive();

        // Act & Assert
        lifecycle.CanBeDeleted().Should().BeFalse();
    }

    #endregion CanBeDeleted

    #region AddStage

    [Fact]
    public void AddStage_ShouldSucceed_WhenProposed()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Test", "Description");

        // Act
        var result = lifecycle.AddStage("Initiation", "Define business case and project charter");

        // Assert
        result.IsSuccess.Should().BeTrue();
        lifecycle.Stages.Should().HaveCount(1);
        lifecycle.Stages.First().Name.Should().Be("Initiation");
        lifecycle.Stages.First().Order.Should().Be(1);
    }

    [Fact]
    public void AddStage_ShouldAutoCalculateOrder()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Test", "Description");

        // Act
        lifecycle.AddStage("Stage 1", "First stage");
        lifecycle.AddStage("Stage 2", "Second stage");
        lifecycle.AddStage("Stage 3", "Third stage");

        // Assert
        lifecycle.Stages.Should().HaveCount(3);
        lifecycle.Stages.Select(p => p.Order).Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public void AddStage_ShouldFail_WhenActive()
    {
        // Arrange
        var lifecycle = _lifecycleFaker.AsActiveWithStages(("Stage 1", "Description"));

        // Act
        var result = lifecycle.AddStage("New Stage", "Description");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("proposed");
    }

    [Fact]
    public void AddStage_ShouldFail_WhenArchived()
    {
        // Arrange
        var lifecycle = _lifecycleFaker.AsActiveWithStages(("Stage 1", "Description"));
        lifecycle.Archive();

        // Act
        var result = lifecycle.AddStage("New Stage", "Description");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion AddStage

    #region UpdateStage

    [Fact]
    public void UpdateStage_ShouldSucceed_WhenProposed()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Test", "Description", [("Old Name", "Old Description")]);
        var stageId = lifecycle.Stages.First().Id;

        // Act
        var result = lifecycle.UpdateStage(stageId, "New Name", "New Description");

        // Assert
        result.IsSuccess.Should().BeTrue();
        lifecycle.Stages.First().Name.Should().Be("New Name");
        lifecycle.Stages.First().Description.Should().Be("New Description");
    }

    [Fact]
    public void UpdateStage_ShouldFail_WhenStageNotFound()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Test", "Description", [("Stage 1", "Description")]);

        // Act
        var result = lifecycle.UpdateStage(Guid.NewGuid(), "New Name", "New Description");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public void UpdateStage_ShouldFail_WhenActive()
    {
        // Arrange
        var lifecycle = _lifecycleFaker.AsActiveWithStages(("Stage 1", "Description"));
        var stageId = lifecycle.Stages.First().Id;

        // Act
        var result = lifecycle.UpdateStage(stageId, "New Name", "New Description");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion UpdateStage

    #region RemoveStage

    [Fact]
    public void RemoveStage_ShouldSucceed_WhenProposed()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Test", "Description",
        [
            ("Stage 1", "First"),
            ("Stage 2", "Second"),
            ("Stage 3", "Third")
        ]);
        var stageToRemove = lifecycle.Stages.First(p => p.Name == "Stage 2");

        // Act
        var result = lifecycle.RemoveStage(stageToRemove.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        lifecycle.Stages.Should().HaveCount(2);
        lifecycle.Stages.Select(p => p.Name).Should().ContainInOrder("Stage 1", "Stage 3");
        lifecycle.Stages.Select(p => p.Order).Should().ContainInOrder(1, 2);
    }

    [Fact]
    public void RemoveStage_ShouldFail_WhenStageNotFound()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Test", "Description", [("Stage 1", "Description")]);

        // Act
        var result = lifecycle.RemoveStage(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public void RemoveStage_ShouldFail_WhenActive()
    {
        // Arrange
        var lifecycle = _lifecycleFaker.AsActiveWithStages(("Stage 1", "Description"));
        var stageId = lifecycle.Stages.First().Id;

        // Act
        var result = lifecycle.RemoveStage(stageId);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion RemoveStage

    #region ReorderStages

    [Fact]
    public void ReorderStages_ShouldSucceed_WhenProposed()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Test", "Description",
        [
            ("Stage A", "First"),
            ("Stage B", "Second"),
            ("Stage C", "Third")
        ]);
        var stages = lifecycle.Stages.OrderBy(p => p.Order).ToList();
        var reorderedIds = new List<Guid> { stages[2].Id, stages[0].Id, stages[1].Id }; // C, A, B

        // Act
        var result = lifecycle.ReorderStages(reorderedIds);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var orderedStages = lifecycle.Stages.OrderBy(p => p.Order).ToList();
        orderedStages[0].Name.Should().Be("Stage C");
        orderedStages[1].Name.Should().Be("Stage A");
        orderedStages[2].Name.Should().Be("Stage B");
    }

    [Fact]
    public void ReorderStages_ShouldFail_WhenCountMismatch()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Test", "Description",
        [
            ("Stage A", "First"),
            ("Stage B", "Second")
        ]);
        var partialIds = new List<Guid> { lifecycle.Stages.First().Id };

        // Act
        var result = lifecycle.ReorderStages(partialIds);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("number of stage IDs");
    }

    [Fact]
    public void ReorderStages_ShouldFail_WhenDuplicateIds()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Test", "Description",
        [
            ("Stage A", "First"),
            ("Stage B", "Second")
        ]);
        var firstStageId = lifecycle.Stages.First().Id;
        var duplicateIds = new List<Guid> { firstStageId, firstStageId };

        // Act
        var result = lifecycle.ReorderStages(duplicateIds);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Duplicate");
    }

    [Fact]
    public void ReorderStages_ShouldFail_WhenStageIdNotFound()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Test", "Description",
        [
            ("Stage A", "First"),
            ("Stage B", "Second")
        ]);
        var invalidIds = new List<Guid> { lifecycle.Stages.First().Id, Guid.NewGuid() };

        // Act
        var result = lifecycle.ReorderStages(invalidIds);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public void ReorderStages_ShouldFail_WhenActive()
    {
        // Arrange
        var lifecycle = _lifecycleFaker.AsActiveWithStages(("Stage 1", "First"), ("Stage 2", "Second"));
        var stages = lifecycle.Stages.OrderBy(p => p.Order).ToList();
        var reorderedIds = new List<Guid> { stages[1].Id, stages[0].Id };

        // Act
        var result = lifecycle.ReorderStages(reorderedIds);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion ReorderStages
}
