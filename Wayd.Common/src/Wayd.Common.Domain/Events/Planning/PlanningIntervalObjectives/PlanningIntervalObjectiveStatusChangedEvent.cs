using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.Planning;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.PlanningIntervalObjectives;

/// <summary>
/// A planning interval objective moved to a different status.
/// </summary>
/// <remarks>
/// Completed, Canceled and Missed close the objective. Moving into one of them sets the closed date, and moving
/// back out clears it, so the event carries the closed date at both ends.
/// </remarks>
public sealed record PlanningIntervalObjectiveStatusChangedEvent : DomainEvent<PlanningIntervalObjectiveStatusChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StatusChanged;

    [JsonConstructor]
    public PlanningIntervalObjectiveStatusChangedEvent(
        Guid id,
        int key,
        ObjectiveStatus fromStatus,
        ObjectiveStatus toStatus,
        Instant? previousClosedDate,
        Instant? closedDate,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        PreviousClosedDate = previousClosedDate;
        ClosedDate = closedDate;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public ObjectiveStatus FromStatus { get; }
    public ObjectiveStatus ToStatus { get; }
    public Instant? PreviousClosedDate { get; }
    public Instant? ClosedDate { get; }

    [JsonIgnore]
    public string AggregateType => "PlanningIntervalObjective";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
