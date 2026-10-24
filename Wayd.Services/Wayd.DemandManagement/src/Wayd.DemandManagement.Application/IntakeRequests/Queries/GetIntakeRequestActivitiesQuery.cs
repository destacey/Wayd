using System.Linq.Expressions;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests.Queries;

/// <summary>
/// An intake request's history, limited to the events of the tiers the current user may read, or null when
/// they may not see the request at all.
/// </summary>
/// <remarks>
/// Events carry their tier's fields — a details edit carries the description at both ends — so the history is
/// filtered by event type in the query, never redacted afterwards.
/// </remarks>
public sealed record GetIntakeRequestActivitiesQuery : IQuery<PagedResponse<ActivityLogDto>?>
{
    public GetIntakeRequestActivitiesQuery(IdOrKey idOrKey, int page = 1, int pageSize = 50)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<IntakeRequest>();
        Page = page;
        PageSize = pageSize;
    }

    public Expression<Func<IntakeRequest, bool>> IdOrKeyFilter { get; }
    public int Page { get; }
    public int PageSize { get; }
}

public sealed class GetIntakeRequestActivitiesQueryHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    IActivityLogReader activityLogReader)
    : IQueryHandler<GetIntakeRequestActivitiesQuery, PagedResponse<ActivityLogDto>?>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public async Task<PagedResponse<ActivityLogDto>?> Handle(GetIntakeRequestActivitiesQuery request, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);

        var visible = _dbContext.IntakeRequests
            .AsNoTracking()
            .Where(request.IdOrKeyFilter)
            .Where(reader.CanSeeSummary(_dbContext.IntakeDesks));

        var target = await visible
            .Select(r => new { r.Id, r.DeskId })
            .FirstOrDefaultAsync(cancellationToken);
        if (target is null)
            return null;

        List<IntakeRequestReadTier> tiers = [IntakeRequestReadTier.Summary];
        if (await visible.Where(reader.CanSeeSubstance(_dbContext.IntakeDesks)).AnyAsync(cancellationToken))
            tiers.Add(IntakeRequestReadTier.Substance);
        if (reader.CanReview(target.DeskId))
            tiers.Add(IntakeRequestReadTier.Internal);

        return await _activityLogReader.ReadEventTypes(
            target.Id,
            nameof(IntakeRequest),
            IntakeRequestEventTiers.EventTypesIn(tiers),
            request.Page,
            request.PageSize,
            cancellationToken);
    }
}
