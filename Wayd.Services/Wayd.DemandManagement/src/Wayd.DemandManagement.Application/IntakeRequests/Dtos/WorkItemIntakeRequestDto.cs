using Wayd.Common.Application.StatusWorkflows.Dtos;

namespace Wayd.DemandManagement.Application.IntakeRequests.Dtos;

/// <summary>
/// An intake request a work item delivers, as listed on the work item's page. Summary tier only.
/// </summary>
public sealed record WorkItemIntakeRequestDto
{
    /// <summary>The request.</summary>
    public required IntakeRequestNavigationDto Request { get; init; }

    /// <summary>Where the request stands in its desk's review workflow.</summary>
    public required StatusNavigationDto Status { get; init; }

    /// <summary>What the work item covers of the request, in the agent's words.</summary>
    public string? Note { get; init; }
}
