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
/// Supersedes <see cref="IterationCreatedEventV2"/>, which carried a <c>State</c>. An iteration's state is worked
/// out from its dates whenever it is read, so it is not part of the record and a payload can't carry it.
/// </remarks>
public sealed record IterationCreatedEventV3 : DomainEvent<IterationCreatedEventV3>, IDomainEventDescriptor, ISimpleIteration, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    public IterationCreatedEventV3(ISimpleIteration iteration, EventActor actor, Instant timestamp)
        : this(iteration.Id, iteration.Key, iteration.Name, iteration.Type, iteration.DateRange, iteration.TeamId, actor, timestamp)
    {
    }

    // Deserialization constructor for the Wolverine durable outbox (STJ binds parameters to properties by
    // name; the primary constructor's `iteration` parameter cannot be bound).
    [JsonConstructor]
    public IterationCreatedEventV3(Guid id, int key, string name, IterationType type, IterationDateRange dateRange, Guid? teamId, EventActor actor, Instant timestamp)
        : base(actor, "3.0")
    {
        Id = id;
        Key = key;
        Name = name;
        Type = type;
        DateRange = dateRange;
        TeamId = teamId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public IterationType Type { get; }

    /// <summary>The planned first and last days, both included.</summary>
    public IterationDateRange DateRange { get; }

    public Guid? TeamId { get; }

    [JsonIgnore]
    public string AggregateType => "Iteration";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
