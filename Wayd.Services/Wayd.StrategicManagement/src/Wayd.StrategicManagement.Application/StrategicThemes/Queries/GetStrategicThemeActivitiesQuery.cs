using System.Linq.Expressions;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.StrategicManagement.Domain.Models;

namespace Wayd.StrategicManagement.Application.StrategicThemes.Queries;

/// <summary>
/// Retrieves activity history for a strategic theme, newest first.
/// </summary>
public sealed record GetStrategicThemeActivitiesQuery : IQuery<Result<PagedResponse<ActivityLogDto>?>>
{
    public GetStrategicThemeActivitiesQuery(IdOrKey idOrKey, int page = 1, int pageSize = 50)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<StrategicTheme>();
        Page = page;
        PageSize = pageSize;
    }

    public Expression<Func<StrategicTheme, bool>> IdOrKeyFilter { get; }
    public int Page { get; }
    public int PageSize { get; }
}

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
            page: request.Page,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
