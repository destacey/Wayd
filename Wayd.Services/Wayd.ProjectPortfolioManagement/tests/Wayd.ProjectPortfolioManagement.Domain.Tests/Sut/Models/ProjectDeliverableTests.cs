using FluentAssertions;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data.Extensions;
using Wayd.Tests.Shared;
using static Wayd.ProjectPortfolioManagement.Domain.Tests.Data.Extensions.PpmActorDataExtensions;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Sut.Models;

public sealed class ProjectDeliverableTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));
    private readonly ProjectFaker _projectFaker = new();
    private readonly ProjectDeliverableStatuses _statuses = new();
    private readonly Guid _ownerId = Guid.NewGuid();

    private Instant Now => _dateTimeProvider.Now;

    private PpmActor Owner => _ownerId.AsActor();

    private Project ProposedProject() => _projectFaker.WithOwner(_ownerId).Generate();

    private Project ActiveProject() => _projectFaker.WithOwner(_ownerId).AsActive(_dateTimeProvider);

    private ProjectDeliverable Add(Project project, string name = "Customer portal")
    {
        var result = project.AddDeliverable(Owner, NoProjectAncestry(), name, null, null, null, null, _statuses.NotStarted, Now);
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        project.ClearDomainEvents();
        return result.Value;
    }

    private void Move(Project project, ProjectDeliverable deliverable, StatusRef status, string? reason = null)
    {
        var result = project.ChangeDeliverableStatus(Owner, NoProjectAncestry(), deliverable.Id, status, reason, Now);
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        project.ClearDomainEvents();
    }

    #region AddDeliverable

    [Fact]
    public void AddDeliverable_WhenAuthorized_AddsItNotStartedAndRaisesAddedEvent()
    {
        // Arrange
        var project = ProposedProject();
        var targetDate = new LocalDate(2027, 3, 31);
        var ownerId = Guid.NewGuid();

        // Act
        var result = project.AddDeliverable(Owner, NoProjectAncestry(), "  Customer portal ", "The portal", " Signed off by support ",
            ownerId, targetDate, _statuses.NotStarted, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var deliverable = project.Deliverables.Should().ContainSingle().Subject;
        deliverable.Name.Should().Be("Customer portal");
        deliverable.AcceptanceCriteria.Should().Be("Signed off by support");
        deliverable.Order.Should().Be(1);
        deliverable.OwnerId.Should().Be(ownerId);
        deliverable.TargetDate.Should().Be(targetDate);
        deliverable.StatusId.Should().Be(_statuses.NotStarted.StatusId);
        deliverable.StatusAlias.Should().Be(ProjectDeliverableStatusAlias.NotStarted);
        deliverable.IsOpen.Should().BeTrue();
        deliverable.StatusTransitions.Should().ContainSingle().Which.ToStatusId.Should().Be(_statuses.NotStarted.StatusId);

        var added = project.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProjectDeliverableAddedEvent>().Subject;
        added.DeliverableId.Should().Be(deliverable.Id);
        added.Name.Should().Be("Customer portal");
        added.Order.Should().Be(1);
        added.StatusAlias.Should().Be(ProjectDeliverableStatusAlias.NotStarted);
        project.Revision.Should().Be(1);
    }

    [Fact]
    public void AddDeliverable_WhenOthersExist_PlacesItLast()
    {
        // Arrange
        var project = ProposedProject();
        Add(project, "First");
        Add(project, "Second");

        // Act
        var result = project.AddDeliverable(Owner, NoProjectAncestry(), "Third", null, null, null, null, _statuses.NotStarted, Now);

        // Assert
        result.Value.Order.Should().Be(3);
    }

    [Fact]
    public void AddDeliverable_WhenActorDoesNotManageProject_FailsAndChangesNothing()
    {
        // Arrange
        var project = ProposedProject();

        // Act
        var result = project.AddDeliverable(AnUnauthorizedActor(), NoProjectAncestry(), "Portal", null, null, null, null, _statuses.NotStarted, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("owner or manager");
        project.Deliverables.Should().BeEmpty();
        project.DomainEvents.Should().BeEmpty();
        project.Revision.Should().Be(0);
    }

    [Fact]
    public void AddDeliverable_WhenProjectClosed_Fails()
    {
        // Arrange
        var project = _projectFaker.WithOwner(_ownerId).AsCompleted(_dateTimeProvider);

        // Act
        var result = project.AddDeliverable(Owner, NoProjectAncestry(), "Portal", null, null, null, null, _statuses.NotStarted, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("completed or canceled");
        project.Deliverables.Should().BeEmpty();
    }

    [Fact]
    public void AddDeliverable_WhenInitialStatusIsNotNotStarted_Throws()
    {
        // Arrange
        var project = ProposedProject();

        // Act
        var act = () => project.AddDeliverable(Owner, NoProjectAncestry(), "Portal", null, null, null, null, _statuses.InProgress, Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion AddDeliverable

    #region ImportDeliverable

    [Fact]
    public void ImportDeliverable_WhenProjectClosedAndStatusOpen_Fails()
    {
        // Arrange
        var project = _projectFaker.AsCompleted(_dateTimeProvider);

        // Act
        var result = project.ImportDeliverable(PpmActor.System, NoProjectAncestry(), "Portal", null, null, null, null, _statuses.InProgress, null, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("still open");
        project.Deliverables.Should().BeEmpty();
    }

    [Fact]
    public void ImportDeliverable_WhenProjectClosedAndAccepted_AddsItAcceptedBySystem()
    {
        // Arrange
        var project = _projectFaker.AsCompleted(_dateTimeProvider);

        // Act
        var result = project.ImportDeliverable(PpmActor.System, NoProjectAncestry(), "Portal", null, null, null, null, _statuses.Accepted, null, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.StatusAlias.Should().Be(ProjectDeliverableStatusAlias.Accepted);
        result.Value.AcceptedOn.Should().Be(Now);
        result.Value.AcceptedById.Should().BeNull();
        project.DomainEvents.OfType<ProjectDeliverableAddedEvent>().Single().StatusAlias.Should().Be(ProjectDeliverableStatusAlias.Accepted);
    }

    [Fact]
    public void ImportDeliverable_WhenDescopedWithoutReason_Fails()
    {
        // Arrange
        var project = _projectFaker.AsCompleted(_dateTimeProvider);

        // Act
        var result = project.ImportDeliverable(PpmActor.System, NoProjectAncestry(), "Portal", null, null, null, null, _statuses.Descoped, " ", Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("needs a reason");
        project.Deliverables.Should().BeEmpty();
    }

    #endregion ImportDeliverable

    #region UpdateDeliverable

    [Fact]
    public void UpdateDeliverable_WhenSomethingChanges_RaisesDetailsUpdatedWithBothEnds()
    {
        // Arrange
        var project = ActiveProject();
        var deliverable = Add(project, "Portal");
        var revision = project.Revision;
        var targetDate = new LocalDate(2027, 6, 30);

        // Act
        var result = project.UpdateDeliverable(Owner, NoProjectAncestry(), deliverable.Id, "Customer portal", null, "Support signs off", null, targetDate, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        deliverable.Name.Should().Be("Customer portal");
        deliverable.TargetDate.Should().Be(targetDate);

        var updated = project.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProjectDeliverableDetailsUpdatedEvent>().Subject;
        updated.Previous.Name.Should().Be("Portal");
        updated.Previous.TargetDate.Should().BeNull();
        updated.Details.Name.Should().Be("Customer portal");
        updated.Details.AcceptanceCriteria.Should().Be("Support signs off");
        project.Revision.Should().Be(revision + 1);
    }

    [Fact]
    public void UpdateDeliverable_WhenOnlyWhitespaceDiffers_RaisesNothing()
    {
        // Arrange
        var project = ActiveProject();
        var deliverable = Add(project, "Portal");
        var revision = project.Revision;

        // Act
        var result = project.UpdateDeliverable(Owner, NoProjectAncestry(), deliverable.Id, " Portal ", "  ", null, null, null, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.DomainEvents.Should().BeEmpty();
        project.Revision.Should().Be(revision);
    }

    [Fact]
    public void UpdateDeliverable_WhenNotOnProject_Fails()
    {
        // Arrange
        var project = ActiveProject();

        // Act
        var result = project.UpdateDeliverable(Owner, NoProjectAncestry(), Guid.NewGuid(), "Portal", null, null, null, null, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    #endregion UpdateDeliverable

    #region ChangeDeliverableStatus

    [Fact]
    public void ChangeDeliverableStatus_WhenAccepted_RecordsAcceptanceAndRaisesBothEnds()
    {
        // Arrange
        var project = ActiveProject();
        var deliverable = Add(project);
        Move(project, deliverable, _statuses.InProgress);

        // Act
        var result = project.ChangeDeliverableStatus(Owner, NoProjectAncestry(), deliverable.Id, _statuses.Accepted, null, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        deliverable.StatusAlias.Should().Be(ProjectDeliverableStatusAlias.Accepted);
        deliverable.IsOpen.Should().BeFalse();
        deliverable.AcceptedOn.Should().Be(Now);
        deliverable.AcceptedById.Should().Be(_ownerId);
        deliverable.StatusTransitions.Should().HaveCount(3);

        var changed = project.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProjectDeliverableStatusChangedEvent>().Subject;
        changed.FromStatusId.Should().Be(_statuses.InProgress.StatusId);
        changed.FromCategory.Should().Be(StatusCategory.Active);
        changed.ToStatusId.Should().Be(_statuses.Accepted.StatusId);
        changed.ToStatusName.Should().Be("Accepted");
        changed.ToAlias.Should().Be(ProjectDeliverableStatusAlias.Accepted);
    }

    [Fact]
    public void ChangeDeliverableStatus_WhenReopenedFromAccepted_ClearsAcceptance()
    {
        // Arrange
        var project = ActiveProject();
        var deliverable = Add(project);
        Move(project, deliverable, _statuses.Accepted);

        // Act
        var result = project.ChangeDeliverableStatus(Owner, NoProjectAncestry(), deliverable.Id, _statuses.InReview, null, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        deliverable.AcceptedOn.Should().BeNull();
        deliverable.AcceptedById.Should().BeNull();
        deliverable.IsOpen.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void ChangeDeliverableStatus_WhenDescopedWithoutReason_FailsAndChangesNothing(string? reason)
    {
        // Arrange
        var project = ActiveProject();
        var deliverable = Add(project);
        var revision = project.Revision;

        // Act
        var result = project.ChangeDeliverableStatus(Owner, NoProjectAncestry(), deliverable.Id, _statuses.Descoped, reason, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("needs a reason");
        deliverable.StatusId.Should().Be(_statuses.NotStarted.StatusId);
        project.DomainEvents.Should().BeEmpty();
        project.Revision.Should().Be(revision);
    }

    [Fact]
    public void ChangeDeliverableStatus_WhenDescopedWithReason_KeepsTheReason()
    {
        // Arrange
        var project = ActiveProject();
        var deliverable = Add(project);

        // Act
        var result = project.ChangeDeliverableStatus(Owner, NoProjectAncestry(), deliverable.Id, _statuses.Descoped, " Moved to phase two ", Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        deliverable.StatusReason.Should().Be("Moved to phase two");
        deliverable.StatusTransitions.Last().Reason.Should().Be("Moved to phase two");
        project.DomainEvents.OfType<ProjectDeliverableStatusChangedEvent>().Single().Reason.Should().Be("Moved to phase two");
    }

    [Fact]
    public void ChangeDeliverableStatus_WhenAlreadyInStatus_RaisesNothing()
    {
        // Arrange
        var project = ActiveProject();
        var deliverable = Add(project);
        var revision = project.Revision;

        // Act
        var result = project.ChangeDeliverableStatus(Owner, NoProjectAncestry(), deliverable.Id, _statuses.NotStarted, null, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.DomainEvents.Should().BeEmpty();
        project.Revision.Should().Be(revision);
    }

    [Fact]
    public void ChangeDeliverableStatus_WhenStatusIsOfAnotherWorkflow_Fails()
    {
        // Arrange
        var project = ActiveProject();
        var deliverable = Add(project);
        var otherWorkflow = new ProjectDeliverableStatuses();

        // Act
        var result = project.ChangeDeliverableStatus(Owner, NoProjectAncestry(), deliverable.Id, otherWorkflow.InProgress, null, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("different workflow");
        deliverable.StatusId.Should().Be(_statuses.NotStarted.StatusId);
    }

    [Fact]
    public void ChangeDeliverableStatus_WhenProjectClosed_Fails()
    {
        // Arrange
        var project = ActiveProject();
        var deliverable = Add(project);
        Move(project, deliverable, _statuses.Accepted);
        project.Complete(Owner, NoProjectAncestry(), Now).IsSuccess.Should().BeTrue();

        // Act
        var result = project.ChangeDeliverableStatus(Owner, NoProjectAncestry(), deliverable.Id, _statuses.InProgress, null, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("completed or canceled");
        deliverable.StatusAlias.Should().Be(ProjectDeliverableStatusAlias.Accepted);
    }

    #endregion ChangeDeliverableStatus

    #region ReorderDeliverables

    [Fact]
    public void ReorderDeliverables_WhenEveryDeliverableNamed_ReordersAndRaisesBothOrders()
    {
        // Arrange
        var project = ActiveProject();
        var first = Add(project, "First");
        var second = Add(project, "Second");
        var third = Add(project, "Third");

        // Act
        var result = project.ReorderDeliverables(Owner, NoProjectAncestry(), [third.Id, first.Id, second.Id], Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        third.Order.Should().Be(1);
        first.Order.Should().Be(2);
        second.Order.Should().Be(3);

        var reordered = project.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProjectDeliverablesReorderedEvent>().Subject;
        reordered.PreviousOrder.Should().Equal(first.Id, second.Id, third.Id);
        reordered.Order.Should().Equal(third.Id, first.Id, second.Id);
    }

    [Fact]
    public void ReorderDeliverables_WhenOneIsMissing_FailsAndKeepsOrder()
    {
        // Arrange
        var project = ActiveProject();
        var first = Add(project, "First");
        var second = Add(project, "Second");

        // Act
        var result = project.ReorderDeliverables(Owner, NoProjectAncestry(), [second.Id], Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        first.Order.Should().Be(1);
        second.Order.Should().Be(2);
        project.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ReorderDeliverables_WhenOrderUnchanged_RaisesNothing()
    {
        // Arrange
        var project = ActiveProject();
        var first = Add(project, "First");
        var second = Add(project, "Second");

        // Act
        var result = project.ReorderDeliverables(Owner, NoProjectAncestry(), [first.Id, second.Id], Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.DomainEvents.Should().BeEmpty();
    }

    #endregion ReorderDeliverables

    #region RemoveDeliverable

    [Fact]
    public void RemoveDeliverable_WhenProjectAndDeliverableNotStarted_RemovesIt()
    {
        // Arrange
        var project = ProposedProject();
        var deliverable = Add(project);

        // Act
        var result = project.RemoveDeliverable(Owner, NoProjectAncestry(), deliverable.Id, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        project.Deliverables.Should().BeEmpty();
        var removed = project.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProjectDeliverableRemovedEvent>().Subject;
        removed.DeliverableId.Should().Be(deliverable.Id);
        removed.Name.Should().Be("Customer portal");
    }

    [Fact]
    public void RemoveDeliverable_WhenProjectActive_Fails()
    {
        // Arrange
        var project = ActiveProject();
        var deliverable = Add(project);

        // Act
        var result = project.RemoveDeliverable(Owner, NoProjectAncestry(), deliverable.Id, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Descope it instead");
        project.Deliverables.Should().ContainSingle();
    }

    [Fact]
    public void RemoveDeliverable_WhenDeliverableStarted_Fails()
    {
        // Arrange
        var project = ProposedProject();
        var deliverable = Add(project);
        Move(project, deliverable, _statuses.InProgress);

        // Act
        var result = project.RemoveDeliverable(Owner, NoProjectAncestry(), deliverable.Id, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        project.Deliverables.Should().ContainSingle();
    }

    #endregion RemoveDeliverable
}
