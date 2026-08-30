using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.Tests.Sut.StatusWorkflows;

public sealed class WorkflowAssignmentTests
{
    private const int NotableAlias = 11;
    private const int TerminalAlias = 12;

    private static readonly WorkflowOwnerDescriptor Widget = new(
        "test.assignment.widget",
        "Widget",
        new Dictionary<int, string> { [NotableAlias] = "Notable", [TerminalAlias] = "Terminal" },
        [NotableAlias, TerminalAlias]);

    private static readonly WorkflowOwnerDescriptor Gadget = new(
        "test.assignment.gadget",
        "Gadget",
        new Dictionary<int, string> { [NotableAlias] = "Notable" },
        [NotableAlias]);

    public WorkflowAssignmentTests() => WorkflowOwners.Register(Widget, Gadget);

    private static StatusWorkflow PublishedWidgetWorkflow(string name = "Widget Workflow")
    {
        var workflow = StatusWorkflow.Create(name, null, Widget.Key).Value;
        workflow.AddStatus("Proposed", null, StatusCategory.Proposed);
        workflow.AddStatus("Notable", null, StatusCategory.Active, NotableAlias);
        workflow.AddStatus("Terminal", null, StatusCategory.Done, TerminalAlias);
        workflow.Publish(EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        return workflow;
    }

    private static StatusRemap CompleteRemap(StatusWorkflow from, StatusWorkflow to) =>
        StatusRemap.AutoMap(from, to).Value;

    #region Create

    [Fact]
    public void Create_ShouldAssignAPublishedWorkflowToAScope()
    {
        // Arrange
        var workflow = PublishedWidgetWorkflow();
        var portfolioId = Guid.CreateVersion7();

        // Act
        var result = WorkflowAssignment.Create(Widget.Key, portfolioId, workflow, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.OwnerType.Should().Be(Widget.Key);
        result.Value.ScopeId.Should().Be(portfolioId);
        result.Value.WorkflowId.Should().Be(workflow.Id);
    }

    [Fact]
    public void Create_WithANullScope_ShouldBeTheOrganizationDefault()
    {
        // Arrange
        var workflow = PublishedWidgetWorkflow();

        // Act
        var result = WorkflowAssignment.Create(Widget.Key, null, workflow, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        // The mandatory fallback for owner types with no narrower scope.
        result.IsSuccess.Should().BeTrue();
        result.Value.ScopeId.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldFail_WhenTheWorkflowIsStillDraft()
    {
        // Arrange
        var draft = StatusWorkflow.Create("Widget Workflow", null, Widget.Key).Value;

        // Act
        var result = WorkflowAssignment.Create(Widget.Key, null, draft, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only a published workflow can be assigned.");
    }

    [Fact]
    public void Create_ShouldFail_WhenTheWorkflowGovernsADifferentOwnerType()
    {
        // Arrange
        var widgetWorkflow = PublishedWidgetWorkflow();

        // Act
        var result = WorkflowAssignment.Create(Gadget.Key, null, widgetWorkflow, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        // A workflow built for one kind of record cannot govern another — its required aliases would
        // not match what that owner type's aggregates resolve.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot govern");
    }

    [Fact]
    public void Create_ShouldFail_ForAnUnregisteredOwnerType()
    {
        // Arrange
        var workflow = PublishedWidgetWorkflow();

        // Act
        var result = WorkflowAssignment.Create("test.assignment.absent", null, workflow, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not a registered workflow owner type");
    }

    #endregion Create

    #region Reassign

    [Fact]
    public void ReassignTo_ShouldPointTheScopeAtTheNewWorkflow()
    {
        // Arrange
        var old = PublishedWidgetWorkflow("Old");
        var assignment = WorkflowAssignment.Create(Widget.Key, null, old, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;
        var replacement = PublishedWidgetWorkflow("New");

        // Act
        var result = assignment.ReassignTo(replacement, CompleteRemap(old, replacement), EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        result.IsSuccess.Should().BeTrue();
        assignment.WorkflowId.Should().Be(replacement.Id);
    }

    [Fact]
    public void ReassignTo_ShouldFail_WhenTheReplacementIsNotPublished()
    {
        // Arrange
        var old = PublishedWidgetWorkflow("Old");
        var assignment = WorkflowAssignment.Create(Widget.Key, null, old, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;
        var draft = StatusWorkflow.Create("Still Building", null, Widget.Key).Value;
        var remap = CompleteRemap(old, PublishedWidgetWorkflow("Other"));

        // Act
        var result = assignment.ReassignTo(draft, remap, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        // A workflow being drafted for the new year is visible to reviewers but not assignable until
        // someone publishes it.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only a published workflow can be assigned.");
    }

    [Fact]
    public void ReassignTo_TheSameWorkflow_ShouldSucceedWithoutChanging()
    {
        // Arrange
        var workflow = PublishedWidgetWorkflow();
        var assignment = WorkflowAssignment.Create(Widget.Key, null, workflow, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;

        // Act
        var remap = CompleteRemap(workflow, PublishedWidgetWorkflow("Other"));
        var result = assignment.ReassignTo(workflow, remap, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        result.IsSuccess.Should().BeTrue();
        assignment.WorkflowId.Should().Be(workflow.Id);
    }

    #endregion Reassign

    #region Archiving an assigned workflow

    [Fact]
    public void Archive_ShouldFail_WhileTheWorkflowIsStillAssigned()
    {
        // Arrange
        var workflow = PublishedWidgetWorkflow();

        // Act
        var result = workflow.Archive(isAssigned: true, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        // Archiving an assigned workflow would leave that scope pointing at something nothing can be
        // assigned to. Reassign first.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This workflow is still assigned. Reassign those scopes to another workflow first.");
        workflow.State.Should().Be(StatusWorkflowState.Published);
    }

    [Fact]
    public void Archive_ShouldSucceed_OnceNothingAssignsIt()
    {
        // Arrange
        var workflow = PublishedWidgetWorkflow();

        // Act
        var result = workflow.Archive(isAssigned: false, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        // "In use" means assigned now, not used historically — records that passed through this
        // workflow resolve their statuses through it forever, so waiting for those would make
        // archiving impossible.
        result.IsSuccess.Should().BeTrue();
        workflow.State.Should().Be(StatusWorkflowState.Archived);
    }

    #endregion Archiving an assigned workflow

    #region Events

    [Fact]
    public void ReassignTo_ShouldRaiseAnEventCarryingBothWorkflows()
    {
        // Arrange
        var old = PublishedWidgetWorkflow("Old");
        var assignment = WorkflowAssignment.Create(Widget.Key, null, old, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;
        var replacement = PublishedWidgetWorkflow("New");

        // Act
        assignment.ReassignTo(replacement, CompleteRemap(old, replacement), EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        // The engine's only event, because an assignment has no owning object to announce it — the
        // container whose records just changed workflow is never touched.
        var assigned = assignment.DomainEvents.OfType<WorkflowAssignedEvent>().Last();
        assigned.FromWorkflowId.Should().Be(old.Id);
        assigned.ToWorkflowId.Should().Be(replacement.Id);
        assigned.ToWorkflowName.Should().Be("New");
    }

    [Fact]
    public void Create_ShouldRaiseAnEventWithNoPreviousWorkflow()
    {
        // Arrange
        var workflow = PublishedWidgetWorkflow();

        // Act
        var assignment = WorkflowAssignment.Create(Widget.Key, null, workflow, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;

        // Assert
        assignment.DomainEvents.OfType<WorkflowAssignedEvent>().Single().FromWorkflowId.Should().BeNull();
    }

    [Fact]
    public void Publish_ShouldRaiseAnEvent()
    {
        // Arrange
        var workflow = StatusWorkflow.Create("Widget Workflow", null, Widget.Key).Value;
        workflow.AddStatus("Notable", null, StatusCategory.Active, NotableAlias);
        workflow.AddStatus("Terminal", null, StatusCategory.Done, TerminalAlias);

        // Act
        workflow.Publish(EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        // Someone is waiting on this: it is the moment a reviewed draft becomes assignable.
        var published = workflow.DomainEvents.OfType<WorkflowPublishedEvent>().Single();
        published.OwnerType.Should().Be(Widget.Key);
        published.StatusCount.Should().Be(2);
    }

    [Fact]
    public void Archive_ShouldRaiseAnEvent()
    {
        // Arrange
        var workflow = PublishedWidgetWorkflow();

        // Act
        workflow.Archive(isAssigned: false, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        workflow.DomainEvents.Should().ContainSingle(e => e is WorkflowArchivedEvent);
    }

    [Fact]
    public void ARefusedPublish_ShouldRaiseNothing()
    {
        // Arrange
        var workflow = StatusWorkflow.Create("Widget Workflow", null, Widget.Key).Value;
        workflow.AddStatus("Notable", null, StatusCategory.Active, NotableAlias);

        // Act
        var result = workflow.Publish(EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        // An event asserts something happened; a refused publish did not.
        result.IsFailure.Should().BeTrue();
        workflow.DomainEvents.Should().BeEmpty();
    }

    #endregion Events
}
