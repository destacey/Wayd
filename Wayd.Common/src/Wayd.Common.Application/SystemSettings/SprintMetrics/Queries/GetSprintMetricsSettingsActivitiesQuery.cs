using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings.SprintMetrics.Queries;

/// <summary>
/// The sprint metrics settings' change history, newest first. Empty until the section is first saved.
/// </summary>
public sealed record GetSprintMetricsSettingsActivitiesQuery(int Page = 1, int PageSize = 50)
    : IQuery<PagedResponse<ActivityLogDto>>;

public sealed class GetSprintMetricsSettingsActivitiesQueryHandler(IActivityLogReader activityLogReader)
    : IQueryHandler<GetSprintMetricsSettingsActivitiesQuery, PagedResponse<ActivityLogDto>>
{
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public Task<PagedResponse<ActivityLogDto>> Handle(GetSprintMetricsSettingsActivitiesQuery request, CancellationToken cancellationToken) =>
        _activityLogReader.Read(
            SystemSettingsSection.IdFor<SprintMetricsSettings>(),
            aggregateType: "SystemSettingsSection",
            page: request.Page,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);
}
