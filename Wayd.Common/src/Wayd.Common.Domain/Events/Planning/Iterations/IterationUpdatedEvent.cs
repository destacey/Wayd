using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Interfaces.Planning.Iterations;
using Wayd.Common.Domain.Models.Planning.Iterations;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Iterations;

public sealed record IterationUpdatedEvent : DomainEvent, ISimpleIteration
{
    public IterationUpdatedEvent(ISimpleIteration iteration, Instant timestamp)
        : this(iteration.Id, iteration.Key, iteration.Name, iteration.Type, iteration.State, iteration.DateRange, iteration.TeamId, timestamp)
    {
    }

    // Deserialization constructor for the Wolverine durable outbox (STJ binds parameters to properties by
    // name; the primary constructor's `iteration` parameter cannot be bound).
    [JsonConstructor]
    public IterationUpdatedEvent(Guid id, int key, string name, IterationType type, IterationState state, IterationDateRange dateRange, Guid? teamId, Instant timestamp)
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
    public IterationDateRange DateRange { get; }
    public Guid? TeamId { get; }
}
