using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// A member of a team or team of teams gained or lost roles, and is still a member afterwards.
/// </summary>
public sealed record TeamMemberRolesChangedEvent : DomainEvent<TeamMemberRolesChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public TeamMemberRolesChangedEvent(Guid id, int key, Guid employeeId, IReadOnlyCollection<Guid> addedRoleIds, IReadOnlyCollection<Guid> removedRoleIds, IReadOnlyCollection<Guid> roleIds, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        EmployeeId = employeeId;
        AddedRoleIds = addedRoleIds;
        RemovedRoleIds = removedRoleIds;
        RoleIds = roleIds;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid EmployeeId { get; }
    public IReadOnlyCollection<Guid> AddedRoleIds { get; }
    public IReadOnlyCollection<Guid> RemovedRoleIds { get; }

    /// <summary>The roles the member holds after the change.</summary>
    public IReadOnlyCollection<Guid> RoleIds { get; }

    [JsonIgnore]
    public string AggregateType => "Team";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
