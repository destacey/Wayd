using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.AppIntegration;

/// <summary>
/// A connection's name or description was edited.
/// </summary>
public sealed record ConnectionDetailsUpdatedEvent : DomainEvent<ConnectionDetailsUpdatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ConnectionDetailsUpdatedEvent(Guid id, string name, string? description, ConnectionDetails previous, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Name = name;
        Description = description;
        Previous = previous;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public string Name { get; }
    public string? Description { get; }

    /// <summary>The details this edit replaced.</summary>
    public ConnectionDetails Previous { get; }

    [JsonIgnore]
    public string AggregateType => "Connection";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
