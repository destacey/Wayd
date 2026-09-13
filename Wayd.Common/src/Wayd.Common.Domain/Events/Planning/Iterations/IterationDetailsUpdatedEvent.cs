using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Models.Planning.Iterations;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Iterations;

/// <summary>
/// The iteration's name or type changed. Supersedes, with the other <c>Iteration*Changed</c> events,
/// <see cref="IterationUpdatedEvent"/>.
/// </summary>
public sealed record IterationDetailsUpdatedEvent : DomainEvent, IAggregateEvent
{
    public IterationDetailsUpdatedEvent(Guid id, int key, string name, IterationType type, IterationDetails? previous, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Type = type;
        Previous = previous;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public IterationType Type { get; }

    /// <summary>
    /// The details this change replaced. Grouped so that null can only mean "not recorded", as
    /// <c>ProjectDetailsUpdatedEvent.Previous</c> is.
    /// </summary>
    public IterationDetails? Previous { get; }

    [JsonIgnore]
    public string AggregateType => "Iteration";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
