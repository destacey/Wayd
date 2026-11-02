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

    /// <summary>Whether the reader may delete the request: a Desk Admin on its desk, while it has never left its initial status.</summary>
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

    /// <summary>
    /// Whether the reader works the request's desk: an agent or Desk Admin on it, or the Demand Management Admin.
    /// Says who the reader is, not what they may do; the capability flags answer that. A duplicate's agents stay
    /// on it to review it, and everyone else is taken to the request it gives way to.
    /// </summary>
    public bool IsDeskAgent { get; init; }

    /// <summary>Whether the reader may move the request to another status: an agent on its desk.</summary>
    public bool CanChangeStatus { get; init; }

    /// <summary>Whether the reader may assign the request to a member of its desk, or unassign it: an agent on its desk.</summary>
    public bool CanChangeAssignee { get; init; }

    /// <summary>
    /// Whether the reader may apply and remove the request's tags: an agent on its desk. Everyone who sees the
    /// request sees its tags.
    /// </summary>
    public bool CanChangeTags { get; init; }

    /// <summary>
    /// What the request became, oldest first. Summary tier, so everyone who sees the request sees them.
    /// </summary>
    public IReadOnlyList<IntakeRequestOutcomeDto> Outcomes { get; init; } = [];

    /// <summary>
    /// Whether the reader may add and remove outcomes: an agent on its desk, while the request is accepted.
    /// </summary>
    public bool CanChangeOutcomes { get; init; }

    /// <summary>
    /// Whether the reader may set, change or clear the request's estimate: an agent on its desk, while the request
    /// is open, and only when the desk has an estimation scale or the request has an estimate to clear. Never the
    /// requester.
    /// </summary>
    public bool CanChangeEstimate { get; init; }

    /// <summary>
    /// The estimate values of the scale version the request's desk pins, in order, for a reader who may change the
    /// estimate; empty otherwise, and when the desk does not estimate its requests.
    /// </summary>
    public IReadOnlyList<string> EstimateValues { get; init; } = [];

    /// <summary>
    /// The scale version the request's desk pins, which the estimate values come from, for a reader who
    /// may change the estimate; null otherwise, and when the desk does not estimate its requests. An estimate on
    /// any other version can be kept or cleared, but a value sent is always given on this one, even when the
    /// older version wrote the same value.
    /// </summary>
    public Guid? EstimationScaleVersionId { get; init; }

    /// <summary>Whether the reader may withdraw the request: its requester, while it is open.</summary>
    public bool CanWithdraw { get; init; }

    /// <summary>
    /// The request this one is a duplicate of, while its status means Duplicate and the reader can see that
    /// request; its page shows that one instead. Null otherwise, so a request the reader cannot see is never
    /// named or counted.
    /// </summary>
    public IntakeRequestNavigationDto? DuplicateOf { get; init; }
}
