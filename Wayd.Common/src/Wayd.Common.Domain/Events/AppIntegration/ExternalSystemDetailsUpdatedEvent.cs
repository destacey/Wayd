using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.AppIntegration;

/// <summary>
/// An outside system's name or base URL changed, refreshed from a connection that reaches it.
/// </summary>
public sealed record ExternalSystemDetailsUpdatedEvent : DomainEvent<ExternalSystemDetailsUpdatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ExternalSystemDetailsUpdatedEvent(Guid id, string name, string? baseUrl, ExternalSystemDetails previous, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Name = name;
        BaseUrl = baseUrl;
        Previous = previous;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public string Name { get; }
    public string? BaseUrl { get; }

    /// <summary>The details this change replaced.</summary>
    public ExternalSystemDetails Previous { get; }

    [JsonIgnore]
    public string AggregateType => "ExternalSystem";
    [JsonIgnore]
    public Guid AggregateId => Id;
}

/// <summary>
/// An outside system's descriptive details taken together, as <see cref="ExternalSystemDetailsUpdatedEvent.Previous"/>
/// records the values a change replaced.
/// </summary>
public sealed record ExternalSystemDetails(string Name, string? BaseUrl);
