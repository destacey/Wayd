namespace Wayd.DemandManagement.Application.IntakeRequests.Dtos;

/// <summary>
/// One intake request as its reader may see it: the summary always, the substance only when they may read it.
/// </summary>
/// <remarks>
/// The tiers are separate objects rather than nullable fields on one, so a reader never receives a field
/// they may not see and a client never mistakes a withheld value for an empty one. The internal tier arrives
/// with the discussion thread, whose internal notes are its first content.
/// </remarks>
public sealed record IntakeRequestDetailsDto
{
    /// <summary>The summary tier.</summary>
    public required IntakeRequestSummaryDto Summary { get; init; }

    /// <summary>The substance tier, or null when the reader may not see it.</summary>
    public IntakeRequestSubstanceDto? Substance { get; init; }

    /// <summary>Whether the reader may edit the request as it stands.</summary>
    public bool CanEdit { get; init; }

    /// <summary>Whether the reader may remove the request's participants: an agent on its desk.</summary>
    public bool CanRemoveParticipants { get; init; }

    /// <summary>Whether the reader may delete the request: a Desk Admin on its desk.</summary>
    public bool CanDelete { get; init; }

    /// <summary>
    /// Whether the reader may move the request to another desk: an agent on its current desk, while the request
    /// is open.
    /// </summary>
    public bool CanReroute { get; init; }

    /// <summary>
    /// Whether the reader may give someone access to the request as a participant: an agent on its desk, or
    /// its requester or submitter unless the desk is private and does not allow it.
    /// </summary>
    public bool CanAddParticipants { get; init; }
}
