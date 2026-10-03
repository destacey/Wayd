using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.Common.Domain.Events.AppIntegration;

/// <summary>
/// An administrator added an integration connection.
/// </summary>
/// <remarks>
/// Carries the connection's non-secret settings and no credential, not even a hash of one.
/// </remarks>
public sealed record ConnectionCreatedEvent : DomainEvent<ConnectionCreatedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    [JsonConstructor]
    public ConnectionCreatedEvent(
        Guid id,
        string name,
        string? description,
        Connector connector,
        bool isActive,
        ConnectionSetting[] settings,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Name = name;
        Description = description;
        Connector = connector;
        IsActive = isActive;
        Settings = [.. settings];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public string Name { get; }
    public string? Description { get; }
    public Connector Connector { get; }
    public bool IsActive { get; }
    public ConnectionSetting[] Settings { get; }

    [JsonIgnore]
    public string AggregateType => "Connection";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
