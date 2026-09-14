using System.Linq.Expressions;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;

namespace Wayd.Planning.Application.Risks.Queries;

/// <summary>
/// Retrieves activity history for a risk, newest first.
/// </summary>
public sealed record GetRiskActivitiesQuery : IQuery<Result<PagedResponse<ActivityLogDto>?>>
{
    public GetRiskActivitiesQuery(IdOrKey idOrKey, int page = 1, int pageSize = 50)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Risk>();
        Page = page;
        PageSize = pageSize;
    }

    public Expression<Func<Risk, bool>> IdOrKeyFilter { get; }
    public int Page { get; }
    public int PageSize { get; }
}

public sealed class GetRiskActivitiesQueryHandler(
    IPlanningDbContext planningDbContext,
    IActivityLogReader activityLogReader)
    : IQueryHandler<GetRiskActivitiesQuery, Result<PagedResponse<ActivityLogDto>?>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public async Task<Result<PagedResponse<ActivityLogDto>?>> Handle(
        GetRiskActivitiesQuery request, CancellationToken cancellationToken)
    {
        var riskId = await _planningDbContext.Risks
            .Where(request.IdOrKeyFilter)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (riskId is null)
        {
            return Result.Success<PagedResponse<ActivityLogDto>?>(null);
        }

        var activities = await _activityLogReader.Read(
            riskId.Value,
            aggregateType: "Risk",
            page: request.Page,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
