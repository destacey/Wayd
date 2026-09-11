using System.Text.Json.Serialization;
using Wayd.Common.Domain.Events;
using NodaTime;

namespace Wayd.Common.Domain.Events.ProjectPortfolioManagement;

public sealed record ProgramDeletedEvent : DomainEvent, IPpmEvent
{
    public ProgramDeletedEvent(Guid id, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;

        Timestamp = timestamp;
    }

    public Guid Id { get; }

    [JsonIgnore]
    public string AggregateType => "Program";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
