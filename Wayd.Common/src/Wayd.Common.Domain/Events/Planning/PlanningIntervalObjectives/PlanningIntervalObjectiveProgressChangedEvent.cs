using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.PlanningIntervalObjectives;

/// <summary>
/// The progress reported against a planning interval objective changed.
/// </summary>
public sealed record PlanningIntervalObjectiveProgressChangedEvent : DomainEvent<PlanningIntervalObjectiveProgressChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public PlanningIntervalObjectiveProgressChangedEvent(
        Guid id,
        int key,
        double previousProgress,
        double progress,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        PreviousProgress = previousProgress;
        Progress = progress;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The percentage complete before the change, from 0 to 100.</summary>
    public double PreviousProgress { get; }

    /// <summary>The percentage complete after the change, from 0 to 100.</summary>
    public double Progress { get; }

    [JsonIgnore]
    public string AggregateType => "PlanningIntervalObjective";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
