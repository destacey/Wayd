using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake channel moved to another position in the list a requester chooses from.
/// </summary>
/// <remarks>
/// Raised by each channel whose position changed, so reordering the list records one entry per channel moved.
/// </remarks>
public sealed record IntakeChannelReorderedEvent : DomainEvent<IntakeChannelReorderedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public IntakeChannelReorderedEvent(Guid id, int fromOrder, int toOrder, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        FromOrder = fromOrder;
        ToOrder = toOrder;

        Timestamp = timestamp;
    }

    public Guid Id { get; }

    /// <summary>The channel's position before the move, lowest first.</summary>
    public int FromOrder { get; }

    /// <summary>The channel's position after the move, lowest first.</summary>
    public int ToOrder { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeChannel";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
