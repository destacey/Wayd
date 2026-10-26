using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// Who works an intake desk changed: members were named, removed, or given a different role.
/// </summary>
/// <remarks>
/// Membership is the grant on a desk, so this is the record of who could act on its requests from this
/// moment on. Carries the resulting members as well as the change, so the history reads without replaying it.
/// </remarks>
public sealed record IntakeDeskMembersChangedEvent : DomainEvent<IntakeDeskMembersChangedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public IntakeDeskMembersChangedEvent(Guid id, int key, IReadOnlyList<IntakeDeskMemberDetails> added,
        IReadOnlyList<IntakeDeskMemberDetails> removed, IReadOnlyList<IntakeDeskRoleChange> roleChanges,
        IReadOnlyList<IntakeDeskMemberDetails> members, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Added = added;
        Removed = removed;
        RoleChanges = roleChanges;
        Members = members;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The employees newly named on the desk, with the role they were given.</summary>
    public IReadOnlyList<IntakeDeskMemberDetails> Added { get; }

    /// <summary>The employees no longer on the desk, with the role they held.</summary>
    public IReadOnlyList<IntakeDeskMemberDetails> Removed { get; }

    /// <summary>The members who stayed on the desk in a different role.</summary>
    public IReadOnlyList<IntakeDeskRoleChange> RoleChanges { get; }

    /// <summary>Everyone on the desk after the change.</summary>
    public IReadOnlyList<IntakeDeskMemberDetails> Members { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeDesk";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
