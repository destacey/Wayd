using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake channel was added to the company's list of where requests come from.
/// </summary>
public sealed record IntakeChannelCreatedEvent : DomainEvent<IntakeChannelCreatedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Created;

    public IntakeChannelCreatedEvent(Guid id, string name, string? description, int order, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Name = name;
        Description = description;
        Order = order;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public string Name { get; }

    /// <summary>What belongs in the channel, when described.</summary>
    public string? Description { get; }

    /// <summary>Position in the list a requester chooses from, lowest first.</summary>
    public int Order { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeChannel";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
