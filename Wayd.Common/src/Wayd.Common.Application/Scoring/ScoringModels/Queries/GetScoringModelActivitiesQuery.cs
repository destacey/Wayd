using System.Linq.Expressions;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Scoring;

namespace Wayd.Common.Application.Scoring.ScoringModels.Queries;

/// <summary>
/// Retrieves activity history for a scoring model, newest first.
/// </summary>
public sealed record GetScoringModelActivitiesQuery : IQuery<Result<PagedResponse<ActivityLogDto>?>>
{
    public GetScoringModelActivitiesQuery(IdOrKey idOrKey, int page = 1, int pageSize = 50)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<ScoringModel>();
        Page = page;
        PageSize = pageSize;
    }

    public Expression<Func<ScoringModel, bool>> IdOrKeyFilter { get; }
    public int Page { get; }
    public int PageSize { get; }
}

public sealed class GetScoringModelActivitiesQueryHandler(
    IWaydDbContext waydDbContext,
    IActivityLogReader activityLogReader)
    : IQueryHandler<GetScoringModelActivitiesQuery, Result<PagedResponse<ActivityLogDto>?>>
{
    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public async Task<Result<PagedResponse<ActivityLogDto>?>> Handle(
        GetScoringModelActivitiesQuery request, CancellationToken cancellationToken)
    {
        var scoringModelId = await _waydDbContext.ScoringModels
            .Where(request.IdOrKeyFilter)
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (scoringModelId is null)
        {
            return Result.Success<PagedResponse<ActivityLogDto>?>(null);
        }

        var activities = await _activityLogReader.Read(
            scoringModelId.Value,
            aggregateType: "ScoringModel",
            page: request.Page,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
