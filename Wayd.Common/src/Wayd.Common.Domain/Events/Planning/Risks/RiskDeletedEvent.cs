using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Risks;

/// <summary>
/// A risk was deleted.
/// </summary>
/// <remarks>
/// The summary is carried because the record it describes is gone by the time anyone reads the entry.
/// </remarks>
public sealed record RiskDeletedEvent : DomainEvent<RiskDeletedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    [JsonConstructor]
    public RiskDeletedEvent(Guid id, int key, Guid? teamId, string summary, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        TeamId = teamId;
        Summary = summary;

        Timestamp = timestamp;
    }

    /// <summary>The deleted risk's id.</summary>
    public Guid Id { get; }

    /// <summary>The deleted risk's key.</summary>
    public int Key { get; }

    /// <summary>The team or team of teams that owned the risk.</summary>
    public Guid? TeamId { get; }

    /// <summary>The risk's summary at the moment it was deleted.</summary>
    public string Summary { get; }

    [JsonIgnore]
    public string AggregateType => "Risk";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
