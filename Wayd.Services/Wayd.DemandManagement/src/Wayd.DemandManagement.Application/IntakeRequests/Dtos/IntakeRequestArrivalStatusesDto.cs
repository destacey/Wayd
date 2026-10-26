using Wayd.Common.Application.StatusWorkflows.Dtos;

namespace Wayd.DemandManagement.Application.IntakeRequests.Dtos;

/// <summary>
/// The statuses an intake request can arrive in when moved to a desk, and the one it arrives in unless the
/// mover chooses.
/// </summary>
public sealed record IntakeRequestArrivalStatusesDto
{
    /// <summary>The destination workflow's open statuses that need no reason, in the workflow's order.</summary>
    public required IReadOnlyList<StatusNavigationDto> Statuses { get; init; }

    /// <summary>
    /// The status the request arrives in unless the mover chooses another: the one it holds, when the desk
    /// shares its workflow, otherwise the one with the same meaning or name. Null when none fits, and the mover
    /// must choose.
    /// </summary>
    public Guid? DefaultStatusId { get; init; }
}
