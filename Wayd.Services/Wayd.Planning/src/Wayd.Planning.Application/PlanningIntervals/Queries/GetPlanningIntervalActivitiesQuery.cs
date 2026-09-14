using System.Linq.Expressions;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;

namespace Wayd.Planning.Application.PlanningIntervals.Queries;

/// <summary>
/// Retrieves activity history for a planning interval, newest first.
/// </summary>
public sealed record GetPlanningIntervalActivitiesQuery : IQuery<Result<PagedResponse<ActivityLogDto>?>>
{
    public GetPlanningIntervalActivitiesQuery(IdOrKey idOrKey, int page = 1, int pageSize = 50)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<PlanningInterval>();
        Page = page;
        PageSize = pageSize;
    }

    public Expression<Func<PlanningInterval, bool>> IdOrKeyFilter { get; }
    public int Page { get; }
    public int PageSize { get; }
}

public sealed class GetPlanningIntervalActivitiesQueryHandler(
    IPlanningDbContext planningDbContext,
    IActivityLogReader activityLogReader)
    : IQueryHandler<GetPlanningIntervalActivitiesQuery, Result<PagedResponse<ActivityLogDto>?>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public async Task<Result<PagedResponse<ActivityLogDto>?>> Handle(
        GetPlanningIntervalActivitiesQuery request, CancellationToken cancellationToken)
    {
        var planningIntervalId = await _planningDbContext.PlanningIntervals
            .Where(request.IdOrKeyFilter)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (planningIntervalId is null)
        {
            return Result.Success<PagedResponse<ActivityLogDto>?>(null);
        }

        var activities = await _activityLogReader.Read(
            planningIntervalId.Value,
            aggregateType: "PlanningInterval",
            page: request.Page,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
