using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.Common.Domain.Tests.Sut.StatusWorkflows;

public sealed class WorkflowAssignmentTests
{
    private const int NotableAlias = 11;
    private const int TerminalAlias = 12;

    private static readonly WorkflowOwnerDescriptor Widget = new(
        "test.assignment.widget",
        "Widget",
        new Dictionary<int, WorkflowAlias> { [NotableAlias] = new("Notable", StatusCategory.Active), [TerminalAlias] = new("Terminal", StatusCategory.Done) },
        [NotableAlias, TerminalAlias]);

    private static readonly WorkflowOwnerDescriptor Gadget = new(
        "test.assignment.gadget",
        "Gadget",
        new Dictionary<int, WorkflowAlias> { [NotableAlias] = new("Notable", StatusCategory.Active) },
        [NotableAlias]);

    // An owner type whose records come in types that follow different workflows.
    private static readonly WorkflowOwnerDescriptor Ticket = new(
        "test.assignment.ticket",
        "Ticket",
        new Dictionary<int, WorkflowAlias> { [NotableAlias] = new("Notable", StatusCategory.Active), [TerminalAlias] = new("Terminal", StatusCategory.Done) },
        [NotableAlias, TerminalAlias],
        hasRecordTypes: true);

    public WorkflowAssignmentTests() => WorkflowOwners.Register(Widget, Gadget, Ticket);

    private static StatusWorkflow PublishedWidgetWorkflow(string name = "Widget Workflow", string? ownerType = null)
    {
        var workflow = StatusWorkflow.Create(name, null, ownerType ?? Widget.Key, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;
        workflow.AddStatus("Proposed", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        workflow.AddStatus("Notable", null, StatusCategory.Active, NotableAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        workflow.AddStatus("Terminal", null, StatusCategory.Done, TerminalAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        workflow.Publish(EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        return workflow;
    }

    /// <summary>
    /// Stands in for the first save: assigns the key the workflow's events wait for, raises what was waiting
    /// on it, and clears it so a test sees only what its own act raises.
    /// </summary>
    private static StatusWorkflow Saved(StatusWorkflow workflow)
    {
        workflow.SetPrivate(w => w.Key, 7);
        workflow.ExecutePostPersistenceActions();
        workflow.ClearDomainEvents();

        return workflow;
    }

    private static StatusRemap CompleteRemap(StatusWorkflow from, StatusWorkflow to) =>
        StatusRemap.AutoMap(from, to).Value;

    private readonly Dictionary<Guid, StatusWorkflowScheme> _schemes = [];

    /// <summary>
    /// The workflow's implicit scheme, one per workflow as the database keeps it. Built as though the workflow
    /// were published, so a test can still offer a draft to the assignment and see it refused there.
    /// </summary>
    private StatusWorkflowScheme SchemeFor(StatusWorkflow workflow)
    {
        if (_schemes.TryGetValue(workflow.Id, out var existing))
        {
            return existing;
        }

        var state = workflow.State;
        workflow.SetPrivate(w => w.State, StatusWorkflowState.Published);
        var scheme = StatusWorkflowScheme.CreateImplicit(workflow, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;
        workflow.SetPrivate(w => w.State, state);

        return _schemes[workflow.Id] = scheme;
    }

    /// <summary>Assigns the workflow's implicit scheme, as a scope picking a workflow does.</summary>
    private Result<WorkflowAssignment> Assign(string ownerType, Guid? scopeId, StatusWorkflow workflow, EventActor actor, Instant timestamp) =>
        WorkflowAssignment.Create(ownerType, scopeId, SchemeFor(workflow), workflow, actor, timestamp);

    #region Create

    [Fact]
    public void Create_ShouldAssignAPublishedWorkflowToAScope()
    {
        // Arrange
        var workflow = PublishedWidgetWorkflow();
        var portfolioId = Guid.CreateVersion7();

        // Act
        var result = Assign(Widget.Key, portfolioId, workflow, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.OwnerType.Should().Be(Widget.Key);
        result.Value.ScopeId.Should().Be(portfolioId);
        result.Value.Scheme!.DefaultWorkflowId.Should().Be(workflow.Id);
    }

    [Fact]
    public void Create_WithANullScope_ShouldBeTheOrganizationDefault()
    {
        // Arrange
        var workflow = PublishedWidgetWorkflow();

        // Act
        var result = Assign(Widget.Key, null, workflow, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        // The mandatory fallback for owner types with no narrower scope.
        result.IsSuccess.Should().BeTrue();
        result.Value.ScopeId.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldFail_WhenTheWorkflowIsStillDraft()
    {
        // Arrange
        var draft = StatusWorkflow.Create("Widget Workflow", null, Widget.Key, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;

        // Act
        var result = Assign(Widget.Key, null, draft, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

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
        var result = Assign(Gadget.Key, null, widgetWorkflow, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

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
        var result = Assign("test.assignment.absent", null, workflow, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

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
        var assignment = Assign(Widget.Key, null, old, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;
        var replacement = PublishedWidgetWorkflow("New");

        // Act
        var result = assignment.ReassignTo(SchemeFor(replacement), replacement, [CompleteRemap(old, replacement)], EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        result.IsSuccess.Should().BeTrue();
        assignment.Scheme!.DefaultWorkflowId.Should().Be(replacement.Id);
    }

    [Fact]
    public void ReassignTo_ShouldFail_WhenTheReplacementIsNotPublished()
    {
        // Arrange
        var old = PublishedWidgetWorkflow("Old");
        var assignment = Assign(Widget.Key, null, old, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;
        var draft = StatusWorkflow.Create("Still Building", null, Widget.Key, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;
        var remap = CompleteRemap(old, PublishedWidgetWorkflow("Other"));

        // Act
        var result = assignment.ReassignTo(SchemeFor(draft), draft, [remap], EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

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
        var assignment = Assign(Widget.Key, null, workflow, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;

        // Act
        var remap = CompleteRemap(workflow, PublishedWidgetWorkflow("Other"));
        var result = assignment.ReassignTo(SchemeFor(workflow), workflow, [remap], EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        result.IsSuccess.Should().BeTrue();
        assignment.Scheme!.DefaultWorkflowId.Should().Be(workflow.Id);
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
        var assignment = Assign(Widget.Key, null, old, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;
        var replacement = PublishedWidgetWorkflow("New");

        // Act
        assignment.ReassignTo(SchemeFor(replacement), replacement, [CompleteRemap(old, replacement)], EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

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
        var assignment = Assign(Widget.Key, null, workflow, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;

        // Assert
        assignment.DomainEvents.OfType<WorkflowAssignedEvent>().Single().FromWorkflowId.Should().BeNull();
    }

    [Fact]
    public void Publish_ShouldRaiseAnEvent()
    {
        // Arrange
        var workflow = StatusWorkflow.Create("Widget Workflow", null, Widget.Key, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;
        workflow.AddStatus("Notable", null, StatusCategory.Active, NotableAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        workflow.AddStatus("Terminal", null, StatusCategory.Done, TerminalAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        Saved(workflow);

        // Act
        workflow.Publish(EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        // Someone is waiting on this: it is the moment a reviewed draft becomes assignable.
        var published = workflow.DomainEvents.OfType<WorkflowPublishedEventV2>().Single();
        published.OwnerType.Should().Be(Widget.Key);
        published.StatusCount.Should().Be(2);
    }

    [Fact]
    public void Archive_ShouldRaiseAnEvent()
    {
        // Arrange
        var workflow = Saved(PublishedWidgetWorkflow());

        // Act
        workflow.Archive(isAssigned: false, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        workflow.DomainEvents.Should().ContainSingle(e => e is WorkflowArchivedEventV2);
    }

    [Fact]
    public void ARefusedPublish_ShouldRaiseNothing()
    {
        // Arrange
        var workflow = StatusWorkflow.Create("Widget Workflow", null, Widget.Key, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;
        workflow.AddStatus("Notable", null, StatusCategory.Active, NotableAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        Saved(workflow);

        // Act
        var result = workflow.Publish(EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        // An event asserts something happened; a refused publish did not.
        result.IsFailure.Should().BeTrue();
        workflow.DomainEvents.Should().BeEmpty();
        workflow.PostPersistenceActions.Should().BeEmpty();
    }

    [Fact]
    public void Remove_ForAScopesOwnAssignment_ShouldRaiseAnEventCarryingBothWorkflows()
    {
        // Arrange
        var scopeId = Guid.NewGuid();
        var workflow = Saved(PublishedWidgetWorkflow());
        var assignment = Assign(Widget.Key, scopeId, workflow, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;
        assignment.ClearDomainEvents();
        var organizationWorkflowId = Guid.NewGuid();
        var organizationSchemeId = Guid.NewGuid();

        // Act
        var result = assignment.Remove(organizationSchemeId, organizationWorkflowId, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        result.IsSuccess.Should().BeTrue();
        var removed = assignment.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowAssignmentRemovedEvent>().Subject;
        removed.ScopeId.Should().Be(scopeId);
        removed.FromWorkflowId.Should().Be(workflow.Id);
        removed.ToWorkflowId.Should().Be(organizationWorkflowId);
        removed.FromSchemeId.Should().Be(assignment.SchemeId);
        removed.ToSchemeId.Should().Be(organizationSchemeId);
    }

    [Fact]
    public void Remove_ForTheOrganizationsAssignment_ShouldFail()
    {
        // Arrange — every narrower scope falls back to it
        var assignment = Assign(Widget.Key, null, Saved(PublishedWidgetWorkflow()), EventActor.System,
            Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;
        assignment.ClearDomainEvents();

        // Act
        var result = assignment.Remove(null, null, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        result.IsFailure.Should().BeTrue();
        assignment.DomainEvents.Should().BeEmpty();
    }

    #endregion Events

    #region Schemes with record types

    private static StatusWorkflowScheme TicketScheme(StatusWorkflow defaultWorkflow) =>
        StatusWorkflowScheme.Create(Ticket.Key, "Engineering", null, defaultWorkflow, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;

    [Fact]
    public void ReassignTo_AnotherScheme_ShouldNeedARemapForEveryTypeWhoseWorkflowChanges()
    {
        // Arrange
        var standard = PublishedWidgetWorkflow("Standard", Ticket.Key);
        var bugFlow = PublishedWidgetWorkflow("Bug Flow", Ticket.Key);
        var bug = RecordType.Create(Ticket.Key, "Bug", EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;
        var current = TicketScheme(standard);
        var target = TicketScheme(standard);
        target.SetEntry(bug, bugFlow, CompleteRemap(standard, bugFlow), EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        var assignment = WorkflowAssignment.Create(Ticket.Key, Guid.NewGuid(), current, standard, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;

        // Act
        var withoutBugRemap = assignment.ReassignTo(target, standard, [], EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        var withBugRemap = assignment.ReassignTo(target, standard, [CompleteRemap(standard, bugFlow)], EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        // Only Bugs change workflow: the defaults match, so nothing else needs mapping.
        withoutBugRemap.IsFailure.Should().BeTrue();
        withBugRemap.IsSuccess.Should().BeTrue();
        assignment.SchemeId.Should().Be(target.Id);
        var assigned = assignment.DomainEvents.OfType<WorkflowAssignedEvent>().Last();
        assigned.FromSchemeId.Should().Be(current.Id);
        assigned.ToSchemeId.Should().Be(target.Id);
    }

    [Fact]
    public void ReassignTo_ShouldFail_WhenARemapIsIncomplete()
    {
        // Arrange
        var standard = PublishedWidgetWorkflow("Standard", Ticket.Key);
        var sparse = StatusWorkflow.Create("Sparse", null, Ticket.Key, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;
        sparse.AddStatus("Notable", null, StatusCategory.Active, NotableAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        sparse.AddStatus("Terminal", null, StatusCategory.Done, TerminalAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        sparse.AddStatus("Closed", null, StatusCategory.Done, StatusWorkflow.NoAlias, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        sparse.Publish(EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));
        var current = TicketScheme(standard);
        var target = TicketScheme(sparse);
        var assignment = WorkflowAssignment.Create(Ticket.Key, null, current, standard, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;
        var remap = CompleteRemap(standard, sparse);
        remap.IsComplete.Should().BeFalse();

        // Act
        var result = assignment.ReassignTo(target, sparse, [remap], EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("have nowhere to go");
        assignment.SchemeId.Should().Be(current.Id);
    }

    [Fact]
    public void ReassignTo_ShouldThrow_WhenTheCurrentSchemeIsNotLoaded()
    {
        // Arrange
        var standard = PublishedWidgetWorkflow("Standard");
        var assignment = Assign(Widget.Key, null, standard, EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0)).Value;
        assignment.SetPrivate(a => a.Scheme, null);
        var replacement = PublishedWidgetWorkflow("New");

        // Act
        var act = () => assignment.ReassignTo(SchemeFor(replacement), replacement, [CompleteRemap(standard, replacement)], EventActor.System, Instant.FromUtc(2026, 1, 15, 9, 30, 0));

        // Assert
        // Without the scheme, which types change workflow cannot be known; a missing Include must not pass.
        act.Should().Throw<InvalidOperationException>();
    }

    #endregion Schemes with record types
}
