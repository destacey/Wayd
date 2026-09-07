using System.Linq.Expressions;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.ReleasePackages.Queries;

/// <summary>
/// Retrieves activity history for a release package, newest first.
/// </summary>
public sealed record GetReleasePackageActivitiesQuery : IQuery<Result<PagedResponse<ActivityLogDto>?>>
{
    public GetReleasePackageActivitiesQuery(IdOrKey idOrKey, int page = 1, int pageSize = 50)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<ReleasePackage>();
        Page = page;
        PageSize = pageSize;
    }

    public Expression<Func<ReleasePackage, bool>> IdOrKeyFilter { get; }
    public int Page { get; }
    public int PageSize { get; }
}

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
            page: request.Page,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
