using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models.Organizations;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

public sealed record TeamActivatedEvent : DomainEvent, IAggregateEvent
{
    public TeamActivatedEvent(Guid id, int key, TeamCode? code, EventActor actor, Instant timestamp)
        : base(actor, "1.1")
    {
        Id = id;
        Key = key;
        Code = code;
        Timestamp = timestamp;
    }

    public Guid Id { get; }

    /// <summary>Added in 1.0 -> 1.1. Zero on a payload written before 1.1, which did not record it.</summary>
    public int Key { get; }

    /// <summary>Added in 1.0 -> 1.1. Null on a payload written before 1.1, which did not record it.</summary>
    public TeamCode? Code { get; }

    [JsonIgnore]
    public string AggregateType => "Team";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
