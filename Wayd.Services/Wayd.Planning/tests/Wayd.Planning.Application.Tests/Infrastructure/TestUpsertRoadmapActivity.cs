using Wayd.Common.Models;
using Wayd.Planning.Domain.Interfaces.Roadmaps;

namespace Wayd.Planning.Application.Tests.Infrastructure;

/// <summary>
/// A roadmap activity to create or update in a handler or validator test: the dates and parent the test is
/// about, with a valid name and no description or color.
/// </summary>
internal sealed record TestUpsertRoadmapActivity(LocalDateRange DateRange, Guid? ParentId = null) : IUpsertRoadmapActivity
{
    /// <summary>
    /// The activity's name; any valid name.
    /// </summary>
    public string Name { get; init; } = "Activity";

    /// <summary>
    /// No description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// No color.
    /// </summary>
    public string? Color { get; init; }
}
