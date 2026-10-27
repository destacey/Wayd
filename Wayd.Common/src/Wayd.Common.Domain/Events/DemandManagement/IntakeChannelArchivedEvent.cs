using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake channel was archived: no new request can be given it, and no desk offers it. Requests already
/// carrying it keep it.
/// </summary>
public sealed record IntakeChannelArchivedEvent : DomainEvent<IntakeChannelArchivedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.StateChanged;

    public IntakeChannelArchivedEvent(Guid id, EventActor actor, Instant timestamp)
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
