using System.Linq.Expressions;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkProcesses.Queries;

/// <summary>
/// Retrieves activity history for a work process, newest first.
/// </summary>
public sealed record GetWorkProcessActivitiesQuery : IQuery<Result<PagedResponse<ActivityLogDto>?>>
{
    public GetWorkProcessActivitiesQuery(IdOrKey idOrKey, int page = 1, int pageSize = 50)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<WorkProcess>();
        Page = page;
        PageSize = pageSize;
    }

    public Expression<Func<WorkProcess, bool>> IdOrKeyFilter { get; }
    public int Page { get; }
    public int PageSize { get; }
}

public sealed class GetWorkProcessActivitiesQueryHandler(
    IWorkDbContext workDbContext,
    IActivityLogReader activityLogReader)
    : IQueryHandler<GetWorkProcessActivitiesQuery, Result<PagedResponse<ActivityLogDto>?>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public async Task<Result<PagedResponse<ActivityLogDto>?>> Handle(
        GetWorkProcessActivitiesQuery request, CancellationToken cancellationToken)
    {
        var workProcessId = await _workDbContext.WorkProcesses
            .Where(request.IdOrKeyFilter)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (workProcessId is null)
        {
            return Result.Success<PagedResponse<ActivityLogDto>?>(null);
        }

        var activities = await _activityLogReader.Read(
            workProcessId.Value,
            aggregateType: "WorkProcess",
            page: request.Page,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
