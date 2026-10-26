using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings.Scheduling.Queries;

/// <summary>
/// The scheduling settings' change history, newest first. Empty until the section is first saved.
/// </summary>
public sealed record GetSchedulingSettingsActivitiesQuery(PageRequest Paging)
    : IQuery<PagedResponse<ActivityLogDto>>, IPagedQuery;

/// <summary>Bounds the page a caller may ask for.</summary>
public sealed class GetSchedulingSettingsActivitiesQueryValidator : PagedQueryValidator<GetSchedulingSettingsActivitiesQuery>;

public sealed class GetSchedulingSettingsActivitiesQueryHandler(IActivityLogReader activityLogReader)
    : IQueryHandler<GetSchedulingSettingsActivitiesQuery, PagedResponse<ActivityLogDto>>
{
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public Task<PagedResponse<ActivityLogDto>> Handle(GetSchedulingSettingsActivitiesQuery request, CancellationToken cancellationToken) =>
        _activityLogReader.Read(
            SystemSettingsSection.IdFor<SchedulingSettings>(),
            aggregateType: "SystemSettingsSection",
            paging: request.Paging,
            cancellationToken: cancellationToken);
}
