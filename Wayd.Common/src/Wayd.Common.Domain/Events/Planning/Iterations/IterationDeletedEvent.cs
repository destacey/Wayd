using System.Text.Json.Serialization;
using Wayd.Common.Domain.Events;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Iterations;

public sealed record IterationDeletedEvent : DomainEvent, IAggregateEvent
{
    public IterationDeletedEvent(Guid id, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;

        Timestamp = timestamp;
    }

    public Guid Id { get; }

    [JsonIgnore]
    public string AggregateType => "Iteration";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
