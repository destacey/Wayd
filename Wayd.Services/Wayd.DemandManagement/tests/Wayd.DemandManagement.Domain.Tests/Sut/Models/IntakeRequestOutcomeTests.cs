using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Domain.Tests.Sut.Models;

public sealed class IntakeRequestOutcomeTests
{
    private static readonly Instant At = Instant.FromUtc(2026, 1, 15, 9, 0, 0);

    [Fact]
    public void IsMet_WithNothingMapped_IsUnknown()
    {
        // Arrange

        // Act
        var met = IntakeRequestOutcome.IsMet([]);

        // Assert
        met.Should().BeNull();
    }

    [Fact]
    public void IsMet_WhenEveryMappedDeliverableIsAccepted_IsTrue()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        DemandProjectDeliverable[] mapped =
        [
            new(SourceProjectDeliverable.Accepted(projectId, "Portal"), At),
            new(SourceProjectDeliverable.Accepted(projectId, "Reports"), At),
        ];

        // Act
        var met = IntakeRequestOutcome.IsMet(mapped);

        // Assert
        met.Should().BeTrue();
    }

    [Theory]
    [InlineData(ProjectDeliverableStatusAlias.NotStarted)]
    [InlineData(ProjectDeliverableStatusAlias.InProgress)]
    [InlineData(ProjectDeliverableStatusAlias.Descoped)]
    [InlineData(ProjectDeliverableStatusAlias.None)]
    public void IsMet_WhileAnyMappedDeliverableIsNotAccepted_IsFalse(ProjectDeliverableStatusAlias other)
    {
        // Arrange
        var projectId = Guid.NewGuid();
        DemandProjectDeliverable[] mapped =
        [
            new(SourceProjectDeliverable.Accepted(projectId, "Portal"), At),
            new(SourceProjectDeliverable.Any(projectId, "Reports") with { StatusAlias = other }, At),
        ];

        // Act
        var met = IntakeRequestOutcome.IsMet(mapped);

        // Assert
        met.Should().BeFalse();
    }

    [Fact]
    public void ProjectDeliverySignal_ForANewProject_FollowsTheProjectsLifecycleEvenBesideDeliverables()
    {
        // Arrange — an accepted deliverable would read done if a new project rolled its deliverables up
        var project = Project(LifecycleCategory.Active);
        DemandProjectDeliverable[] deliverables = [new(SourceProjectDeliverable.Accepted(project.Id), At)];

        // Act
        var signal = IntakeRequestOutcome.ProjectDeliverySignal(IntakeRequestOutcomeKind.NewProject, project, deliverables);

        // Assert
        signal.Should().Be(DeliverySignal.InProgress);
    }

    [Fact]
    public void ProjectDeliverySignal_ForScopeWithMappedDeliverables_RollsThemUpRatherThanFollowingTheProject()
    {
        // Arrange
        var project = Project(LifecycleCategory.Completed);
        DemandProjectDeliverable[] mapped =
        [
            new(SourceProjectDeliverable.Accepted(project.Id, "Portal"), At),
            new(Deliverable(project.Id, "Reports", StatusCategory.Active, ProjectDeliverableStatusAlias.InProgress), At),
        ];

        // Act
        var signal = IntakeRequestOutcome.ProjectDeliverySignal(IntakeRequestOutcomeKind.ExistingProjectScope, project, mapped);

        // Assert
        signal.Should().Be(DeliverySignal.InProgress);
    }

    [Theory]
    [InlineData(StatusCategory.Done, ProjectDeliverableStatusAlias.Accepted, DeliverySignal.Done)]
    [InlineData(StatusCategory.Removed, ProjectDeliverableStatusAlias.Descoped, DeliverySignal.Cancelled)]
    public void ProjectDeliverySignal_ForScopeBesideADescopedDeliverable_ReportsWhatTheOtherReached(
        StatusCategory otherCategory, ProjectDeliverableStatusAlias other, DeliverySignal expected)
    {
        // Arrange
        var project = Project(LifecycleCategory.Active);
        DemandProjectDeliverable[] mapped =
        [
            new(Deliverable(project.Id, "Portal", StatusCategory.Removed, ProjectDeliverableStatusAlias.Descoped), At),
            new(Deliverable(project.Id, "Reports", otherCategory, other), At),
        ];

        // Act
        var signal = IntakeRequestOutcome.ProjectDeliverySignal(IntakeRequestOutcomeKind.ExistingProjectScope, project, mapped);

        // Assert
        signal.Should().Be(expected);
    }

    [Fact]
    public void ProjectDeliverySignal_ForScopeWithNoDeliverableOnTheProject_FollowsTheProjectsLifecycle()
    {
        // Arrange
        var project = Project(LifecycleCategory.Canceled);

        // Act
        var signal = IntakeRequestOutcome.ProjectDeliverySignal(IntakeRequestOutcomeKind.ExistingProjectScope, project, []);

        // Assert
        signal.Should().Be(DeliverySignal.Cancelled);
    }

    [Theory]
    [InlineData(IntakeRequestOutcomeKind.NewProject)]
    [InlineData(IntakeRequestOutcomeKind.ExistingProjectScope)]
    public void ProjectDeliverySignal_OnceTheProjectIsDeleted_IsNull(IntakeRequestOutcomeKind kind)
    {
        // Arrange — no copy of the project remains.

        // Act
        var signal = IntakeRequestOutcome.ProjectDeliverySignal(kind, null, []);

        // Assert
        signal.Should().BeNull();
    }

    [Fact]
    public void ProjectDeliverySignal_ForAWorkItem_Throws()
    {
        // Arrange
        var project = Project(LifecycleCategory.Active);

        // Act
        Action act = () => IntakeRequestOutcome.ProjectDeliverySignal(IntakeRequestOutcomeKind.WorkItem, project, []);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsScopeUndefined_ForScopeWithNoDeliverableOnTheProject_IsTrue()
    {
        // Arrange
        var project = Project(LifecycleCategory.Active);

        // Act
        var undefined = IntakeRequestOutcome.IsScopeUndefined(IntakeRequestOutcomeKind.ExistingProjectScope, project, []);

        // Assert
        undefined.Should().BeTrue();
    }

    [Fact]
    public void IsScopeUndefined_ForScopeWithADeliverableOnTheProject_IsFalse()
    {
        // Arrange
        var project = Project(LifecycleCategory.Active);
        DemandProjectDeliverable[] mapped = [new(SourceProjectDeliverable.Any(project.Id), At)];

        // Act
        var undefined = IntakeRequestOutcome.IsScopeUndefined(IntakeRequestOutcomeKind.ExistingProjectScope, project, mapped);

        // Assert
        undefined.Should().BeFalse();
    }

    [Fact]
    public void IsScopeUndefined_ForANewProject_IsFalse()
    {
        // Arrange
        var project = Project(LifecycleCategory.Active);

        // Act
        var undefined = IntakeRequestOutcome.IsScopeUndefined(IntakeRequestOutcomeKind.NewProject, project, []);

        // Assert
        undefined.Should().BeFalse();
    }

    [Fact]
    public void IsScopeUndefined_OnceTheProjectIsDeleted_IsFalse()
    {
        // Arrange — no copy of the project remains.

        // Act
        var undefined = IntakeRequestOutcome.IsScopeUndefined(IntakeRequestOutcomeKind.ExistingProjectScope, null, []);

        // Assert
        undefined.Should().BeFalse();
    }

    private static DemandProject Project(LifecycleCategory lifecycle) =>
        new(SourceProject.Any(lifecycle: lifecycle), At);

    private static SourceProjectDeliverable Deliverable(Guid projectId, string name, StatusCategory category, ProjectDeliverableStatusAlias alias) =>
        SourceProjectDeliverable.Any(projectId, name) with { StatusCategory = category, StatusAlias = alias };
}
