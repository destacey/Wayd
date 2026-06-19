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
    public void Create_ShouldCreateProposedLifecycleWithoutPhases()
    {
        // Act
        var lifecycle = ProjectLifecycle.Create("Standard Waterfall", "Classic lifecycle for traditional projects.");

        // Assert
        lifecycle.Should().NotBeNull();
        lifecycle.Name.Should().Be("Standard Waterfall");
        lifecycle.Description.Should().Be("Classic lifecycle for traditional projects.");
        lifecycle.State.Should().Be(ProjectLifecycleState.Proposed);
        lifecycle.Phases.Should().BeEmpty();
    }

    [Fact]
    public void Create_ShouldCreateProposedLifecycleWithPhases()
    {
        // Arrange
        var phases = new[]
        {
            ("Plan", "Define goals and timeline"),
            ("Execute", "Perform the work"),
            ("Deliver", "Release or complete outcome")
        };

        // Act
        var lifecycle = ProjectLifecycle.Create("Lightweight Project", "For smaller efforts.", phases);

        // Assert
        lifecycle.Should().NotBeNull();
        lifecycle.State.Should().Be(ProjectLifecycleState.Proposed);
        lifecycle.Phases.Should().HaveCount(3);
        lifecycle.Phases.Select(p => p.Name).Should().ContainInOrder("Plan", "Execute", "Deliver");
        lifecycle.Phases.Select(p => p.Order).Should().ContainInOrder(1, 2, 3);
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
        var lifecycle = _lifecycleFaker.AsActiveWithPhases(("Phase 1", "Description"));

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
        var lifecycle = _lifecycleFaker.AsActiveWithPhases(("Phase 1", "Description"));
        lifecycle.Archive();

        // Act
        var result = lifecycle.Update("New Name", "New Description");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion Update

    #region State Transitions

    [Fact]
    public void Activate_ShouldSucceed_WhenProposedWithPhases()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Test", "Description", [("Phase 1", "Description")]);

        // Act
        var result = lifecycle.Activate();

        // Assert
        result.IsSuccess.Should().BeTrue();
        lifecycle.State.Should().Be(ProjectLifecycleState.Active);
    }

    [Fact]
    public void Activate_ShouldFail_WhenProposedWithoutPhases()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Test", "Description");

        // Act
        var result = lifecycle.Activate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("at least one phase");
    }

    [Fact]
    public void Activate_ShouldFail_WhenAlreadyActive()
    {
        // Arrange
        var lifecycle = _lifecycleFaker.AsActiveWithPhases(("Phase 1", "Description"));

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
        var lifecycle = _lifecycleFaker.AsActiveWithPhases(("Phase 1", "Description"));
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
        var lifecycle = _lifecycleFaker.AsActiveWithPhases(("Phase 1", "Description"));

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
        var lifecycle = _lifecycleFaker.AsActiveWithPhases(("Phase 1", "Description"));
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
        var lifecycle = _lifecycleFaker.AsActiveWithPhases(("Phase 1", "Description"));

        // Act & Assert
        lifecycle.CanBeDeleted().Should().BeFalse();
    }

    [Fact]
    public void CanBeDeleted_ShouldReturnFalse_WhenArchived()
    {
        // Arrange
        var lifecycle = _lifecycleFaker.AsActiveWithPhases(("Phase 1", "Description"));
        lifecycle.Archive();

        // Act & Assert
        lifecycle.CanBeDeleted().Should().BeFalse();
    }

    #endregion CanBeDeleted

    #region AddPhase

    [Fact]
    public void AddPhase_ShouldSucceed_WhenProposed()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Test", "Description");

        // Act
        var result = lifecycle.AddPhase("Initiation", "Define business case and project charter");

        // Assert
        result.IsSuccess.Should().BeTrue();
        lifecycle.Phases.Should().HaveCount(1);
        lifecycle.Phases.First().Name.Should().Be("Initiation");
        lifecycle.Phases.First().Order.Should().Be(1);
    }

    [Fact]
    public void AddPhase_ShouldAutoCalculateOrder()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Test", "Description");

        // Act
        lifecycle.AddPhase("Phase 1", "First phase");
        lifecycle.AddPhase("Phase 2", "Second phase");
        lifecycle.AddPhase("Phase 3", "Third phase");

        // Assert
        lifecycle.Phases.Should().HaveCount(3);
        lifecycle.Phases.Select(p => p.Order).Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public void AddPhase_ShouldFail_WhenActive()
    {
        // Arrange
        var lifecycle = _lifecycleFaker.AsActiveWithPhases(("Phase 1", "Description"));

        // Act
        var result = lifecycle.AddPhase("New Phase", "Description");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("proposed");
    }

    [Fact]
    public void AddPhase_ShouldFail_WhenArchived()
    {
        // Arrange
        var lifecycle = _lifecycleFaker.AsActiveWithPhases(("Phase 1", "Description"));
        lifecycle.Archive();

        // Act
        var result = lifecycle.AddPhase("New Phase", "Description");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion AddPhase

    #region UpdatePhase

    [Fact]
    public void UpdatePhase_ShouldSucceed_WhenProposed()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Test", "Description", [("Old Name", "Old Description")]);
        var phaseId = lifecycle.Phases.First().Id;

        // Act
        var result = lifecycle.UpdatePhase(phaseId, "New Name", "New Description");

        // Assert
        result.IsSuccess.Should().BeTrue();
        lifecycle.Phases.First().Name.Should().Be("New Name");
        lifecycle.Phases.First().Description.Should().Be("New Description");
    }

    [Fact]
    public void UpdatePhase_ShouldFail_WhenPhaseNotFound()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Test", "Description", [("Phase 1", "Description")]);

        // Act
        var result = lifecycle.UpdatePhase(Guid.NewGuid(), "New Name", "New Description");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public void UpdatePhase_ShouldFail_WhenActive()
    {
        // Arrange
        var lifecycle = _lifecycleFaker.AsActiveWithPhases(("Phase 1", "Description"));
        var phaseId = lifecycle.Phases.First().Id;

        // Act
        var result = lifecycle.UpdatePhase(phaseId, "New Name", "New Description");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion UpdatePhase

    #region RemovePhase

    [Fact]
    public void RemovePhase_ShouldSucceed_WhenProposed()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Test", "Description",
        [
            ("Phase 1", "First"),
            ("Phase 2", "Second"),
            ("Phase 3", "Third")
        ]);
        var phaseToRemove = lifecycle.Phases.First(p => p.Name == "Phase 2");

        // Act
        var result = lifecycle.RemovePhase(phaseToRemove.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        lifecycle.Phases.Should().HaveCount(2);
        lifecycle.Phases.Select(p => p.Name).Should().ContainInOrder("Phase 1", "Phase 3");
        lifecycle.Phases.Select(p => p.Order).Should().ContainInOrder(1, 2);
    }

    [Fact]
    public void RemovePhase_ShouldFail_WhenPhaseNotFound()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Test", "Description", [("Phase 1", "Description")]);

        // Act
        var result = lifecycle.RemovePhase(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public void RemovePhase_ShouldFail_WhenActive()
    {
        // Arrange
        var lifecycle = _lifecycleFaker.AsActiveWithPhases(("Phase 1", "Description"));
        var phaseId = lifecycle.Phases.First().Id;

        // Act
        var result = lifecycle.RemovePhase(phaseId);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion RemovePhase

    #region ReorderPhases

    [Fact]
    public void ReorderPhases_ShouldSucceed_WhenProposed()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Test", "Description",
        [
            ("Phase A", "First"),
            ("Phase B", "Second"),
            ("Phase C", "Third")
        ]);
        var phases = lifecycle.Phases.OrderBy(p => p.Order).ToList();
        var reorderedIds = new List<Guid> { phases[2].Id, phases[0].Id, phases[1].Id }; // C, A, B

        // Act
        var result = lifecycle.ReorderPhases(reorderedIds);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var orderedPhases = lifecycle.Phases.OrderBy(p => p.Order).ToList();
        orderedPhases[0].Name.Should().Be("Phase C");
        orderedPhases[1].Name.Should().Be("Phase A");
        orderedPhases[2].Name.Should().Be("Phase B");
    }

    [Fact]
    public void ReorderPhases_ShouldFail_WhenCountMismatch()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Test", "Description",
        [
            ("Phase A", "First"),
            ("Phase B", "Second")
        ]);
        var partialIds = new List<Guid> { lifecycle.Phases.First().Id };

        // Act
        var result = lifecycle.ReorderPhases(partialIds);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("number of phase IDs");
    }

    [Fact]
    public void ReorderPhases_ShouldFail_WhenDuplicateIds()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Test", "Description",
        [
            ("Phase A", "First"),
            ("Phase B", "Second")
        ]);
        var firstPhaseId = lifecycle.Phases.First().Id;
        var duplicateIds = new List<Guid> { firstPhaseId, firstPhaseId };

        // Act
        var result = lifecycle.ReorderPhases(duplicateIds);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Duplicate");
    }

    [Fact]
    public void ReorderPhases_ShouldFail_WhenPhaseIdNotFound()
    {
        // Arrange
        var lifecycle = ProjectLifecycle.Create("Test", "Description",
        [
            ("Phase A", "First"),
            ("Phase B", "Second")
        ]);
        var invalidIds = new List<Guid> { lifecycle.Phases.First().Id, Guid.NewGuid() };

        // Act
        var result = lifecycle.ReorderPhases(invalidIds);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public void ReorderPhases_ShouldFail_WhenActive()
    {
        // Arrange
        var lifecycle = _lifecycleFaker.AsActiveWithPhases(("Phase 1", "First"), ("Phase 2", "Second"));
        var phases = lifecycle.Phases.OrderBy(p => p.Order).ToList();
        var reorderedIds = new List<Guid> { phases[1].Id, phases[0].Id };

        // Act
        var result = lifecycle.ReorderPhases(reorderedIds);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion ReorderPhases
}
