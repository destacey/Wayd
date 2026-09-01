using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.ReleasePackages.Dtos;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.ReleasePackages.Queries;

/// <summary>
/// Release packages, newest first.
/// </summary>
public sealed record GetReleasePackagesQuery(
    IReadOnlyCollection<StatusCategory>? StatusCategories = null,
    Guid? ContainingProductId = null) : IQuery<IReadOnlyCollection<ReleasePackageDto>>;

public sealed class GetReleasePackagesQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetReleasePackagesQuery, IReadOnlyCollection<ReleasePackageDto>>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    public async Task<IReadOnlyCollection<ReleasePackageDto>> Handle(
        GetReleasePackagesQuery query, CancellationToken cancellationToken)
    {
        var packages = _productManagementDbContext.ReleasePackages.AsNoTracking();

        if (query.StatusCategories is { Count: > 0 })
        {
            packages = packages.Where(p => query.StatusCategories.Contains(p.StatusCategory));
        }

        if (query.ContainingProductId is not null)
        {
            packages = packages.Where(p => _productManagementDbContext.ReleasePackageComponents
                .Any(c => c.PackageId == p.Id && c.ProductId == query.ContainingProductId));
        }

        return await packages
            .ProjectToType<ReleasePackageDto>(
                ReleasePackageDto.CreateTypeAdapterConfig(_productManagementDbContext))
            .OrderByDescending(p => p.ReleasedDate == null)
            .ThenByDescending(p => p.ReleasedDate)
            .ToListAsync(cancellationToken);
    }
}
