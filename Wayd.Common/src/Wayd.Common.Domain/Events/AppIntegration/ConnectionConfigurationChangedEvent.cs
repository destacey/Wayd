using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.AppIntegration;

/// <summary>
/// One or more of a connection's non-secret settings changed. Carries every setting before and after, so a
/// reader sees which moved; a credential change is <see cref="ConnectionCredentialsChangedEvent"/>.
/// </summary>
public sealed record ConnectionConfigurationChangedEvent : DomainEvent<ConnectionConfigurationChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ConnectionConfigurationChangedEvent(Guid id, ConnectionSetting[] settings, ConnectionSetting[] previous, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Settings = [.. settings];
        Previous = [.. previous];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public ConnectionSetting[] Settings { get; }

    /// <summary>The settings this change replaced.</summary>
    public ConnectionSetting[] Previous { get; }

    [JsonIgnore]
    public string AggregateType => "Connection";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
