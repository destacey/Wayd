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
/// <param name="ContainingVersionId">
/// Packages whose manifest names this exact version. Narrower than
/// <paramref name="ContainingProductId"/> and the right filter for a version's own page, where the
/// product-wide answer would list packages that version was never in.
/// </param>
public sealed record GetReleasePackagesQuery(
    IReadOnlyCollection<StatusCategory>? StatusCategories = null,
    Guid? ContainingProductId = null,
    Guid? ContainingVersionId = null) : IQuery<IReadOnlyCollection<ReleasePackageDto>>;

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

        // A manifest entry's VersionId is nullable — a carried-forward line often names a version that
        // was never cut here. Those entries are correctly not matches: the question is which packages
        // named *this* version record.
        if (query.ContainingVersionId is not null)
        {
            packages = packages.Where(p => _productManagementDbContext.ReleasePackageComponents
                .Any(c => c.PackageId == p.Id && c.VersionId == query.ContainingVersionId));
        }

        return await packages
            .ProjectToType<ReleasePackageDto>(
                ReleasePackageDto.CreateTypeAdapterConfig(_productManagementDbContext))
            .OrderByDescending(p => p.ReleasedDate == null)
            .ThenByDescending(p => p.ReleasedDate)
            .ToListAsync(cancellationToken);
    }
}
