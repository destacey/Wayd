namespace Wayd.DemandManagement.Application.IntakeRequests.Dtos;

/// <summary>
/// What one intake request became, answered beside the requests list. Summary tier, like the outcomes.
/// </summary>
public sealed record IntakeRequestOutcomesDto
{
    /// <summary>The request the outcomes belong to.</summary>
    public Guid RequestId { get; init; }

    /// <summary>The projects and work items the request became, oldest first. Never empty.</summary>
    public IReadOnlyList<IntakeRequestOutcomeDto> Outcomes { get; init; } = [];

    /// <summary>
    /// Whether the request is met by the deliverables its outcomes are mapped to: true when every mapped deliverable
    /// still on its project is Accepted, false while any is not, and null when no outcome maps one.
    /// </summary>
    public bool? DeliverablesMet { get; init; }
}
