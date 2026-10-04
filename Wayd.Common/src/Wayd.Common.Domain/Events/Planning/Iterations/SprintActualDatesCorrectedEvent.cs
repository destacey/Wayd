using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Iterations;

/// <summary>
/// The team corrected when the sprint actually started or completed, after the fact. Distinct from
/// <see cref="SprintStartedEvent"/> and <see cref="SprintCompletedEvent"/> because it moves the commitment point
/// of a sprint already measured, which changes its say/do ratio.
/// </summary>
public sealed record SprintActualDatesCorrectedEvent : DomainEvent<SprintActualDatesCorrectedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.ScheduleChanged;

    public SprintActualDatesCorrectedEvent(Guid id, int key, SprintActualDates previous, SprintActualDates current, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Previous = previous;
        Current = current;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The actual dates the correction replaced.</summary>
    public SprintActualDates Previous { get; }

    /// <summary>The actual dates after the correction. A null value takes the sprint's default.</summary>
    public SprintActualDates Current { get; }

    [JsonIgnore]
    public string AggregateType => "Iteration";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
