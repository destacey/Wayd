using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Domain.Enums.DemandManagement;

namespace Wayd.DemandManagement.Application.IntakeDesks.Dtos;

/// <summary>
/// One member of an intake desk and what they may do on it.
/// </summary>
public sealed record IntakeDeskMemberDto
{
    /// <summary>The member, by name.</summary>
    public required EmployeeNavigationDto Employee { get; init; }

    /// <summary>What the member may do on the desk: Admin or Agent.</summary>
    public IntakeDeskRole Role { get; init; }

    /// <summary>
    /// Whether the member triages the desk's new requests, and so is told about requests waiting on it unassigned.
    /// Independent of the role.
    /// </summary>
    public bool IsTriager { get; init; }
}
