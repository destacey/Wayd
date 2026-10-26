using Wayd.Common.Application.Employees.Dtos;

namespace Wayd.DemandManagement.Application.IntakeRequests.Dtos;

/// <summary>
/// The substance tier of an intake request: the ask itself and its own people.
/// </summary>
/// <remarks>
/// Returned only to readers who may see it, never as empty or redacted fields: on an open desk everyone who
/// can view requests, otherwise the request's own people and anyone who reviews on any desk.
/// </remarks>
public sealed record IntakeRequestSubstanceDto
{
    /// <summary>The ask in full.</summary>
    public required string Description { get; init; }

    /// <summary>What gets better, and for whom, if the ask is delivered.</summary>
    public string? ExpectedBenefits { get; init; }

    /// <summary>The employee who entered the request, who differs from the requester when it was submitted on their behalf.</summary>
    public required EmployeeNavigationDto SubmittedBy { get; init; }

    /// <summary>The people given access to the request beyond its requester and submitter.</summary>
    public required IReadOnlyList<IntakeRequestParticipantDto> Participants { get; init; }

    /// <summary>The upstream record the request arrived from, when it did not start in Wayd.</summary>
    public IntakeRequestOriginDto? Origin { get; init; }

    /// <summary>
    /// Why the request is in its current status: the desk's reason for declining, deferring, marking a duplicate
    /// or finishing it, or the requester's note on withdrawing. Null when the move recorded none.
    /// </summary>
    public string? StatusReason { get; init; }
}
