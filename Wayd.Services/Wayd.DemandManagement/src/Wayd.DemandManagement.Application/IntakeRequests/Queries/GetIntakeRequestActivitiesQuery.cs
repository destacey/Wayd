using System.Linq.Expressions;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests.Queries;

/// <summary>
/// An intake request's history, limited to the events of the tiers the current user may read, or null when
/// they may not see the request at all.
/// </summary>
/// <remarks>
/// Read through <see cref="IntakeRequestActivity"/>, which says what each tier hides.
/// </remarks>
public sealed record GetIntakeRequestActivitiesQuery : IQuery<PagedResponse<ActivityLogDto>?>, IPagedQuery
{
    public GetIntakeRequestActivitiesQuery(IdOrKey idOrKey, PageRequest paging)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<IntakeRequest>();
        Paging = paging;
    }

    public Expression<Func<IntakeRequest, bool>> IdOrKeyFilter { get; }
    public PageRequest Paging { get; }
}

/// <summary>Bounds the page a caller may ask for.</summary>
public sealed class GetIntakeRequestActivitiesQueryValidator : PagedQueryValidator<GetIntakeRequestActivitiesQuery>;

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

        return await new IntakeRequestActivity(_dbContext, _activityLogReader)
            .Read(visible, reader, request.Paging, cancellationToken);
    }
}
