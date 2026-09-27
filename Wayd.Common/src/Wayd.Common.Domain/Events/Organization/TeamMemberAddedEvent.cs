using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

/// <summary>
/// An employee joined a team or team of teams, in one or more roles.
/// </summary>
public sealed record TeamMemberAddedEvent : DomainEvent<TeamMemberAddedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public TeamMemberAddedEvent(Guid id, int key, Guid employeeId, IReadOnlyCollection<Guid> roleIds, EventActor actor, Instant timestamp)
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

    /// <summary>The team member roles the employee joined in.</summary>
    public IReadOnlyCollection<Guid> RoleIds { get; }

    [JsonIgnore]
    public string AggregateType => "Team";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
