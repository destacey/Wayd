using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Models;

namespace Wayd.Common.Domain.Events.Planning.Roadmaps;

/// <summary>
/// A roadmap's descriptive details taken together, as <see cref="RoadmapDetailsUpdatedEvent.Previous"/> records
/// the values an edit replaced.
/// </summary>
public sealed record RoadmapDetails(string Name, string? Description);

/// <summary>
/// One of a roadmap's configured colors. The hex value is its natural key.
/// </summary>
public sealed record RoadmapColorValues(string Color, string Name, int Order, bool IsDefault);

/// <summary>
/// An item of a roadmap — an activity, milestone or timebox — as a creation or baseline records it.
/// </summary>
/// <param name="DateRange">The item's dates. A milestone falls on a single day, so its range starts and ends on it.</param>
/// <param name="Order">The activity's position among its siblings; null for a milestone or timebox, which are unordered.</param>
public sealed record RoadmapItemValues(
    Guid ItemId,
    RoadmapItemType Type,
    string Name,
    string? Description,
    Guid? ParentId,
    string? Color,
    LocalDateRange DateRange,
    int? Order);

/// <summary>
/// A roadmap item's descriptive details taken together, as <see cref="RoadmapItemDetailsUpdatedEvent.Previous"/>
/// records the values an edit replaced.
/// </summary>
public sealed record RoadmapItemDetails(string Name, string? Description, string? Color);

/// <summary>
/// One roadmap item whose dates moved, with both ends. A milestone falls on a single day, so its range starts
/// and ends on it.
/// </summary>
public sealed record RoadmapItemDateChange(
    Guid ItemId,
    RoadmapItemType Type,
    LocalDateRange PreviousDateRange,
    LocalDateRange DateRange);

/// <summary>
/// A roadmap item removed along with the one deleted, because it sat beneath it.
/// </summary>
public sealed record RoadmapItemReference(Guid ItemId, RoadmapItemType Type);
