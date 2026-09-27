using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// An employee left a team or team of teams, giving up every role they held on it.
/// </summary>
public sealed record TeamMemberRemovedEvent : DomainEvent<TeamMemberRemovedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public TeamMemberRemovedEvent(Guid id, int key, Guid employeeId, IReadOnlyCollection<Guid> roleIds, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        EmployeeId = employeeId;
        RoleIds = roleIds;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid EmployeeId { get; }

    /// <summary>The roles the employee held until they left.</summary>
    public IReadOnlyCollection<Guid> RoleIds { get; }

    [JsonIgnore]
    public string AggregateType => "Team";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
