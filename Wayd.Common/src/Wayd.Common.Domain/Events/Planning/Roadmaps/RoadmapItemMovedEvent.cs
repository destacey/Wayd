using System.Text.Json.Serialization;
using Wayd.Common.Domain.Enums.Planning;
using NodaTime;

namespace Wayd.Common.Domain.Events.Planning.Roadmaps;

/// <summary>
/// A roadmap item was moved beneath a different activity, or to or from the roadmap's root.
/// </summary>
/// <remarks>
/// Moving an activity renumbers its siblings at both ends to close the gap and make room, which follows from
/// the move and is not recorded separately. Its dates and its new ancestors' may move too, recorded by the
/// <see cref="RoadmapItemDatesChangedEvent"/> raised alongside this one.
/// </remarks>
public sealed record RoadmapItemMovedEvent : DomainEvent<RoadmapItemMovedEvent>, IDomainEventDescriptor, IAggregateEvent
{
    public static ActivityCategory ActivityCategory => ActivityCategory.Updated;

    [JsonConstructor]
    public RoadmapItemMovedEvent(
        Guid id,
        int key,
        Guid itemId,
        RoadmapItemType itemType,
        Guid? previousParentId,
        Guid? parentId,
        int? previousOrder,
        int? order,
        EventActor actor,
        Instant timestamp)
        : base(actor, "1.0")
    {
        Id = id;
        Key = key;
        ItemId = itemId;
        ItemType = itemType;
        PreviousParentId = previousParentId;
        ParentId = parentId;
        PreviousOrder = previousOrder;
        Order = order;

        Timestamp = timestamp;
    }

    public Guid Id { get; }
    public int Key { get; }
    public Guid ItemId { get; }
    public RoadmapItemType ItemType { get; }

    /// <summary>The activity it sat beneath, or null at the roadmap's root.</summary>
    public Guid? PreviousParentId { get; }

    /// <summary>The activity it now sits beneath, or null at the roadmap's root.</summary>
    public Guid? ParentId { get; }

    /// <summary>An activity's position among its previous siblings; null for a milestone or timebox.</summary>
    public int? PreviousOrder { get; }

    /// <summary>An activity's position among its new siblings; null for a milestone or timebox.</summary>
    public int? Order { get; }

    [JsonIgnore]
    public string AggregateType => "Roadmap";
    [JsonIgnore]
    public Guid AggregateId => Id;
}
