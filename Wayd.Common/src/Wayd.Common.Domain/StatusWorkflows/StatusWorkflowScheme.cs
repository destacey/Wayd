using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using NodaTime;
using Wayd.Common.Domain.Data;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.StatusWorkflows;
using Wayd.Common.Domain.Interfaces;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Domain.StatusWorkflows;

/// <summary>
/// Which workflow each record type of an owner type follows, with a default for every type it does not
/// name. Shared: many scopes point at one scheme through their <see cref="WorkflowAssignment"/>.
/// </summary>
/// <remarks>
/// The scheme is the one answer to "which workflow does a Bug follow here" at its level of the scope
/// chain. A scope that needs a different answer is reassigned to a clone, never given an override, so
/// records of one type in one scope never diverge.
/// <para>
/// An owner type without record types never sees a scheme. Each of its workflows has an implicit,
/// default-only scheme (<see cref="IsImplicit"/>) shared by every scope that picks that workflow, and
/// administrators keep picking workflows.
/// </para>
/// <para>
/// Changing which workflow a type follows is not a migration on its own, exactly as for
/// <see cref="WorkflowAssignment.ReassignTo"/>: each change demands the complete <see cref="StatusRemap"/>
/// for the records it moves, and the caller applies it to them, in every scope on the scheme.
/// </para>
/// </remarks>
public sealed class StatusWorkflowScheme : BaseAuditableEntity, IHasIdAndKey
{
    private const string ImplicitError = "A workflow's default scheme follows its workflow and cannot be edited.";

    private readonly List<StatusWorkflowSchemeEntry> _entries = [];

    private StatusWorkflowScheme() { }

    private StatusWorkflowScheme(
        string ownerType, string name, string? description, WorkflowOwnership ownership, string? externalKey, bool isImplicit, Guid defaultWorkflowId)
    {
        OwnerType = ownerType;
        Name = name;
        Description = description;
        Ownership = ownership;
        ExternalKey = externalKey;
        IsImplicit = isImplicit;
        DefaultWorkflowId = defaultWorkflowId;
    }

    /// <summary>
    /// The unique auto-generated key of the scheme. This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>The owner type whose records this scheme sends to workflows. Fixed at creation.</summary>
    public string OwnerType { get; private init; } = default!;

    /// <summary>What the organization or the source calls this scheme.</summary>
    /// <remarks>An implicit scheme carries its workflow's name at creation and is never shown.</remarks>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>Optional explanation shown to administrators choosing between schemes.</summary>
    public string? Description
    {
        get;
        private set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>Whether administrators keep this scheme, or a sync keeps it from its source.</summary>
    /// <remarks>
    /// A managed scheme maps one-to-one to its source object (an Azure DevOps process), so one sync update
    /// reaches every scope on it.
    /// </remarks>
    public WorkflowOwnership Ownership { get; private init; }

    /// <summary>The source's identifier when managed, or <c>null</c> when owned.</summary>
    public string? ExternalKey { get; private init; }

    /// <summary>
    /// Whether this is a workflow's default-only scheme, standing behind every scope that picks that
    /// workflow directly. It has no entries, never changes its default, and is never shown as a scheme.
    /// </summary>
    public bool IsImplicit { get; private init; }

    /// <summary>The workflow a record follows when its type has no entry, or when it has no type.</summary>
    public Guid DefaultWorkflowId { get; private set; }

    /// <summary>The record types this scheme sends somewhere other than its default.</summary>
    public IReadOnlyCollection<StatusWorkflowSchemeEntry> Entries => _entries.AsReadOnly();

    /// <summary>
    /// The workflow a record of <paramref name="recordTypeId"/> follows: its type's entry, otherwise the
    /// default. A record with no type follows the default.
    /// </summary>
    public Guid WorkflowFor(Guid? recordTypeId) =>
        recordTypeId is { } typeId && _entries.SingleOrDefault(e => e.RecordTypeId == typeId) is { } entry
            ? entry.WorkflowId
            : DefaultWorkflowId;

    /// <summary>Edits the scheme's name and description.</summary>
    public Result UpdateDetails(string name, string? description, EventActor actor, Instant timestamp)
    {
        if (IsImplicit)
        {
            return Result.Failure(ImplicitError);
        }

        var previous = new WorkflowSchemeDetails(Name, Description);
        Name = name;
        Description = description;

        if (previous == new WorkflowSchemeDetails(Name, Description))
        {
            return Result.Success();
        }

        var (current, currentDescription) = (Name, Description);
        AddKeyedDomainEvent(() => new WorkflowSchemeDetailsUpdatedEvent(
            Id, Key, current, currentDescription, previous, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Sends records of <paramref name="recordType"/> to <paramref name="workflow"/>, in every scope on this
    /// scheme.
    /// </summary>
    /// <param name="remap">
    /// How each status of the workflow the type follows now translates into <paramref name="workflow"/>.
    /// Required, and required to be complete, unless the type already follows that workflow.
    /// </param>
    public Result SetEntry(RecordType recordType, StatusWorkflow workflow, StatusRemap? remap, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(recordType, nameof(recordType));

        var guard = GuardEditable();
        if (guard.IsFailure)
        {
            return guard;
        }

        if (!string.Equals(recordType.OwnerType, OwnerType, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure($"A {recordType.OwnerType} record type cannot be part of a {OwnerType} scheme.");
        }

        var assignable = GuardAssignable(workflow);
        if (assignable.IsFailure)
        {
            return assignable;
        }

        var existing = _entries.SingleOrDefault(e => e.RecordTypeId == recordType.Id);
        if (existing?.WorkflowId == workflow.Id)
        {
            return Result.Success();
        }

        var fromWorkflowId = WorkflowFor(recordType.Id);
        var mapped = GuardRemap(fromWorkflowId, workflow.Id, remap, recordType.Name);
        if (mapped.IsFailure)
        {
            return mapped;
        }

        if (existing is null)
        {
            _entries.Add(new StatusWorkflowSchemeEntry(Id, recordType.Id, workflow.Id));
        }
        else
        {
            existing.WorkflowId = workflow.Id;
        }

        var (recordTypeId, toWorkflowId, hadEntry) = (recordType.Id, workflow.Id, existing is not null);
        AddKeyedDomainEvent(() => new WorkflowSchemeEntrySetEvent(
            Id, Key, recordTypeId, fromWorkflowId, toWorkflowId, hadEntry, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Removes a record type's entry, so its records follow the scheme's default again.
    /// </summary>
    /// <param name="remap">
    /// How each status of the entry's workflow translates into the default. Required, and required to be
    /// complete, unless the entry already names the default.
    /// </param>
    public Result RemoveEntry(Guid recordTypeId, StatusRemap? remap, EventActor actor, Instant timestamp)
    {
        var guard = GuardEditable();
        if (guard.IsFailure)
        {
            return guard;
        }

        var existing = _entries.SingleOrDefault(e => e.RecordTypeId == recordTypeId);
        if (existing is null)
        {
            return Result.Success();
        }

        var mapped = GuardRemap(existing.WorkflowId, DefaultWorkflowId, remap, "this record type");
        if (mapped.IsFailure)
        {
            return mapped;
        }

        _entries.Remove(existing);

        var (fromWorkflowId, toWorkflowId) = (existing.WorkflowId, DefaultWorkflowId);
        AddKeyedDomainEvent(() => new WorkflowSchemeEntryRemovedEvent(
            Id, Key, recordTypeId, fromWorkflowId, toWorkflowId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Changes the workflow followed by every record whose type has no entry, in every scope on this scheme.
    /// </summary>
    /// <param name="remap">How each status of the current default translates into <paramref name="workflow"/>.</param>
    public Result ChangeDefault(StatusWorkflow workflow, StatusRemap remap, EventActor actor, Instant timestamp)
    {
        var guard = GuardEditable();
        if (guard.IsFailure)
        {
            return guard;
        }

        var assignable = GuardAssignable(workflow);
        if (assignable.IsFailure)
        {
            return assignable;
        }

        if (workflow.Id == DefaultWorkflowId)
        {
            return Result.Success();
        }

        var fromWorkflowId = DefaultWorkflowId;
        var mapped = GuardRemap(fromWorkflowId, workflow.Id, remap, "records with no entry");
        if (mapped.IsFailure)
        {
            return mapped;
        }

        DefaultWorkflowId = workflow.Id;

        var toWorkflowId = workflow.Id;
        AddKeyedDomainEvent(() => new WorkflowSchemeDefaultChangedEvent(Id, Key, fromWorkflowId, toWorkflowId, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Records that the scheme is being deleted. Removing it is the caller's.
    /// </summary>
    /// <param name="isAssigned">
    /// Whether any scope points at this scheme. Supplied by the caller, which owns that query.
    /// </param>
    public Result Delete(bool isAssigned, EventActor actor, Instant timestamp)
    {
        if (isAssigned)
        {
            return Result.Failure("This scheme is still assigned. Reassign those scopes to another scheme first.");
        }

        AddDomainEvent(new WorkflowSchemeDeletedEvent(Id, Key, OwnerType, actor, timestamp));

        return Result.Success();
    }

    /// <summary>
    /// Copies this scheme into a new owned one, for a scope that needs to differ: the scope is then
    /// reassigned to the copy.
    /// </summary>
    public Result<StatusWorkflowScheme> Clone(string name, string? description, EventActor actor, Instant timestamp)
    {
        if (IsImplicit)
        {
            return Result.Failure<StatusWorkflowScheme>("A workflow's default scheme cannot be cloned. Create a scheme instead.");
        }

        var clone = new StatusWorkflowScheme(
            OwnerType, name, description ?? Description, WorkflowOwnership.Owned, externalKey: null, isImplicit: false, DefaultWorkflowId);

        foreach (var entry in _entries)
        {
            clone._entries.Add(new StatusWorkflowSchemeEntry(clone.Id, entry.RecordTypeId, entry.WorkflowId));
        }

        clone.RaiseCreated(Id, actor, timestamp);

        return Result.Success(clone);
    }

    /// <summary>
    /// Creates an owned scheme for an owner type with record types, sending every type to
    /// <paramref name="defaultWorkflow"/> until entries say otherwise.
    /// </summary>
    public static Result<StatusWorkflowScheme> Create(
        string ownerType, string name, string? description, StatusWorkflow defaultWorkflow, EventActor actor, Instant timestamp) =>
        Create(ownerType, name, description, WorkflowOwnership.Owned, externalKey: null, defaultWorkflow, actor, timestamp);

    /// <summary>
    /// Records a scheme a sync keeps from its source object, identified there by <paramref name="externalKey"/>.
    /// </summary>
    public static Result<StatusWorkflowScheme> CreateManaged(
        string ownerType, string name, string externalKey, StatusWorkflow defaultWorkflow, EventActor actor, Instant timestamp)
    {
        Guard.Against.NullOrWhiteSpace(externalKey, nameof(externalKey));

        return Create(ownerType, name, description: null, WorkflowOwnership.Managed, externalKey.Trim(), defaultWorkflow, actor, timestamp);
    }

    /// <summary>
    /// Creates a workflow's default-only scheme, which stands behind every scope that picks the workflow
    /// directly. The caller ensures a workflow has only one.
    /// </summary>
    public static Result<StatusWorkflowScheme> CreateImplicit(StatusWorkflow workflow, EventActor actor, Instant timestamp)
    {
        Guard.Against.Null(workflow, nameof(workflow));

        var descriptor = WorkflowOwners.Resolve(workflow.OwnerType);
        if (descriptor.IsFailure)
        {
            return Result.Failure<StatusWorkflowScheme>(descriptor.Error);
        }

        var scheme = new StatusWorkflowScheme(
            descriptor.Value.Key, workflow.Name, description: null, WorkflowOwnership.Owned, externalKey: null, isImplicit: true, workflow.Id);
        scheme.RaiseCreated(sourceSchemeId: null, actor, timestamp);

        return Result.Success(scheme);
    }

    private static Result<StatusWorkflowScheme> Create(
        string ownerType,
        string name,
        string? description,
        WorkflowOwnership ownership,
        string? externalKey,
        StatusWorkflow defaultWorkflow,
        EventActor actor,
        Instant timestamp)
    {
        Guard.Against.Null(defaultWorkflow, nameof(defaultWorkflow));

        var descriptor = WorkflowOwners.Resolve(ownerType);
        if (descriptor.IsFailure)
        {
            return Result.Failure<StatusWorkflowScheme>(descriptor.Error);
        }

        if (!descriptor.Value.HasRecordTypes)
        {
            return Result.Failure<StatusWorkflowScheme>(
                $"{descriptor.Value.DisplayName} records do not have record types, so a scope picks a workflow rather than a scheme.");
        }

        var scheme = new StatusWorkflowScheme(
            descriptor.Value.Key, name, description, ownership, externalKey, isImplicit: false, defaultWorkflow.Id);

        var assignable = scheme.GuardAssignable(defaultWorkflow);
        if (assignable.IsFailure)
        {
            return Result.Failure<StatusWorkflowScheme>(assignable.Error);
        }

        scheme.RaiseCreated(sourceSchemeId: null, actor, timestamp);

        return Result.Success(scheme);
    }

    private Result GuardEditable() => IsImplicit ? Result.Failure(ImplicitError) : Result.Success();

    private Result GuardAssignable(StatusWorkflow workflow)
    {
        Guard.Against.Null(workflow, nameof(workflow));

        if (!string.Equals(workflow.OwnerType, OwnerType, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure($"A {workflow.OwnerType} workflow cannot govern {OwnerType} records.");
        }

        return workflow.State == StatusWorkflowState.Published
            ? Result.Success()
            : Result.Failure("Only a published workflow can be part of a scheme.");
    }

    /// <summary>
    /// Refuses a change that would leave records holding statuses their new workflow does not contain.
    /// </summary>
    private static Result GuardRemap(Guid fromWorkflowId, Guid toWorkflowId, StatusRemap? remap, string movedRecords)
    {
        if (fromWorkflowId == toWorkflowId)
        {
            return Result.Success();
        }

        if (remap is null)
        {
            return Result.Failure($"Moving {movedRecords} onto another workflow needs a mapping for every status.");
        }

        if (remap.FromWorkflowId != fromWorkflowId || remap.ToWorkflowId != toWorkflowId)
        {
            return Result.Failure("The mapping is between different workflows than this change.");
        }

        return remap.IsComplete
            ? Result.Success()
            : Result.Failure($"{remap.Unresolved.Count} status(es) have nowhere to go. Map every status first.");
    }

    /// <summary>Raises the creation event once the first save has assigned <see cref="Key"/>.</summary>
    private void RaiseCreated(Guid? sourceSchemeId, EventActor actor, Instant timestamp)
    {
        var (ownerType, name, description, ownership, externalKey, isImplicit, defaultWorkflowId) =
            (OwnerType, Name, Description, Ownership, ExternalKey, IsImplicit, DefaultWorkflowId);
        WorkflowSchemeEntryValues[] entries = [.. _entries
            .OrderBy(e => e.RecordTypeId)
            .Select(e => new WorkflowSchemeEntryValues(e.RecordTypeId, e.WorkflowId))];

        AddPostPersistenceAction(() => AddDomainEvent(new WorkflowSchemeCreatedEvent(
            Id, Key, ownerType, name, description, ownership, externalKey, isImplicit, defaultWorkflowId, entries, sourceSchemeId, actor, timestamp)));
    }

    /// <summary>
    /// Raises an event that carries <see cref="Key"/>, waiting for the first save to assign it. Only Key
    /// may be read inside <paramref name="build"/>; capture everything else in locals first.
    /// </summary>
    private void AddKeyedDomainEvent(Func<DomainEvent> build)
    {
        if (Key == 0)
            AddPostPersistenceAction(() => AddDomainEvent(build()));
        else
            AddDomainEvent(build());
    }
}
