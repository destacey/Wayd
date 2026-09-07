using System.Linq.Expressions;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Domain.Models.Iterations;

namespace Wayd.Planning.Application.Iterations.Queries;

/// <summary>
/// Retrieves activity history for a sprint, newest first.
/// </summary>
public sealed record GetSprintActivitiesQuery : IQuery<Result<PagedResponse<ActivityLogDto>?>>
{
    public GetSprintActivitiesQuery(IdOrKey idOrKey, int page = 1, int pageSize = 50)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Iteration>();
        Page = page;
        PageSize = pageSize;
    }

    public Expression<Func<Iteration, bool>> IdOrKeyFilter { get; }
    public int Page { get; }
    public int PageSize { get; }
}

public sealed class GetSprintActivitiesQueryHandler(
    IPlanningDbContext planningDbContext,
    IActivityLogReader activityLogReader)
    : IQueryHandler<GetSprintActivitiesQuery, Result<PagedResponse<ActivityLogDto>?>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public async Task<Result<PagedResponse<ActivityLogDto>?>> Handle(
        GetSprintActivitiesQuery request, CancellationToken cancellationToken)
    {
        var sprintId = await _planningDbContext.Iterations
            .Where(request.IdOrKeyFilter)
            .Where(i => i.Type == IterationType.Sprint)
            .Select(i => (Guid?)i.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (sprintId is null)
        {
            return Result.Success<PagedResponse<ActivityLogDto>?>(null);
        }

        var activities = await _activityLogReader.Read(
            sprintId.Value,
            aggregateType: "Iteration",
            page: request.Page,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
