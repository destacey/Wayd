using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// Which company channels an intake desk offers its requesters changed: it switched between offering every
/// channel and only those it chose, or it chose different channels.
/// </summary>
/// <remarks>
/// A desk offering every channel also offers channels added later, so its chosen list is empty. A desk offering
/// only its chosen channels with none chosen offers none, and its submit form does not ask for a channel.
/// </remarks>
public sealed record IntakeDeskChannelsChangedEvent : DomainEvent<IntakeDeskChannelsChangedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public IntakeDeskChannelsChangedEvent(
        Guid id,
        int key,
        bool offersAllChannels,
        bool previousOffersAllChannels,
        IReadOnlyList<Guid> addedChannelIds,
        IReadOnlyList<Guid> removedChannelIds,
        IReadOnlyList<Guid> channelIds,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        OffersAllChannels = offersAllChannels;
        PreviousOffersAllChannels = previousOffersAllChannels;
        AddedChannelIds = addedChannelIds;
        RemovedChannelIds = removedChannelIds;
        ChannelIds = channelIds;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>Whether the desk now offers every active channel, including ones added later.</summary>
    public bool OffersAllChannels { get; }

    /// <summary>Whether it did before the change.</summary>
    public bool PreviousOffersAllChannels { get; }

    /// <summary>The channels newly chosen.</summary>
    public IReadOnlyList<Guid> AddedChannelIds { get; }

    /// <summary>The channels no longer chosen.</summary>
    public IReadOnlyList<Guid> RemovedChannelIds { get; }

    /// <summary>The channels chosen after the change; empty when the desk offers every channel.</summary>
    public IReadOnlyList<Guid> ChannelIds { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeDesk";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
