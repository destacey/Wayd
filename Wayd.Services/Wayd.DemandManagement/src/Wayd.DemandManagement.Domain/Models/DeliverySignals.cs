using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Enums.ProjectPortfolioManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.DemandManagement.Domain.Models;

/// <summary>
/// How each kind of target's own status maps to a <see cref="DeliverySignal"/>, and how several signals roll
/// up into one. Defined once, so a request's page, its list and an upstream system cannot disagree.
/// </summary>
public static class DeliverySignals
{
    /// <summary>The signal of a project, by where its status stands in its lifecycle.</summary>
    public static DeliverySignal Of(LifecycleCategory category) => category switch
    {
        LifecycleCategory.NotStarted => DeliverySignal.NotStarted,
        LifecycleCategory.Active => DeliverySignal.InProgress,
        LifecycleCategory.Completed => DeliverySignal.Done,
        LifecycleCategory.Canceled => DeliverySignal.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "A lifecycle category with no delivery signal."),
    };

    /// <summary>The signal of a work item, or of any record on a status workflow, by its status category.</summary>
    public static DeliverySignal Of(StatusCategory category) => category switch
    {
        StatusCategory.Proposed => DeliverySignal.NotStarted,
        StatusCategory.Active => DeliverySignal.InProgress,
        StatusCategory.Done => DeliverySignal.Done,
        StatusCategory.Removed => DeliverySignal.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "A status category with no delivery signal."),
    };

    /// <summary>
    /// The signal of a project deliverable: by the well-known meaning of its status, or by the status's category
    /// for a stage a company added.
    /// </summary>
    public static DeliverySignal Of(ProjectDeliverableStatusAlias alias, StatusCategory category) => alias switch
    {
        ProjectDeliverableStatusAlias.NotStarted => DeliverySignal.NotStarted,
        ProjectDeliverableStatusAlias.InProgress => DeliverySignal.InProgress,
        ProjectDeliverableStatusAlias.Accepted => DeliverySignal.Done,
        ProjectDeliverableStatusAlias.Descoped => DeliverySignal.Cancelled,
        _ => Of(category),
    };

    /// <summary>
    /// One signal for several: any blocked is blocked; else any in progress is in progress; else any not started
    /// is not started; else any done is done; else cancelled. Null when there are none.
    /// </summary>
    /// <remarks>
    /// Work still waiting outranks work finished, so a request reads done only once nothing it became is still
    /// to come; cancelled wins only when everything was dropped.
    /// </remarks>
    public static DeliverySignal? Rollup(IEnumerable<DeliverySignal> signals)
    {
        var present = signals.ToHashSet();
        if (present.Count == 0)
            return null;

        if (present.Contains(DeliverySignal.Blocked))
            return DeliverySignal.Blocked;
        if (present.Contains(DeliverySignal.InProgress))
            return DeliverySignal.InProgress;
        if (present.Contains(DeliverySignal.NotStarted))
            return DeliverySignal.NotStarted;
        if (present.Contains(DeliverySignal.Done))
            return DeliverySignal.Done;
        return DeliverySignal.Cancelled;
    }
}
