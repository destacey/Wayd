using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Iterations;

/// <summary>
/// The team reopened a completed sprint, clearing the completion it had recorded.
/// </summary>
public sealed record SprintReopenedEvent : DomainEvent<SprintReopenedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StatusChanged;

    public SprintReopenedEvent(Guid id, int key, Instant previousCompleted, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        PreviousCompleted = previousCompleted;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The completion that reopening cleared.</summary>
    public Instant PreviousCompleted { get; }

    [JsonIgnore]
    public string AggregateType => "Iteration";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
