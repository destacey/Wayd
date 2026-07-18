using System.Text.Json.Serialization;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

public sealed record ProgramDetailsUpdatedEvent : DomainEvent, ISimpleProgram
{
    public ProgramDetailsUpdatedEvent(ISimpleProgram program, Instant timestamp)
        : this(program.Id, program.Key, program.Name, program.Description, timestamp)
    {
    }

    // Deserialization constructor for the Wolverine durable outbox (STJ binds parameters to properties by
    // name; the primary constructor's `program` parameter cannot be bound).
    [JsonConstructor]
    public ProgramDetailsUpdatedEvent(Guid id, int key, string name, string description, Instant timestamp)
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public string Name { get; }
    public string Description { get; }
}