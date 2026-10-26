using Wayd.Common.Application.StatusWorkflows.Dtos;

namespace Wayd.DemandManagement.Application.IntakeRequests.Dtos;

/// <summary>
/// A status an agent can move an intake request to, with what the move needs.
/// </summary>
/// <remarks>
/// The needs come from the status's meaning and category rather than its name, so they hold whatever a desk's
/// workflow calls its statuses; the change command applies the same rules.
/// </remarks>
public sealed record IntakeRequestStatusOptionDto : StatusNavigationDto
{
    /// <summary>Whether the move needs a reason the requester can read: declining, deferring, marking a duplicate or finishing.</summary>
    public bool NeedsReason { get; init; }

    /// <summary>Whether the move needs the request this one duplicates.</summary>
    public bool NeedsSurvivingRequest { get; init; }
}
