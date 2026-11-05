using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.Common.Domain.Tests.Sut.StatusWorkflows;

public sealed class RecordTypeTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    private static readonly WorkflowOwnerDescriptor Ticket = new(
        "test.recordtype.ticket", "Ticket", new Dictionary<int, WorkflowAlias>(), [], hasRecordTypes: true, hasManagedWorkflows: true);

    private static readonly WorkflowOwnerDescriptor Plain = new(
        "test.recordtype.plain", "Plain", new Dictionary<int, WorkflowAlias>(), []);

    // Has record types, but only administrators define them.
    private static readonly WorkflowOwnerDescriptor Authored = new(
        "test.recordtype.authored", "Authored", new Dictionary<int, WorkflowAlias>(), [], hasRecordTypes: true);

    public RecordTypeTests() => WorkflowOwners.Register(Ticket, Plain, Authored);

    /// <summary>
    /// Stands in for the first save: assigns the key the events wait for, raises what was waiting on it,
    /// and clears it so a test sees only what its own act raises.
    /// </summary>
    private static RecordType Saved(RecordType recordType)
    {
        recordType.SetPrivate(r => r.Key, 4);
        recordType.ExecutePostPersistenceActions();
        recordType.ClearDomainEvents();

        return recordType;
    }

    #region Create

    [Fact]
    public void Create_ShouldDefineAnActiveOwnedType()
    {
        // Act
        var result = RecordType.Create(Ticket.Key, " Bug ", EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.OwnerType.Should().Be(Ticket.Key);
        result.Value.Name.Should().Be("Bug");
        result.Value.Ownership.Should().Be(WorkflowOwnership.Owned);
        result.Value.ExternalKey.Should().BeNull();
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldRaiseCreated_OnceTheKeyIsAssigned()
    {
        // Arrange
        var recordType = RecordType.Create(Ticket.Key, "Bug", EventActor.System, Now).Value;
        recordType.DomainEvents.Should().BeEmpty();

        // Act
        recordType.SetPrivate(r => r.Key, 4);
        recordType.ExecutePostPersistenceActions();

        // Assert
        var created = recordType.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RecordTypeCreatedEvent>().Subject;
        created.Id.Should().Be(recordType.Id);
        created.Key.Should().Be(4);
        created.OwnerType.Should().Be(Ticket.Key);
        created.Name.Should().Be("Bug");
        created.Ownership.Should().Be(WorkflowOwnership.Owned);
    }

    [Fact]
    public void Create_ShouldFail_WhenTheOwnerTypeHasNoRecordTypes()
    {
        // Act
        var result = RecordType.Create(Plain.Key, "Bug", EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Plain records do not have record types.");
    }

    [Fact]
    public void Create_ShouldFail_WhenTheOwnerTypeIsNotRegistered()
    {
        // Act
        var result = RecordType.Create("test.recordtype.unknown", "Bug", EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CreateManaged_ShouldKeepTheSourceKey()
    {
        // Act
        var result = RecordType.CreateManaged(Ticket.Key, "Bug", " Microsoft.VSTS.WorkItemTypes.Bug ", EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Ownership.Should().Be(WorkflowOwnership.Managed);
        result.Value.ExternalKey.Should().Be("Microsoft.VSTS.WorkItemTypes.Bug");
    }

    #endregion Create

    #region Rename

    [Fact]
    public void Rename_ShouldChangeTheNameAndRecordBothEnds()
    {
        // Arrange
        var recordType = Saved(RecordType.Create(Ticket.Key, "Bug", EventActor.System, Now).Value);

        // Act
        var result = recordType.Rename("Defect ", EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        recordType.Name.Should().Be("Defect");
        var renamed = recordType.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RecordTypeRenamedEvent>().Subject;
        renamed.Name.Should().Be("Defect");
        renamed.PreviousName.Should().Be("Bug");
    }

    [Fact]
    public void Rename_ShouldRaiseNothing_WhenTheNameIsUnchangedAfterTrimming()
    {
        // Arrange
        var recordType = Saved(RecordType.Create(Ticket.Key, "Bug", EventActor.System, Now).Value);

        // Act
        var result = recordType.Rename(" Bug ", EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        recordType.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Rename_ShouldFail_ForAManagedType()
    {
        // Arrange
        var recordType = Saved(RecordType.CreateManaged(Ticket.Key, "Bug", "bug", EventActor.System, Now).Value);

        // Act
        var result = recordType.Rename("Defect", EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        recordType.Name.Should().Be("Bug");
        recordType.DomainEvents.Should().BeEmpty();
    }

    #endregion Rename

    #region Activation

    [Fact]
    public void Deactivate_ShouldStopTheTypeBeingGiven()
    {
        // Arrange
        var recordType = Saved(RecordType.Create(Ticket.Key, "Bug", EventActor.System, Now).Value);

        // Act
        var result = recordType.Deactivate(EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        recordType.IsActive.Should().BeFalse();
        recordType.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RecordTypeDeactivatedEvent>();
    }

    [Fact]
    public void Deactivate_ShouldRaiseNothing_WhenAlreadyInactive()
    {
        // Arrange
        var recordType = Saved(RecordType.Create(Ticket.Key, "Bug", EventActor.System, Now).Value);
        recordType.Deactivate(EventActor.System, Now);
        recordType.ClearDomainEvents();

        // Act
        var result = recordType.Deactivate(EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        recordType.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Activate_ShouldRestoreAnInactiveType()
    {
        // Arrange
        var recordType = Saved(RecordType.Create(Ticket.Key, "Bug", EventActor.System, Now).Value);
        recordType.Deactivate(EventActor.System, Now);
        recordType.ClearDomainEvents();

        // Act
        var result = recordType.Activate(EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        recordType.IsActive.Should().BeTrue();
        recordType.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RecordTypeActivatedEvent>();
    }

    [Fact]
    public void Activate_ShouldRaiseNothing_WhenAlreadyActive()
    {
        // Arrange
        var recordType = Saved(RecordType.Create(Ticket.Key, "Bug", EventActor.System, Now).Value);

        // Act
        var result = recordType.Activate(EventActor.System, Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        recordType.DomainEvents.Should().BeEmpty();
    }

    #endregion Activation

    #region Managed

    private static RecordType ManagedBug() =>
        Saved(RecordType.CreateManaged(Ticket.Key, "Bug", "conn-1:bug", EventActor.Sync(null), Now).Value);

    [Fact]
    public void CreateManaged_ShouldFail_WhenTheOwnerTypesTypesAreNotSynced()
    {
        // Act
        var result = RecordType.CreateManaged(Authored.Key, "Bug", "conn-1:bug", EventActor.Sync(null), Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Authored record types are not kept by a sync.");
    }

    [Fact]
    public void SyncFromSource_ShouldTakeTheSourcesName()
    {
        // Arrange
        var recordType = ManagedBug();

        // Act
        var result = recordType.SyncFromSource(" Defect ", EventActor.Sync(null), Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        recordType.Name.Should().Be("Defect");
        recordType.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RecordTypeRenamedEvent>()
            .Which.PreviousName.Should().Be("Bug");
    }

    [Fact]
    public void SyncFromSource_ShouldRaiseNothing_WhenTheSourceIsUnchanged()
    {
        // Arrange
        var recordType = ManagedBug();

        // Act
        var result = recordType.SyncFromSource("Bug", EventActor.Sync(null), Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        recordType.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RetireFromSource_ShouldDeactivate_AndSyncFromSourceShouldReinstate()
    {
        // Arrange
        var recordType = ManagedBug();

        // Act
        recordType.RetireFromSource(EventActor.Sync(null), Now);
        var retired = recordType.IsActive;
        recordType.SyncFromSource("Bug", EventActor.Sync(null), Now);

        // Assert
        retired.Should().BeFalse();
        recordType.IsActive.Should().BeTrue();
        recordType.DomainEvents.Select(e => e.GetType()).Should().Equal(typeof(RecordTypeDeactivatedEvent), typeof(RecordTypeActivatedEvent));
    }

    [Fact]
    public void SyncFromSource_ShouldFail_ForAnOwnedType()
    {
        // Arrange
        var recordType = Saved(RecordType.Create(Ticket.Key, "Bug", EventActor.System, Now).Value);

        // Act
        var result = recordType.SyncFromSource("Defect", EventActor.Sync(null), Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        recordType.Name.Should().Be("Bug");
        recordType.RetireFromSource(EventActor.Sync(null), Now).IsFailure.Should().BeTrue();
        recordType.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_ShouldFail_ForAManagedType()
    {
        // Arrange
        var recordType = ManagedBug();

        // Act
        var result = recordType.Deactivate(EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        recordType.IsActive.Should().BeTrue();
        recordType.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Activate_ShouldFail_ForARetiredManagedType()
    {
        // Arrange
        var recordType = ManagedBug();
        recordType.RetireFromSource(EventActor.Sync(null), Now);
        recordType.ClearDomainEvents();

        // Act
        var result = recordType.Activate(EventActor.System, Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        recordType.IsActive.Should().BeFalse();
    }

    #endregion Managed
}
