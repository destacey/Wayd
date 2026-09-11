using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models.Organizations;
using NodaTime;

namespace Wayd.Common.Domain.Events.Organization;

public sealed record TeamUpdatedEvent : DomainEvent, IAggregateEvent
{
    public TeamUpdatedEvent(Guid id, TeamCode code, string name, string? description, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Code = code;
        Name = name;
        Description = description;
        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public TeamCode Code { get; }
    public string Name { get; }
    public string? Description { get; }

    [JsonIgnore]
    public string AggregateType => "Team";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
