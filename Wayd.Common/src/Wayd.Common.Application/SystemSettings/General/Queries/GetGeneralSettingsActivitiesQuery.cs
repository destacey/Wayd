using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings.General.Queries;

/// <summary>
/// The general settings' change history, newest first. Empty until the section is first saved.
/// </summary>
public sealed record GetGeneralSettingsActivitiesQuery(PageRequest Paging)
    : IQuery<PagedResponse<ActivityLogDto>>, IPagedQuery;

/// <summary>Bounds the page a caller may ask for.</summary>
public sealed class GetGeneralSettingsActivitiesQueryValidator : PagedQueryValidator<GetGeneralSettingsActivitiesQuery>;

public sealed class GetGeneralSettingsActivitiesQueryHandler(IActivityLogReader activityLogReader)
    : IQueryHandler<GetGeneralSettingsActivitiesQuery, PagedResponse<ActivityLogDto>>
{
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public Task<PagedResponse<ActivityLogDto>> Handle(GetGeneralSettingsActivitiesQuery request, CancellationToken cancellationToken) =>
        _activityLogReader.Read(
            SystemSettingsSection.IdFor<GeneralSettings>(),
            aggregateType: "SystemSettingsSection",
            paging: request.Paging,
            cancellationToken: cancellationToken);
}
