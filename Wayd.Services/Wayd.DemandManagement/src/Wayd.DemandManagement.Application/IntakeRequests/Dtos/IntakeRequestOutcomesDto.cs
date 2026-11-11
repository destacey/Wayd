using Wayd.Common.Domain.Enums.DemandManagement;

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

    /// <summary>
    /// How far delivery of the request has got, rolled up from its outcomes' signals: any blocked is blocked; else
    /// any in progress is in progress; else any not started is not started; else any done is done; else cancelled.
    /// Null when no outcome reports a signal — the request has none yet, or every target has been deleted. Derived
    /// when read.
    /// </summary>
    public DeliverySignal? DeliveryState { get; init; }
}
