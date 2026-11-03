using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Roadmaps;

/// <summary>
/// Owners were added to or removed from a roadmap.
/// </summary>
/// <remarks>
/// Replaces <see cref="RoadmapManagersChangedEvent"/>, raised before roadmap managers were renamed owners.
/// </remarks>
public sealed record RoadmapOwnersChangedEvent : DomainEvent<RoadmapOwnersChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public RoadmapOwnersChangedEvent(
        Guid id,
        int key,
        Guid[] added,
        Guid[] removed,
        Guid[] ownerIds,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Added = [.. added];
        Removed = [.. removed];
        OwnerIds = [.. ownerIds];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The employees who became owners.</summary>
    public Guid[] Added { get; }

    /// <summary>The employees who stopped being owners.</summary>
    public Guid[] Removed { get; }

    /// <summary>Every owner after the change.</summary>
    public Guid[] OwnerIds { get; }

    [JsonIgnore]
    public string AggregateType => "Roadmap";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
