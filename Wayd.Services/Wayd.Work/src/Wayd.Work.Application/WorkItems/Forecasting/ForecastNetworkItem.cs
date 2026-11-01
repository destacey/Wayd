using Wayd.Common.Domain.Enums.Work;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Work.Application.WorkItems.Forecasting;

internal sealed record ForecastNetworkItem(
    Guid Id,
    WorkItemKey Key,
    string Title,
    Guid? TeamId,
    StatusCategory StatusCategory,
    WorkTypeTier? Tier,
    double StackRank)
{
    public bool IsOpen => StatusCategory is StatusCategory.Proposed or StatusCategory.Active;

    public bool IsBacklogItem => Tier == WorkTypeTier.Requirement;
}
