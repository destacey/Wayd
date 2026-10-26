using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings.SprintMetrics.Queries;

/// <summary>
/// The sprint metrics settings' change history, newest first. Empty until the section is first saved.
/// </summary>
public sealed record GetSprintMetricsSettingsActivitiesQuery(PageRequest Paging)
    : IQuery<PagedResponse<ActivityLogDto>>, IPagedQuery;

/// <summary>Bounds the page a caller may ask for.</summary>
public sealed class GetSprintMetricsSettingsActivitiesQueryValidator : PagedQueryValidator<GetSprintMetricsSettingsActivitiesQuery>;

public sealed class GetSprintMetricsSettingsActivitiesQueryHandler(IActivityLogReader activityLogReader)
    : IQueryHandler<GetSprintMetricsSettingsActivitiesQuery, PagedResponse<ActivityLogDto>>
{
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public Task<PagedResponse<ActivityLogDto>> Handle(GetSprintMetricsSettingsActivitiesQuery request, CancellationToken cancellationToken) =>
        _activityLogReader.Read(
            SystemSettingsSection.IdFor<SprintMetricsSettings>(),
            aggregateType: "SystemSettingsSection",
            paging: request.Paging,
            cancellationToken: cancellationToken);
}
