using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.PlanningIntervalObjectives;

/// <summary>
/// A planning interval objective's start or target date moved, was set, or was cleared.
/// </summary>
public sealed record PlanningIntervalObjectiveTimelineChangedEvent : DomainEvent<PlanningIntervalObjectiveTimelineChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.ScheduleChanged;

    [JsonConstructor]
    public PlanningIntervalObjectiveTimelineChangedEvent(
        Guid id,
        int key,
        LocalDate? previousStartDate,
        LocalDate? previousTargetDate,
        LocalDate? startDate,
        LocalDate? targetDate,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        PreviousStartDate = previousStartDate;
        PreviousTargetDate = previousTargetDate;
        StartDate = startDate;
        TargetDate = targetDate;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public LocalDate? PreviousStartDate { get; }
    public LocalDate? PreviousTargetDate { get; }
    public LocalDate? StartDate { get; }
    public LocalDate? TargetDate { get; }

    [JsonIgnore]
    public string AggregateType => "PlanningIntervalObjective";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
