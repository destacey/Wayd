using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings.ProductTags.Queries;

/// <summary>
/// The product tag settings' change history, newest first. Empty until the section is first saved.
/// </summary>
public sealed record GetProductTagSettingsActivitiesQuery(PageRequest Paging)
    : IQuery<PagedResponse<ActivityLogDto>>, IPagedQuery;

/// <summary>Bounds the page a caller may ask for.</summary>
public sealed class GetProductTagSettingsActivitiesQueryValidator : PagedQueryValidator<GetProductTagSettingsActivitiesQuery>;

public sealed class GetProductTagSettingsActivitiesQueryHandler(IActivityLogReader activityLogReader)
    : IQueryHandler<GetProductTagSettingsActivitiesQuery, PagedResponse<ActivityLogDto>>
{
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public Task<PagedResponse<ActivityLogDto>> Handle(GetProductTagSettingsActivitiesQuery request, CancellationToken cancellationToken) =>
        _activityLogReader.Read(
            SystemSettingsSection.IdFor<ProductTagSettings>(),
            aggregateType: "SystemSettingsSection",
            paging: request.Paging,
            cancellationToken: cancellationToken);
}
