using System.Linq.Expressions;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.WorkProcesses.Queries;

/// <summary>
/// Retrieves activity history for a work process, newest first.
/// </summary>
public sealed record GetWorkProcessActivitiesQuery : IQuery<Result<PagedResponse<ActivityLogDto>?>>, IPagedQuery
{
    public GetWorkProcessActivitiesQuery(IdOrKey idOrKey, PageRequest paging)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<WorkProcess>();
        Paging = paging;
    }

    public Expression<Func<WorkProcess, bool>> IdOrKeyFilter { get; }
    public PageRequest Paging { get; }
}

/// <summary>Bounds the page a caller may ask for.</summary>
public sealed class GetWorkProcessActivitiesQueryValidator : PagedQueryValidator<GetWorkProcessActivitiesQuery>;

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
            paging: request.Paging,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
