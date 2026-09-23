using Wayd.Common.Application.Dtos;

namespace Wayd.Work.Application.WorkItems.Dtos;

public sealed record ForecastIssueDto
{
    public required ForecastWorkItemDto WorkItem { get; init; }

    /// <summary>
    /// A <see cref="ForecastIssueType"/>.
    /// </summary>
    public required SimpleNavigationDto Type { get; init; }
}
