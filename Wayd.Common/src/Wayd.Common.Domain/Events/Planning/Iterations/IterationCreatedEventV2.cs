using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Interfaces.Planning.Iterations;
using Wayd.Common.Domain.Models.Planning.Iterations;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Iterations;

/// <summary>
/// An iteration was created.
/// </summary>
/// <remarks>
/// Supersedes <see cref="IterationCreatedEvent"/>, whose <c>DateRange</c> carried instants. The planned dates are
/// calendar days, so a consumer can place them in whichever zone applies to it. A new type rather than a new
/// version, because retyping a field breaks every consumer written against the old shape.
/// </remarks>
public sealed record IterationCreatedEventV2 : DomainEvent<IterationCreatedEventV2>, IDomainEventDescriptor, ISimpleIteration, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    public IterationCreatedEventV2(ISimpleIteration iteration, EventActor actor, Instant timestamp)
        : this(iteration.Id, iteration.Key, iteration.Name, iteration.Type, iteration.State, iteration.DateRange, iteration.TeamId, actor, timestamp)
    {
    }

    // Deserialization constructor for the Wolverine durable outbox (STJ binds parameters to properties by
    // name; the primary constructor's `iteration` parameter cannot be bound).
    [JsonConstructor]
    public IterationCreatedEventV2(Guid id, int key, string name, IterationType type, IterationState state, IterationDateRange dateRange, Guid? teamId, EventActor actor, Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Type = type;
        State = state;
        DateRange = dateRange;
        TeamId = teamId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public IterationType Type { get; }
    public IterationState State { get; }

    /// <summary>The planned first and last days, both included.</summary>
    public IterationDateRange DateRange { get; }

    public Guid? TeamId { get; }

    [JsonIgnore]
    public string AggregateType => "Iteration";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
