using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// An intake channel was renamed, or what it says belongs in it changed. Every request carrying the channel
/// reads the new name from then on.
/// </summary>
public sealed record IntakeChannelDetailsUpdatedEvent : DomainEvent<IntakeChannelDetailsUpdatedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public IntakeChannelDetailsUpdatedEvent(Guid id, string name, string? description, string previousName,
        string? previousDescription, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Name = name;
        Description = description;
        PreviousName = previousName;
        PreviousDescription = previousDescription;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public string Name { get; }

    /// <summary>What belongs in the channel, or null when it is no longer described.</summary>
    public string? Description { get; }
    public string PreviousName { get; }

    /// <summary>The description before the change, or null when it had none.</summary>
    public string? PreviousDescription { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeChannel";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
