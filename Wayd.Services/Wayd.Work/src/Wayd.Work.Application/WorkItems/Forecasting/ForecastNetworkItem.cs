using Wayd.Common.Domain.Enums.Work;

namespace Wayd.Work.Application.WorkItems.Forecasting;

internal sealed record ForecastNetworkItem(
    Guid Id,
    WorkItemKey Key,
    string Title,
    Guid? TeamId,
    WorkStatusCategory StatusCategory,
    WorkTypeTier? Tier,
    double StackRank)
{
    public bool IsOpen => StatusCategory is WorkStatusCategory.Proposed or WorkStatusCategory.Active;

    public bool IsBacklogItem => Tier == WorkTypeTier.Requirement;
}
