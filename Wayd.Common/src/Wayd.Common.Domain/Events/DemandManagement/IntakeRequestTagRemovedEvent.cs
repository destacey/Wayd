using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// A tag no request carried was deleted from an intake request tag axis.
/// </summary>
public sealed record IntakeRequestTagRemovedEvent : DomainEvent<IntakeRequestTagRemovedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    public IntakeRequestTagRemovedEvent(Guid id, Guid tagId, string name, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        TagId = tagId;
        Name = name;

        Timestamp = timestamp;
    }

    /// <summary>The axis the tag was on.</summary>
    public Guid Id { get; }
    public Guid TagId { get; }

    /// <summary>The tag's name, which nothing else records once it is gone.</summary>
    public string Name { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequestTagCategory";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
