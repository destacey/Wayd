using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An archived intake channel was brought back into use: new requests can be given it again.
/// </summary>
public sealed record IntakeChannelRestoredEvent : DomainEvent<IntakeChannelRestoredEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    public IntakeChannelRestoredEvent(Guid id, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;

        Timestamp = timestamp;
    }

    public Guid Id { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeChannel";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
