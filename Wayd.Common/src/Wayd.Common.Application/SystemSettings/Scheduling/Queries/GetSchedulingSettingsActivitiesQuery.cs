using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings.Scheduling.Queries;

/// <summary>
/// The scheduling settings' change history, newest first. Empty until the section is first saved.
/// </summary>
public sealed record GetSchedulingSettingsActivitiesQuery(int Page = 1, int PageSize = 50)
    : IQuery<PagedResponse<ActivityLogDto>>;

public sealed class GetSchedulingSettingsActivitiesQueryHandler(IActivityLogReader activityLogReader)
    : IQueryHandler<GetSchedulingSettingsActivitiesQuery, PagedResponse<ActivityLogDto>>
{
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public Task<PagedResponse<ActivityLogDto>> Handle(GetSchedulingSettingsActivitiesQuery request, CancellationToken cancellationToken) =>
        _activityLogReader.Read(
            SystemSettingsSection.IdFor<SchedulingSettings>(),
            aggregateType: "SystemSettingsSection",
            page: request.Page,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);
}
