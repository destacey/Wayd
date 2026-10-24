using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake request was given a different channel.
/// </summary>
public sealed record IntakeRequestChannelChangedEvent : DomainEvent<IntakeRequestChannelChangedEvent>, IDomainEventDescriptor, IIntakeRequestEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public static IntakeRequestReadTier ReadTier => IntakeRequestReadTier.Summary;

    public IntakeRequestChannelChangedEvent(Guid id, int key, Guid fromChannelId, Guid toChannelId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        FromChannelId = fromChannelId;
        ToChannelId = toChannelId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid FromChannelId { get; }
    public Guid ToChannelId { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequest";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
