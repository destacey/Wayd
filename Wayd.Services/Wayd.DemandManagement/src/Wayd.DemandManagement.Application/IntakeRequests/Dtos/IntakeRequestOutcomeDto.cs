using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.DemandProjects.Dtos;

namespace Wayd.DemandManagement.Application.IntakeRequests.Dtos;

/// <summary>
/// One thing an intake request became. Summary tier.
/// </summary>
public sealed record IntakeRequestOutcomeDto
{
    /// <summary>What the request became.</summary>
    public IntakeRequestOutcomeKind Kind { get; init; }

    /// <summary>The id of the project the outcome points at, which identifies the outcome on the request.</summary>
    public Guid TargetId { get; init; }

    /// <summary>The project, or null when it has since been deleted; the outcome stays as the record of what was decided.</summary>
    public DemandProjectNavigationDto? Project { get; init; }

    /// <summary>What the outcome covers, in the agent's words. Always present for existing project scope.</summary>
    public string? Note { get; init; }

    /// <summary>The employee who recorded the outcome.</summary>
    public required EmployeeNavigationDto AddedBy { get; init; }

    /// <summary>When the outcome was recorded.</summary>
    public Instant AddedOn { get; init; }
}
