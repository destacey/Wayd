using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.AppIntegration;

/// <summary>
/// A connection's stored credential was replaced — a personal access token, API key, client secret or
/// password.
/// </summary>
/// <remarks>
/// Records that a credential changed and which one, never what it changed from or to, not even hashed.
/// </remarks>
public sealed record ConnectionCredentialsChangedEvent : DomainEvent<ConnectionCredentialsChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public ConnectionCredentialsChangedEvent(Guid id, string[] credentials, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Credentials = [.. credentials];

        Timestamp = timestamp;
    }

    public Guid Id { get; }

    /// <summary>The names of the credentials that were replaced, as the connector names them.</summary>
    public string[] Credentials { get; }

    [JsonIgnore]
    public string AggregateType => "Connection";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
