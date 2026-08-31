using Wayd.ProductManagement.Application.Releases.Dtos;

namespace Wayd.ProductManagement.Application.Releases.Queries;

/// <summary>
/// A single release by id, or <c>null</c> when it does not exist.
/// </summary>
public sealed record GetReleaseQuery(Guid Id) : IQuery<ReleaseDto?>;

public sealed class GetReleaseQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetReleaseQuery, ReleaseDto?>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    public async Task<ReleaseDto?> Handle(GetReleaseQuery query, CancellationToken cancellationToken)
    {
        var releases = _productManagementDbContext.Releases
            .AsNoTracking()
            .Where(r => r.Id == query.Id);

        return await GetReleasesQueryHandler
            .Project(releases, _productManagementDbContext)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
