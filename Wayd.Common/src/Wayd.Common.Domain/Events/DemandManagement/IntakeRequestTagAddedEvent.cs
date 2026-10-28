using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// A tag was added to an intake request tag axis.
/// </summary>
public sealed record IntakeRequestTagAddedEvent : DomainEvent<IntakeRequestTagAddedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public IntakeRequestTagAddedEvent(Guid id, Guid tagId, string name, string? description, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        TagId = tagId;
        Name = name;
        Description = description;

        Timestamp = timestamp;
    }

    /// <summary>The axis the tag was added to.</summary>
    public Guid Id { get; }
    public Guid TagId { get; }

    /// <summary>The tag's name.</summary>
    public string Name { get; }

    /// <summary>What the tag means, when described.</summary>
    public string? Description { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequestTagCategory";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
