using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

public sealed record ProjectDeletedEvent : DomainEvent, IAggregateEvent
{
    public ProjectDeletedEvent(Guid id, EventActor actor, Instant timestamp)
        : base(actor)
    {
        Id = id;

        Timestamp = timestamp;
    }

    public Guid Id { get; }

    [JsonIgnore]
    public string AggregateType => "Project";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
