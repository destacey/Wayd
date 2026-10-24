using Wayd.Common.Application.Employees.Dtos;

namespace Wayd.DemandManagement.Application.IntakeRequests.Dtos;

/// <summary>
/// A participant on an intake request: who, and who added them when.
/// </summary>
public sealed record IntakeRequestParticipantDto
{
    /// <summary>The employee given access.</summary>
    public required EmployeeNavigationDto Employee { get; init; }

    /// <summary>The employee who added them, or null when the platform did.</summary>
    public EmployeeNavigationDto? AddedBy { get; init; }

    /// <summary>When they were added.</summary>
    public Instant AddedOn { get; init; }
}
