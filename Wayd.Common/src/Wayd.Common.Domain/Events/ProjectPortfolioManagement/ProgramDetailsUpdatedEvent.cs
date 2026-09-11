using System.Text.Json.Serialization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

public sealed record ProgramDetailsUpdatedEvent : DomainEvent, ISimpleProgram, IPpmEvent
{
    public ProgramDetailsUpdatedEvent(ISimpleProgram program, ProgramDetails? previous, EventActor actor, Instant timestamp)
        : this(program.Id, program.Key, program.Name, program.Description, previous, actor, timestamp)
    {
    }

    // Deserialization constructor for the Wolverine durable outbox (STJ binds parameters to properties by
    // name; the primary constructor's `program` parameter cannot be bound).
    [JsonConstructor]
    public ProgramDetailsUpdatedEvent(Guid id, int key, string name, string description, ProgramDetails? previous, EventActor actor, Instant timestamp)
        : base(actor, "1.1")
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
        Previous = previous;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string Description { get; }

    /// <summary>
    /// Added in 1.0 -> 1.1: the details this edit replaced. Null on a payload written before 1.1, which did
    /// not record them. Grouped the way <see cref="ProjectDetailsUpdatedEvent.Previous"/> is.
    /// </summary>
    public ProgramDetails? Previous { get; }

    [JsonIgnore]
    public string AggregateType => "Program";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
