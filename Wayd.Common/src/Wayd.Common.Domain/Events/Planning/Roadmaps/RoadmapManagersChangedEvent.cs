using System.Text.Json.Serialization;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Roadmaps;

/// <summary>
/// Managers were added to or removed from a roadmap.
/// </summary>
public sealed record RoadmapManagersChangedEvent : DomainEvent<RoadmapManagersChangedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public RoadmapManagersChangedEvent(
        Guid id,
        int key,
        Guid[] added,
        Guid[] removed,
        Guid[] managerIds,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        Added = [.. added];
        Removed = [.. removed];
        ManagerIds = [.. managerIds];

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }

    /// <summary>The employees who became managers.</summary>
    public Guid[] Added { get; }

    /// <summary>The employees who stopped being managers.</summary>
    public Guid[] Removed { get; }

    /// <summary>Every manager after the change.</summary>
    public Guid[] ManagerIds { get; }

    [JsonIgnore]
    public string AggregateType => "Roadmap";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
