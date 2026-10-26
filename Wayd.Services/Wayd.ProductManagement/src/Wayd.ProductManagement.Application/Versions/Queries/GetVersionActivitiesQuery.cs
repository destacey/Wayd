using System.Linq.Expressions;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Application.Versions.Queries;

/// <summary>
/// Retrieves activity history for a version, newest first.
/// </summary>
public sealed record GetVersionActivitiesQuery : IQuery<Result<PagedResponse<ActivityLogDto>?>>, IPagedQuery
{
    public GetVersionActivitiesQuery(IdOrKey idOrKey, PageRequest paging)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Version>();
        Paging = paging;
    }

    public Expression<Func<Version, bool>> IdOrKeyFilter { get; }
    public PageRequest Paging { get; }
}

/// <summary>Bounds the page a caller may ask for.</summary>
public sealed class GetVersionActivitiesQueryValidator : PagedQueryValidator<GetVersionActivitiesQuery>;

public sealed class GetVersionActivitiesQueryHandler(
    IProductManagementDbContext productManagementDbContext,
    IActivityLogReader activityLogReader)
    : IQueryHandler<GetVersionActivitiesQuery, Result<PagedResponse<ActivityLogDto>?>>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IActivityLogReader _activityLogReader = activityLogReader;

    public async Task<Result<PagedResponse<ActivityLogDto>?>> Handle(
        GetVersionActivitiesQuery request, CancellationToken cancellationToken)
    {
        var versionId = await _productManagementDbContext.Versions
            .Where(request.IdOrKeyFilter)
            .Select(v => (Guid?)v.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (versionId is null)
        {
            return Result.Success<PagedResponse<ActivityLogDto>?>(null);
        }

        var activities = await _activityLogReader.Read(
            versionId.Value,
            aggregateType: "Version",
            paging: request.Paging,
            cancellationToken: cancellationToken);

        return Result.Success<PagedResponse<ActivityLogDto>?>(activities);
    }
}
