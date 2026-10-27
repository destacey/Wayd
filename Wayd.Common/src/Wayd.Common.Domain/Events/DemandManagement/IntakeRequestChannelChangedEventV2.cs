using System.Text.Json.Serialization;
using NodaTime;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake request was given a different channel, or had its channel set or cleared.
/// </summary>
/// <remarks>
/// Replaces <see cref="IntakeRequestChannelChangedEvent"/>, whose channels were required. A request has no channel
/// when it was submitted to a desk that offers none.
/// </remarks>
public sealed record IntakeRequestChannelChangedEventV2 : DomainEvent<IntakeRequestChannelChangedEventV2>, IDomainEventDescriptor, IIntakeRequestEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public static IntakeRequestReadTier ReadTier => IntakeRequestReadTier.Summary;

    public IntakeRequestChannelChangedEventV2(Guid id, int key, Guid? fromChannelId, Guid? toChannelId, EventActor actor, Instant timestamp)
        : base(actor, "2.0")
    {
        Id = id;
        Key = key;
        FromChannelId = fromChannelId;
        ToChannelId = toChannelId;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    /// <summary>The channel before the change, or null when the request had none.</summary>
    public Guid? FromChannelId { get; }

    /// <summary>The channel after the change, or null when it was cleared.</summary>
    public Guid? ToChannelId { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequest";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
