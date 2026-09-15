using System.Linq.Expressions;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;

namespace Wayd.Planning.Application.PlanningIntervals.Queries;

/// <summary>
/// Retrieves activity history for a planning interval objective, newest first. The objective must belong to
/// the planning interval named alongside it.
/// </summary>
public sealed record GetPlanningIntervalObjectiveActivitiesQuery : IQuery<Result<PagedResponse<ActivityLogDto>?>>
{
    public GetPlanningIntervalObjectiveActivitiesQuery(IdOrKey idOrKey, IdOrKey objectiveIdOrKey, int page = 1, int pageSize = 50)
    {
        PlanningIntervalIdOrKeyFilter = idOrKey.CreateFilter<PlanningInterval>();
        ObjectiveIdOrKeyFilter = objectiveIdOrKey.CreateFilter<PlanningIntervalObjective>();
        Page = page;
        PageSize = pageSize;
    }

    public Expression<Func<PlanningInterval, bool>> PlanningIntervalIdOrKeyFilter { get; }
    public Expression<Func<PlanningIntervalObjective, bool>> ObjectiveIdOrKeyFilter { get; }
    public int Page { get; }
    public int PageSize { get; }
}

public sealed class GetPlanningIntervalObjectiveActivitiesQueryHandler(
    IPlanningDbContext planningDbContext,
    IActivityLogReader activityLogReader)
    : IQueryHandler<GetPlanningIntervalObjectiveActivitiesQuery, Result<PagedResponse<ActivityLogDto>?>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public async Task<Result<PagedResponse<ActivityLogDto>?>> Handle(
        GetPlanningIntervalObjectiveActivitiesQuery request, CancellationToken cancellationToken)
    {
        var planningIntervalId = await _planningDbContext.PlanningIntervals
            .Where(request.PlanningIntervalIdOrKeyFilter)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (planningIntervalId is null)
        {
            return Result.Success<PagedResponse<ActivityLogDto>?>(null);
        }

        var objectiveId = await _planningDbContext.PlanningIntervalObjectives
            .Where(o => o.PlanningIntervalId == planningIntervalId.Value)
            .Where(request.ObjectiveIdOrKeyFilter)
            .Select(o => (Guid?)o.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (objectiveId is null)
        {
            return Result.Success<PagedResponse<ActivityLogDto>?>(null);
        }

        var activities = await _activityLogReader.Read(
            objectiveId.Value,
            aggregateType: "PlanningIntervalObjective",
            page: request.Page,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
