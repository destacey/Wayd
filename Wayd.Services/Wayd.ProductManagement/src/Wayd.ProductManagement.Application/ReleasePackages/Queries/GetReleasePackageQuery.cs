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
        return await _productManagementDbContext.ReleasePackages
            .Where(p => p.Id == query.Id)
            .ProjectToType<ReleasePackageDto>(
                ReleasePackageDto.CreateTypeAdapterConfig(_productManagementDbContext))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
