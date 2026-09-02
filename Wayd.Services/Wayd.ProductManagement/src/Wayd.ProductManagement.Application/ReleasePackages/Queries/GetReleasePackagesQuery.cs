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
/// <param name="ContainingProductId">
/// Packages whose manifest names this product, in any version. Answers "what has this component
/// shipped in?" across its whole history.
/// </param>
/// <param name="ContainingReleaseId">
/// Packages whose manifest names this exact release. Narrower than
/// <paramref name="ContainingProductId"/> and the right filter for a release's own page, where the
/// product-wide answer would list packages that release was never in.
/// </param>
public sealed record GetReleasePackagesQuery(
    IReadOnlyCollection<StatusCategory>? StatusCategories = null,
    Guid? ContainingProductId = null,
    Guid? ContainingReleaseId = null) : IQuery<IReadOnlyCollection<ReleasePackageDto>>;

public sealed class GetReleasePackagesQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetReleasePackagesQuery, IReadOnlyCollection<ReleasePackageDto>>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    public async Task<IReadOnlyCollection<ReleasePackageDto>> Handle(
        GetReleasePackagesQuery query, CancellationToken cancellationToken)
    {
        var packages = _productManagementDbContext.ReleasePackages.AsQueryable();

        if (query.StatusCategories is { Count: > 0 })
        {
            packages = packages.Where(p => query.StatusCategories.Contains(p.StatusCategory));
        }

        if (query.ContainingProductId is not null)
        {
            packages = packages.Where(p => _productManagementDbContext.ReleasePackageComponents
                .Any(c => c.PackageId == p.Id && c.ProductId == query.ContainingProductId));
        }

        // A manifest entry's ReleaseId is nullable — a carried-forward line often names a version that
        // was never cut as a release here. Those entries are correctly not matches: the question is
        // which packages named *this* release.
        if (query.ContainingReleaseId is not null)
        {
            packages = packages.Where(p => _productManagementDbContext.ReleasePackageComponents
                .Any(c => c.PackageId == p.Id && c.ReleaseId == query.ContainingReleaseId));
        }

        return await packages
            .ProjectToType<ReleasePackageDto>(
                ReleasePackageDto.CreateTypeAdapterConfig(_productManagementDbContext))
            .OrderByDescending(p => p.ReleasedDate == null)
            .ThenByDescending(p => p.ReleasedDate)
            .ToListAsync(cancellationToken);
    }
}
