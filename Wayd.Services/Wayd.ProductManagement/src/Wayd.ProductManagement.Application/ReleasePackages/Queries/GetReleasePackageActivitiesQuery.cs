using System.Linq.Expressions;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.ReleasePackages.Queries;

/// <summary>
/// Retrieves activity history for a release package, newest first.
/// </summary>
public sealed record GetReleasePackageActivitiesQuery : IQuery<Result<PagedResponse<ActivityLogDto>?>>, IPagedQuery
{
    public GetReleasePackageActivitiesQuery(IdOrKey idOrKey, PageRequest paging)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<ReleasePackage>();
        Paging = paging;
    }

    public Expression<Func<ReleasePackage, bool>> IdOrKeyFilter { get; }
    public PageRequest Paging { get; }
}

/// <summary>Bounds the page a caller may ask for.</summary>
public sealed class GetReleasePackageActivitiesQueryValidator : PagedQueryValidator<GetReleasePackageActivitiesQuery>;

public sealed class GetReleasePackageActivitiesQueryHandler(
    IProductManagementDbContext productManagementDbContext,
    IActivityLogReader activityLogReader)
    : IQueryHandler<GetReleasePackageActivitiesQuery, Result<PagedResponse<ActivityLogDto>?>>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public async Task<Result<PagedResponse<ActivityLogDto>?>> Handle(
        GetReleasePackageActivitiesQuery request, CancellationToken cancellationToken)
    {
        var releasePackageId = await _productManagementDbContext.ReleasePackages
            .Where(request.IdOrKeyFilter)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (releasePackageId is null)
        {
            return Result.Success<PagedResponse<ActivityLogDto>?>(null);
        }

        var activities = await _activityLogReader.Read(
            releasePackageId.Value,
            aggregateType: "ReleasePackage",
            paging: request.Paging,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
