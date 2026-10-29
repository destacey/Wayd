using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.DemandManagement;

/// <summary>
/// A free intake request tag nothing carried any more was deleted: its last use was removed, or it was merged into another
/// tag.
/// </summary>
public sealed record IntakeRequestFreeTagDeletedEvent : DomainEvent<IntakeRequestFreeTagDeletedEvent>, IDomainEventDescriptor, IDemandManagementEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Removed;

    public IntakeRequestFreeTagDeletedEvent(Guid id, string name, Guid? mergedIntoTagId, EventActor actor, Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Name = name;
        MergedIntoTagId = mergedIntoTagId;

        Timestamp = timestamp;
    }

    /// <summary>The free tag.</summary>
    public Guid Id { get; }

    /// <summary>The tag's name, which nothing else records once it is gone.</summary>
    public string Name { get; }

    /// <summary>The tag its records were moved to, when the deletion completed a merge.</summary>
    public Guid? MergedIntoTagId { get; }

    [JsonIgnore]
    public string AggregateType => "IntakeRequestTag";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
