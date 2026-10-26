using System.Linq.Expressions;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Work.Domain.Models;
using Wayd.Work.Application.Persistence;

namespace Wayd.Work.Application.Iterations.Queries;

/// <summary>
/// Retrieves activity history for a sprint, newest first.
/// </summary>
public sealed record GetSprintActivitiesQuery : IQuery<Result<PagedResponse<ActivityLogDto>?>>, IPagedQuery
{
    public GetSprintActivitiesQuery(IdOrKey idOrKey, PageRequest paging)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Iteration>();
        Paging = paging;
    }

    public Expression<Func<Iteration, bool>> IdOrKeyFilter { get; }
    public PageRequest Paging { get; }
}

/// <summary>Bounds the page a caller may ask for.</summary>
public sealed class GetSprintActivitiesQueryValidator : PagedQueryValidator<GetSprintActivitiesQuery>;

public sealed class GetSprintActivitiesQueryHandler(
    IWorkDbContext workDbContext,
    IActivityLogReader activityLogReader)
    : IQueryHandler<GetSprintActivitiesQuery, Result<PagedResponse<ActivityLogDto>?>>
{
    private readonly IWorkDbContext _workDbContext = workDbContext;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public async Task<Result<PagedResponse<ActivityLogDto>?>> Handle(
        GetSprintActivitiesQuery request, CancellationToken cancellationToken)
    {
        var sprintId = await _workDbContext.Iterations
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
            paging: request.Paging,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
