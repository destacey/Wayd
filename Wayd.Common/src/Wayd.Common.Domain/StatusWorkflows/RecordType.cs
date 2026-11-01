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
/// A variant of an owner type's records that can follow its own workflow — a Story or a Bug among work
/// items. Organization-wide per owner type.
/// </summary>
/// <remarks>
/// The engine knows only the type's name and identity. What a module attaches to a type — a work type's
/// level, a plan task being a milestone — stays in that module, keyed by this record type's id.
/// <para>
/// Only an owner type whose descriptor declares <see cref="WorkflowOwnerDescriptor.HasRecordTypes"/> has
/// them. A record type is never deleted, because records and scheme entries refer to it; one no longer
/// in use is deactivated.
/// </para>
/// </remarks>
public sealed class RecordType : BaseAuditableEntity, IHasIdAndKey
{
    private RecordType() { }

    private RecordType(string ownerType, string name, WorkflowOwnership ownership, string? externalKey)
    {
        OwnerType = ownerType;
        Name = name;
        Ownership = ownership;
        ExternalKey = externalKey;
        IsActive = true;
    }

    /// <summary>
    /// The unique auto-generated key of the record type. This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// The owner type this is a variant of, as a registered <see cref="WorkflowOwnerDescriptor.Key"/>.
    /// Fixed at creation.
    /// </summary>
    public string OwnerType { get; private init; } = default!;

    /// <summary>What the organization or the source calls this type (e.g. "Bug").</summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>Whether administrators define this type, or a sync keeps it from its source.</summary>
    public WorkflowOwnership Ownership { get; private init; }

    /// <summary>
    /// The source system's identifier for this type when it is managed, or <c>null</c> when it is owned.
    /// What the sync matches on, so a rename at the source renames the type rather than adding another.
    /// </summary>
    public string? ExternalKey { get; private init; }

    /// <summary>
    /// Whether records can still be given this type. An inactive type keeps its records and scheme
    /// entries, so history and existing records resolve as before.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>Renames an owned record type.</summary>
    /// <remarks>A managed type takes its name from its source.</remarks>
    public Result Rename(string name, EventActor actor, Instant timestamp)
    {
        if (Ownership == WorkflowOwnership.Managed)
        {
            return Result.Failure("A managed record type takes its name from its source.");
        }

        var previous = Name;
        Name = name;
        if (Name == previous)
        {
            return Result.Success();
        }

        var current = Name;
        AddKeyedDomainEvent(() => new RecordTypeRenamedEvent(Id, Key, OwnerType, current, previous, actor, timestamp));

        return Result.Success();
    }

    /// <summary>Stops records being given this type. Existing records keep it.</summary>
    public Result Deactivate(EventActor actor, Instant timestamp)
    {
        if (!IsActive)
        {
            return Result.Success();
        }

        IsActive = false;
        AddKeyedDomainEvent(() => new RecordTypeDeactivatedEvent(Id, Key, OwnerType, actor, timestamp));

        return Result.Success();
    }

    /// <summary>Lets records be given this type again.</summary>
    public Result Activate(EventActor actor, Instant timestamp)
    {
        if (IsActive)
        {
            return Result.Success();
        }

        IsActive = true;
        AddKeyedDomainEvent(() => new RecordTypeActivatedEvent(Id, Key, OwnerType, actor, timestamp));

        return Result.Success();
    }

    /// <summary>Defines a record type in Wayd for an owner type that has them.</summary>
    public static Result<RecordType> Create(string ownerType, string name, EventActor actor, Instant timestamp) =>
        Create(ownerType, name, WorkflowOwnership.Owned, externalKey: null, actor, timestamp);

    /// <summary>Records a record type a sync keeps from its source, identified there by <paramref name="externalKey"/>.</summary>
    public static Result<RecordType> CreateManaged(string ownerType, string name, string externalKey, EventActor actor, Instant timestamp)
    {
        Guard.Against.NullOrWhiteSpace(externalKey, nameof(externalKey));

        return Create(ownerType, name, WorkflowOwnership.Managed, externalKey.Trim(), actor, timestamp);
    }

    private static Result<RecordType> Create(
        string ownerType, string name, WorkflowOwnership ownership, string? externalKey, EventActor actor, Instant timestamp)
    {
        var descriptor = WorkflowOwners.Resolve(ownerType);
        if (descriptor.IsFailure)
        {
            return Result.Failure<RecordType>(descriptor.Error);
        }

        if (!descriptor.Value.HasRecordTypes)
        {
            return Result.Failure<RecordType>($"{descriptor.Value.DisplayName} records do not have record types.");
        }

        var recordType = new RecordType(descriptor.Value.Key, name, ownership, externalKey);

        var (key, typeName) = (recordType.OwnerType, recordType.Name);
        recordType.AddPostPersistenceAction(() => recordType.AddDomainEvent(new RecordTypeCreatedEvent(
            recordType.Id, recordType.Key, key, typeName, ownership, externalKey, actor, timestamp)));

        return Result.Success(recordType);
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
