using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.DemandManagement.Application.IntakeRequests.Dtos;

/// <summary>
/// An intake request a project came from or absorbed, as listed on the project's page. Summary tier only.
/// </summary>
public sealed record ProjectIntakeRequestDto
{
    /// <summary>The request.</summary>
    public required IntakeRequestNavigationDto Request { get; init; }

    /// <summary>Where the request stands in its desk's review workflow.</summary>
    public required StatusNavigationDto Status { get; init; }

    /// <summary>How the project relates to the request: started for it, or absorbing it into existing scope.</summary>
    public IntakeRequestOutcomeKind Kind { get; init; }

    /// <summary>What the project took on from the request, in the agent's words.</summary>
    public string? Note { get; init; }
}
