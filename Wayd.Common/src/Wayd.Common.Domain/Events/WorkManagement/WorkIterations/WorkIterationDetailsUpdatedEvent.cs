using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Common.Domain.Models.Planning.Iterations;
using NodaTime;

namespace Wayd.Common.Domain.Events.WorkManagement.WorkIterations;

/// <summary>
/// The Work copy of an iteration's name or type changed. Supersedes, with the other <c>WorkIteration*Changed</c> events,
/// <see cref="WorkIterationUpdatedEvent"/>.
/// </summary>
public sealed record WorkIterationDetailsUpdatedEvent : DomainEvent<WorkIterationDetailsUpdatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public WorkIterationDetailsUpdatedEvent(Guid id, int key, string name, IterationType type, IterationDetails? previous, EventActor actor, Instant timestamp)
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
    public string AggregateType => "WorkIteration";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
