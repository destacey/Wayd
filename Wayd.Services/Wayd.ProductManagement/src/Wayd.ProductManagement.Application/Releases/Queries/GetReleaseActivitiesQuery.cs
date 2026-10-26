using System.Linq.Expressions;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Releases.Queries;

/// <summary>
/// Retrieves activity history for a release, newest first.
/// </summary>
public sealed record GetReleaseActivitiesQuery : IQuery<Result<PagedResponse<ActivityLogDto>?>>, IPagedQuery
{
    public GetReleaseActivitiesQuery(IdOrKey idOrKey, PageRequest paging)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Release>();
        Paging = paging;
    }

    public Expression<Func<Release, bool>> IdOrKeyFilter { get; }
    public PageRequest Paging { get; }
}

/// <summary>Bounds the page a caller may ask for.</summary>
public sealed class GetReleaseActivitiesQueryValidator : PagedQueryValidator<GetReleaseActivitiesQuery>;

public sealed class GetReleaseActivitiesQueryHandler(
    IProductManagementDbContext productManagementDbContext,
    IActivityLogReader activityLogReader)
    : IQueryHandler<GetReleaseActivitiesQuery, Result<PagedResponse<ActivityLogDto>?>>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public async Task<Result<PagedResponse<ActivityLogDto>?>> Handle(
        GetReleaseActivitiesQuery request, CancellationToken cancellationToken)
    {
        var releaseId = await _productManagementDbContext.Releases
            .Where(request.IdOrKeyFilter)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (releaseId is null)
        {
            return Result.Success<PagedResponse<ActivityLogDto>?>(null);
        }

        var activities = await _activityLogReader.Read(
            releaseId.Value,
            aggregateType: "Release",
            paging: request.Paging,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
