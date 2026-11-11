using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests.Dtos;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests;

/// <summary>
/// Completes the delivery a request reports once its work items are known: each work item outcome's work item
/// and signal, and the request's rolled-up state.
/// </summary>
/// <remarks>
/// Demand Management keeps no copy of work items, so the API layer asks Work for them and hands them in here.
/// Every read that returns outcomes passes through it, so the state always rolls up the signals shown beside it.
/// </remarks>
public static class IntakeRequestDelivery
{
    /// <summary>
    /// The outcomes, each work item outcome with its work item and signal when Work still has it; one Work no
    /// longer has keeps its outcome, with neither.
    /// </summary>
    /// <param name="outcomes">The outcomes as Demand Management read them.</param>
    /// <param name="workItems">The work items the outcomes point at, as Work reports them now, by id.</param>
    public static IReadOnlyList<IntakeRequestOutcomeDto> WithWorkItems(
        IReadOnlyList<IntakeRequestOutcomeDto> outcomes,
        IReadOnlyDictionary<Guid, IntakeRequestOutcomeWorkItemDto> workItems) =>
        workItems.Count == 0
            ? outcomes
            :
            [
                .. outcomes.Select(o => o.Kind == IntakeRequestOutcomeKind.WorkItem && workItems.TryGetValue(o.TargetId, out var workItem)
                    ? o with { WorkItem = workItem, DeliverySignal = DeliverySignals.Of(workItem.StatusCategory) }
                    : o),
            ];

    /// <summary>The request's delivery state: the rollup of the signals its outcomes report, or null when none does.</summary>
    public static DeliverySignal? StateOf(IEnumerable<IntakeRequestOutcomeDto> outcomes) =>
        DeliverySignals.Rollup(outcomes.Select(o => o.DeliverySignal).OfType<DeliverySignal>());

    /// <summary>The request with its work items filled in and its delivery state rolled up from them.</summary>
    public static IntakeRequestDetailsDto WithWorkItems(this IntakeRequestDetailsDto request,
        IReadOnlyDictionary<Guid, IntakeRequestOutcomeWorkItemDto> workItems)
    {
        var outcomes = WithWorkItems(request.Outcomes, workItems);
        return request with { Outcomes = outcomes, DeliveryState = StateOf(outcomes) };
    }

    /// <summary>The request's outcomes with its work items filled in and its delivery state rolled up from them.</summary>
    public static IntakeRequestOutcomesDto WithWorkItems(this IntakeRequestOutcomesDto request,
        IReadOnlyDictionary<Guid, IntakeRequestOutcomeWorkItemDto> workItems)
    {
        var outcomes = WithWorkItems(request.Outcomes, workItems);
        return request with { Outcomes = outcomes, DeliveryState = StateOf(outcomes) };
    }
}
