using System.Linq.Expressions;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.StrategicManagement.Domain.Models;

namespace Wayd.StrategicManagement.Application.StrategicThemes.Queries;

/// <summary>
/// Retrieves activity history for a strategic theme, newest first.
/// </summary>
public sealed record GetStrategicThemeActivitiesQuery : IQuery<Result<PagedResponse<ActivityLogDto>?>>, IPagedQuery
{
    public GetStrategicThemeActivitiesQuery(IdOrKey idOrKey, PageRequest paging)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<StrategicTheme>();
        Paging = paging;
    }

    public Expression<Func<StrategicTheme, bool>> IdOrKeyFilter { get; }
    public PageRequest Paging { get; }
}

/// <summary>Bounds the page a caller may ask for.</summary>
public sealed class GetStrategicThemeActivitiesQueryValidator : PagedQueryValidator<GetStrategicThemeActivitiesQuery>;

public sealed class GetStrategicThemeActivitiesQueryHandler(
    IStrategicManagementDbContext strategicManagementDbContext,
    IActivityLogReader activityLogReader)
    : IQueryHandler<GetStrategicThemeActivitiesQuery, Result<PagedResponse<ActivityLogDto>?>>
{
    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public async Task<Result<PagedResponse<ActivityLogDto>?>> Handle(
        GetStrategicThemeActivitiesQuery request, CancellationToken cancellationToken)
    {
        var strategicThemeId = await _strategicManagementDbContext.StrategicThemes
            .Where(request.IdOrKeyFilter)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (strategicThemeId is null)
        {
            return Result.Success<PagedResponse<ActivityLogDto>?>(null);
        }

        var activities = await _activityLogReader.Read(
            strategicThemeId.Value,
            aggregateType: "StrategicTheme",
            paging: request.Paging,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
