using Wayd.Common.Application.Dtos;

namespace Wayd.Work.Application.WorkItems.Dtos;

public sealed record ForecastIssueDto
{
    public required ForecastWorkItemDto WorkItem { get; init; }

    /// <summary>
    /// Why the work item could not be forecast: No Team, Not a Backlog Item or Not Enough History.
    /// </summary>
    public required SimpleNavigationDto Type { get; init; }
}
