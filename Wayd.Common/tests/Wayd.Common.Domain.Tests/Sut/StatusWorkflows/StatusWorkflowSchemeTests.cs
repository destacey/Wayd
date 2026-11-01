using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.Common.Domain.Tests.Sut.StatusWorkflows;

public sealed class StatusWorkflowSchemeTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    private static readonly WorkflowOwnerDescriptor Ticket = new(
        "test.scheme.ticket", "Ticket", new Dictionary<int, WorkflowAlias>(), [], hasRecordTypes: true);

    private static readonly WorkflowOwnerDescriptor Plain = new(
        "test.scheme.plain", "Plain", new Dictionary<int, WorkflowAlias>(), []);

    public StatusWorkflowSchemeTests() => WorkflowOwners.Register(Ticket, Plain);

    private static StatusWorkflow Published(string name, string? ownerType = null, bool withActive = true)
    {
        var workflow = StatusWorkflow.Create(name, null, ownerType ?? Ticket.Key, EventActor.System, Now).Value;
        workflow.AddStatus("New", null, StatusCategory.Proposed, StatusWorkflow.NoAlias, EventActor.System, Now);
        if (withActive)
        {
            workflow.AddStatus("Doing", null, StatusCategory.Active, StatusWorkflow.NoAlias, EventActor.System, Now);
        }
        workflow.AddStatus("Closed", null, StatusCategory.Done, StatusWorkflow.NoAlias, EventActor.System, Now);
        workflow.AddStatus("Dropped", null, StatusCategory.Done, StatusWorkflow.NoAlias, EventActor.System, Now);
        workflow.Publish(EventActor.System, Now);

        return workflow;
    }

    private static RecordType Type(string name, string? ownerType = null) =>
        RecordType.Create(ownerType ?? Ticket.Key, name, EventActor.System, Now).Value;

    /// <summary>
    /// Stands in for the first save: assigns the key the events wait for, raises what was waiting on it,
    /// and clears it so a test sees only what its own act raises.
    /// </summary>
    private static StatusWorkflowScheme Saved(StatusWorkflowScheme scheme)
    {
        scheme.SetPrivate(s => s.Key, 9);
        scheme.ExecutePostPersistenceActions();
        scheme.ClearDomainEvents();

        return scheme;
    }

    private static StatusWorkflowScheme SchemeOn(StatusWorkflow defaultWorkflow) =>
        Saved(StatusWorkflowScheme.Create(Ticket.Key, "Engineering", null, defaultWorkflow, EventActor.System, Now).Value);

    private static StatusRemap Remap(StatusWorkflow from, StatusWorkflow to) => StatusRemap.AutoMap(from, to).Value;

    #region Create

    [Fact]
    public void Create_ShouldSendEveryTypeToTheDefault()
    {
        // Arrange
        var workflow = Published("Standard");

        // Act
        var result = StatusWorkflowScheme.Create(Ticket.Key, " Engineering ", "Teams that ship", workflow, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var scheme = result.Value;
        scheme.OwnerType.Should().Be(Ticket.Key);
        scheme.Name.Should().Be("Engineering");
        scheme.Ownership.Should().Be(WorkflowOwnership.Owned);
        scheme.IsImplicit.Should().BeFalse();
        scheme.DefaultWorkflowId.Should().Be(workflow.Id);
        scheme.Entries.Should().BeEmpty();
        scheme.WorkflowFor(Guid.CreateVersion7()).Should().Be(workflow.Id);
    }

    [Fact]
    public void Create_ShouldRaiseCreated_OnceTheKeyIsAssigned()
    {
        // Arrange
        var workflow = Published("Standard");
        var scheme = StatusWorkflowScheme.Create(Ticket.Key, "Engineering", null, workflow, EventActor.System, Now).Value;
        scheme.DomainEvents.Should().BeEmpty();

        // Act
        scheme.SetPrivate(s => s.Key, 9);
        scheme.ExecutePostPersistenceActions();

        // Assert
        var created = scheme.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowSchemeCreatedEvent>().Subject;
        created.Key.Should().Be(9);
        created.DefaultWorkflowId.Should().Be(workflow.Id);
        created.IsImplicit.Should().BeFalse();
        created.SourceSchemeId.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldFail_WhenTheOwnerTypeHasNoRecordTypes()
    {
        // Arrange
        var workflow = Published("Plain", Plain.Key);

        // Act
        var result = StatusWorkflowScheme.Create(Plain.Key, "Engineering", null, workflow, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("do not have record types");
    }

    [Fact]
    public void Create_ShouldFail_WhenTheDefaultIsStillDraft()
    {
        // Arrange
        var draft = StatusWorkflow.Create("Draft", null, Ticket.Key, EventActor.System, Now).Value;

        // Act
        var result = StatusWorkflowScheme.Create(Ticket.Key, "Engineering", null, draft, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Only a published workflow can be part of a scheme.");
    }

    [Fact]
    public void Create_ShouldFail_WhenTheDefaultGovernsAnotherOwnerType()
    {
        // Arrange
        var plainWorkflow = Published("Plain", Plain.Key);

        // Act
        var result = StatusWorkflowScheme.Create(Ticket.Key, "Engineering", null, plainWorkflow, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot govern");
    }

    [Fact]
    public void CreateManaged_ShouldKeepTheSourceKey()
    {
        // Arrange
        var workflow = Published("Standard");

        // Act
        var result = StatusWorkflowScheme.CreateManaged(Ticket.Key, "Agile", " process-agile ", workflow, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Ownership.Should().Be(WorkflowOwnership.Managed);
        result.Value.ExternalKey.Should().Be("process-agile");
    }

    [Fact]
    public void CreateImplicit_ShouldStandForTheWorkflow_ForAnOwnerTypeWithoutRecordTypes()
    {
        // Arrange
        var workflow = Published("Plain", Plain.Key);

        // Act
        var result = StatusWorkflowScheme.CreateImplicit(workflow, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsImplicit.Should().BeTrue();
        result.Value.OwnerType.Should().Be(Plain.Key);
        result.Value.DefaultWorkflowId.Should().Be(workflow.Id);
        result.Value.WorkflowFor(null).Should().Be(workflow.Id);
    }

    #endregion Create

    #region WorkflowFor

    [Fact]
    public void WorkflowFor_ShouldUseTheTypesEntry_AndTheDefaultOtherwise()
    {
        // Arrange
        var standard = Published("Standard");
        var bugFlow = Published("Bug Flow");
        var scheme = SchemeOn(standard);
        var bug = Type("Bug");
        var story = Type("Story");
        scheme.SetEntry(bug, bugFlow, Remap(standard, bugFlow), EventActor.System, Now);

        // Act
        var forBug = scheme.WorkflowFor(bug.Id);
        var forStory = scheme.WorkflowFor(story.Id);
        var forUntyped = scheme.WorkflowFor(null);

        // Assert
        forBug.Should().Be(bugFlow.Id);
        forStory.Should().Be(standard.Id);
        forUntyped.Should().Be(standard.Id);
    }

    #endregion WorkflowFor

    #region SetEntry

    [Fact]
    public void SetEntry_ShouldSendTheTypeToTheWorkflow_RecordingWhereItCameFrom()
    {
        // Arrange
        var standard = Published("Standard");
        var bugFlow = Published("Bug Flow");
        var scheme = SchemeOn(standard);
        var bug = Type("Bug");

        // Act
        var result = scheme.SetEntry(bug, bugFlow, Remap(standard, bugFlow), EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scheme.Entries.Should().ContainSingle().Which.Should().Match<StatusWorkflowSchemeEntry>(
            e => e.RecordTypeId == bug.Id && e.WorkflowId == bugFlow.Id && e.SchemeId == scheme.Id);
        var set = scheme.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowSchemeEntrySetEvent>().Subject;
        set.RecordTypeId.Should().Be(bug.Id);
        set.FromWorkflowId.Should().Be(standard.Id);
        set.ToWorkflowId.Should().Be(bugFlow.Id);
        set.HadEntry.Should().BeFalse();
    }

    [Fact]
    public void SetEntry_ShouldReplaceAnExistingEntry_RemappingFromItsWorkflow()
    {
        // Arrange
        var standard = Published("Standard");
        var bugFlow = Published("Bug Flow");
        var triage = Published("Triage");
        var scheme = SchemeOn(standard);
        var bug = Type("Bug");
        scheme.SetEntry(bug, bugFlow, Remap(standard, bugFlow), EventActor.System, Now);
        scheme.ClearDomainEvents();

        // Act
        var result = scheme.SetEntry(bug, triage, Remap(bugFlow, triage), EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scheme.Entries.Should().ContainSingle().Which.WorkflowId.Should().Be(triage.Id);
        var set = scheme.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowSchemeEntrySetEvent>().Subject;
        set.FromWorkflowId.Should().Be(bugFlow.Id);
        set.HadEntry.Should().BeTrue();
    }

    [Fact]
    public void SetEntry_ShouldDoNothing_WhenTheEntryAlreadyNamesTheWorkflow()
    {
        // Arrange
        var standard = Published("Standard");
        var bugFlow = Published("Bug Flow");
        var scheme = SchemeOn(standard);
        var bug = Type("Bug");
        scheme.SetEntry(bug, bugFlow, Remap(standard, bugFlow), EventActor.System, Now);
        scheme.ClearDomainEvents();

        // Act
        var result = scheme.SetEntry(bug, bugFlow, remap: null, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scheme.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void SetEntry_ShouldNeedNoRemap_WhenPinningATypeToTheDefault()
    {
        // Arrange
        var standard = Published("Standard");
        var scheme = SchemeOn(standard);
        var bug = Type("Bug");

        // Act
        var result = scheme.SetEntry(bug, standard, remap: null, EventActor.System, Now);

        // Assert
        // No record changes workflow, so there is nothing to map; the entry keeps Bugs on it if the default changes.
        result.IsSuccess.Should().BeTrue();
        scheme.Entries.Should().ContainSingle().Which.WorkflowId.Should().Be(standard.Id);
    }

    [Fact]
    public void SetEntry_ShouldFail_WithoutARemap_WhenTheTypeChangesWorkflow()
    {
        // Arrange
        var standard = Published("Standard");
        var bugFlow = Published("Bug Flow");
        var scheme = SchemeOn(standard);
        var bug = Type("Bug");

        // Act
        var result = scheme.SetEntry(bug, bugFlow, remap: null, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Moving Bug onto another workflow needs a mapping for every status.");
        scheme.Entries.Should().BeEmpty();
        scheme.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void SetEntry_ShouldFail_WhenTheRemapIsIncomplete()
    {
        // Arrange
        var standard = Published("Standard");
        var noActive = Published("No Active", withActive: false);
        var scheme = SchemeOn(standard);
        var bug = Type("Bug");
        var remap = Remap(standard, noActive);
        remap.IsComplete.Should().BeFalse();

        // Act
        var result = scheme.SetEntry(bug, noActive, remap, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("have nowhere to go");
        scheme.Entries.Should().BeEmpty();
    }

    [Fact]
    public void SetEntry_ShouldFail_WhenTheRemapIsFromAnotherWorkflow()
    {
        // Arrange
        var standard = Published("Standard");
        var bugFlow = Published("Bug Flow");
        var triage = Published("Triage");
        var scheme = SchemeOn(standard);
        var bug = Type("Bug");

        // Act
        var result = scheme.SetEntry(bug, bugFlow, Remap(triage, bugFlow), EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The mapping is between different workflows than this change.");
    }

    [Fact]
    public void SetEntry_ShouldFail_ForARecordTypeOfAnotherOwnerType()
    {
        // Arrange
        var standard = Published("Standard");
        var scheme = SchemeOn(standard);
        var foreign = RecordType.Create(Ticket.Key, "Bug", EventActor.System, Now).Value;
        foreign.SetPrivate(r => r.OwnerType, "test.scheme.other");

        // Act
        var result = scheme.SetEntry(foreign, standard, remap: null, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        scheme.Entries.Should().BeEmpty();
    }

    [Fact]
    public void SetEntry_ShouldFail_OnAnImplicitScheme()
    {
        // Arrange
        var standard = Published("Standard");
        var bugFlow = Published("Bug Flow");
        var scheme = Saved(StatusWorkflowScheme.CreateImplicit(standard, EventActor.System, Now).Value);

        // Act
        var result = scheme.SetEntry(Type("Bug"), bugFlow, Remap(standard, bugFlow), EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        scheme.Entries.Should().BeEmpty();
    }

    #endregion SetEntry

    #region RemoveEntry

    [Fact]
    public void RemoveEntry_ShouldSendTheTypeBackToTheDefault()
    {
        // Arrange
        var standard = Published("Standard");
        var bugFlow = Published("Bug Flow");
        var scheme = SchemeOn(standard);
        var bug = Type("Bug");
        scheme.SetEntry(bug, bugFlow, Remap(standard, bugFlow), EventActor.System, Now);
        scheme.ClearDomainEvents();

        // Act
        var result = scheme.RemoveEntry(bug.Id, Remap(bugFlow, standard), EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scheme.Entries.Should().BeEmpty();
        scheme.WorkflowFor(bug.Id).Should().Be(standard.Id);
        var removed = scheme.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowSchemeEntryRemovedEvent>().Subject;
        removed.FromWorkflowId.Should().Be(bugFlow.Id);
        removed.ToWorkflowId.Should().Be(standard.Id);
    }

    [Fact]
    public void RemoveEntry_ShouldFail_WithoutARemap_WhenTheEntryDiffersFromTheDefault()
    {
        // Arrange
        var standard = Published("Standard");
        var bugFlow = Published("Bug Flow");
        var scheme = SchemeOn(standard);
        var bug = Type("Bug");
        scheme.SetEntry(bug, bugFlow, Remap(standard, bugFlow), EventActor.System, Now);
        scheme.ClearDomainEvents();

        // Act
        var result = scheme.RemoveEntry(bug.Id, remap: null, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        scheme.Entries.Should().ContainSingle();
        scheme.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RemoveEntry_ShouldDoNothing_WhenTheTypeHasNoEntry()
    {
        // Arrange
        var scheme = SchemeOn(Published("Standard"));

        // Act
        var result = scheme.RemoveEntry(Guid.CreateVersion7(), remap: null, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scheme.DomainEvents.Should().BeEmpty();
    }

    #endregion RemoveEntry

    #region ChangeDefault

    [Fact]
    public void ChangeDefault_ShouldMoveTypesWithoutAnEntry_AndLeaveEntriesAlone()
    {
        // Arrange
        var standard = Published("Standard");
        var bugFlow = Published("Bug Flow");
        var lean = Published("Lean");
        var scheme = SchemeOn(standard);
        var bug = Type("Bug");
        var story = Type("Story");
        scheme.SetEntry(bug, bugFlow, Remap(standard, bugFlow), EventActor.System, Now);
        scheme.ClearDomainEvents();

        // Act
        var result = scheme.ChangeDefault(lean, Remap(standard, lean), EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scheme.WorkflowFor(story.Id).Should().Be(lean.Id);
        scheme.WorkflowFor(bug.Id).Should().Be(bugFlow.Id);
        var changed = scheme.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowSchemeDefaultChangedEvent>().Subject;
        changed.FromWorkflowId.Should().Be(standard.Id);
        changed.ToWorkflowId.Should().Be(lean.Id);
    }

    [Fact]
    public void ChangeDefault_ShouldFail_WhenTheRemapIsIncomplete()
    {
        // Arrange
        var standard = Published("Standard");
        var noActive = Published("No Active", withActive: false);
        var scheme = SchemeOn(standard);

        // Act
        var result = scheme.ChangeDefault(noActive, Remap(standard, noActive), EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        scheme.DefaultWorkflowId.Should().Be(standard.Id);
        scheme.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ChangeDefault_ShouldFail_OnAnImplicitScheme()
    {
        // Arrange
        var standard = Published("Standard");
        var lean = Published("Lean");
        var scheme = Saved(StatusWorkflowScheme.CreateImplicit(standard, EventActor.System, Now).Value);

        // Act
        var result = scheme.ChangeDefault(lean, Remap(standard, lean), EventActor.System, Now);

        // Assert
        // An implicit scheme is its workflow; a scope that wants another picks that workflow's own.
        result.IsFailure.Should().BeTrue();
        scheme.DefaultWorkflowId.Should().Be(standard.Id);
    }

    #endregion ChangeDefault

    #region UpdateDetails

    [Fact]
    public void UpdateDetails_ShouldRecordThePreviousDetails()
    {
        // Arrange
        var scheme = SchemeOn(Published("Standard"));

        // Act
        var result = scheme.UpdateDetails("Platform", "Platform teams", EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scheme.Name.Should().Be("Platform");
        var updated = scheme.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowSchemeDetailsUpdatedEvent>().Subject;
        updated.Previous.Should().Be(new WorkflowSchemeDetails("Engineering", null));
        updated.Description.Should().Be("Platform teams");
    }

    [Fact]
    public void UpdateDetails_ShouldRaiseNothing_WhenNothingChangesAfterTrimming()
    {
        // Arrange
        var scheme = SchemeOn(Published("Standard"));

        // Act
        var result = scheme.UpdateDetails(" Engineering ", " ", EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scheme.DomainEvents.Should().BeEmpty();
    }

    #endregion UpdateDetails

    #region Clone

    [Fact]
    public void Clone_ShouldCopyTheDefaultAndEntriesIntoAnOwnedScheme()
    {
        // Arrange
        var standard = Published("Standard");
        var bugFlow = Published("Bug Flow");
        var source = Saved(StatusWorkflowScheme.CreateManaged(Ticket.Key, "Agile", "agile", standard, EventActor.System, Now).Value);
        var bug = Type("Bug");
        source.SetEntry(bug, bugFlow, Remap(standard, bugFlow), EventActor.System, Now);

        // Act
        var result = source.Clone("Agile (Payments)", null, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var clone = result.Value;
        clone.Id.Should().NotBe(source.Id);
        clone.Ownership.Should().Be(WorkflowOwnership.Owned);
        clone.ExternalKey.Should().BeNull();
        clone.DefaultWorkflowId.Should().Be(standard.Id);
        clone.Entries.Should().ContainSingle().Which.Should().Match<StatusWorkflowSchemeEntry>(
            e => e.SchemeId == clone.Id && e.RecordTypeId == bug.Id && e.WorkflowId == bugFlow.Id);

        clone.SetPrivate(s => s.Key, 10);
        clone.ExecutePostPersistenceActions();
        clone.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowSchemeCreatedEvent>()
            .Which.SourceSchemeId.Should().Be(source.Id);
    }

    [Fact]
    public void Clone_ShouldFail_ForAnImplicitScheme()
    {
        // Arrange
        var scheme = Saved(StatusWorkflowScheme.CreateImplicit(Published("Standard"), EventActor.System, Now).Value);

        // Act
        var result = scheme.Clone("Copy", null, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion Clone

    #region Delete

    [Fact]
    public void Delete_ShouldFail_WhileAScopePointsAtTheScheme()
    {
        // Arrange
        var scheme = SchemeOn(Published("Standard"));

        // Act
        var result = scheme.Delete(isAssigned: true, EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        scheme.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Delete_ShouldRecordTheDeletion_WhenNothingPointsAtIt()
    {
        // Arrange
        var scheme = SchemeOn(Published("Standard"));

        // Act
        var result = scheme.Delete(isAssigned: false, EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scheme.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<WorkflowSchemeDeletedEvent>()
            .Which.Key.Should().Be(9);
    }

    #endregion Delete
}
