using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Models;

namespace Wayd.Common.Domain.Events.Planning.PlanningIntervals;

/// <summary>
/// A planning interval's descriptive details taken together, as
/// <see cref="PlanningIntervalDetailsUpdatedEvent.Previous"/> records the values a change replaced.
/// </summary>
public sealed record PlanningIntervalDetails(string Name, string? Description);

/// <summary>An iteration of a planning interval, as a creation or baseline records it.</summary>
public sealed record PlanningIntervalIterationValues(Guid IterationId, string Name, IterationCategory Category, LocalDateRange DateRange);

/// <summary>
/// An iteration's descriptive details taken together, as
/// <see cref="PlanningIntervalIterationDetailsUpdatedEvent.Previous"/> records the values a change replaced.
/// </summary>
public sealed record PlanningIntervalIterationDetails(string Name, IterationCategory Category);

/// <summary>A team sprint mapped to an iteration of a planning interval.</summary>
public sealed record PlanningIntervalSprintMapping(Guid IterationId, Guid SprintId);
