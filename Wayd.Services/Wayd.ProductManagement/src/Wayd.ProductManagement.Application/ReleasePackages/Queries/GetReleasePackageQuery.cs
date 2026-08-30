using Wayd.ProductManagement.Application.ReleasePackages.Dtos;

namespace Wayd.ProductManagement.Application.ReleasePackages.Queries;

/// <summary>
/// A single package by id, or <c>null</c> when it does not exist.
/// </summary>
public sealed record GetReleasePackageQuery(Guid Id) : IQuery<ReleasePackageDto?>;

public sealed class GetReleasePackageQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetReleasePackageQuery, ReleasePackageDto?>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    public async Task<ReleasePackageDto?> Handle(GetReleasePackageQuery query, CancellationToken cancellationToken)
    {
        var packages = _productManagementDbContext.ReleasePackages
            .AsNoTracking()
            .Where(p => p.Id == query.Id);

        return await GetReleasePackagesQueryHandler
            .Project(packages, _productManagementDbContext)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
