using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Risks;

/// <summary>
/// A risk was assigned, reassigned or unassigned.
/// </summary>
public sealed record RiskAssigneeChangedEvent : DomainEvent<RiskAssigneeChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public RiskAssigneeChangedEvent(
        Guid id,
        int key,
        Guid? previousAssigneeId,
        Guid? assigneeId,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        PreviousAssigneeId = previousAssigneeId;
        AssigneeId = assigneeId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The employee the risk was assigned to before the change.</summary>
    public Guid? PreviousAssigneeId { get; }

    /// <summary>The employee the risk is assigned to after the change.</summary>
    public Guid? AssigneeId { get; }

    [JsonIgnore]
    public string AggregateType => "Risk";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
