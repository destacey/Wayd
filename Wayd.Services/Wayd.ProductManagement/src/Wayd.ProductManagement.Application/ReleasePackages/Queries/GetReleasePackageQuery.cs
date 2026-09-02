using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.ProductManagement.Application.ReleasePackages.Dtos;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.ReleasePackages.Queries;

/// <summary>
/// A single package by id or key, or <c>null</c> when it does not exist.
/// </summary>
/// <remarks>
/// Accepts either so a URL can carry the short integer key a reader can recognise, rather than a
/// GUID, matching how the other modules address a record.
/// </remarks>
public sealed record GetReleasePackageQuery : IQuery<ReleasePackageDto?>
{
    public GetReleasePackageQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<ReleasePackage>();
    }

    public Expression<Func<ReleasePackage, bool>> IdOrKeyFilter { get; }
}

public sealed class GetReleasePackageQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetReleasePackageQuery, ReleasePackageDto?>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    public async Task<ReleasePackageDto?> Handle(GetReleasePackageQuery query, CancellationToken cancellationToken)
    {
        return await _productManagementDbContext.ReleasePackages
            .Where(query.IdOrKeyFilter)
            .ProjectToType<ReleasePackageDto>(
                ReleasePackageDto.CreateTypeAdapterConfig(_productManagementDbContext))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
