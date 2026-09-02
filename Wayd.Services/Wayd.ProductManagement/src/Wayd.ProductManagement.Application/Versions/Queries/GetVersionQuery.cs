using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.ProductManagement.Application.Versions.Dtos;
using Wayd.ProductManagement.Domain.Models;

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Application.Versions.Queries;

/// <summary>
/// A single version by id or key, or <c>null</c> when it does not exist.
/// </summary>
/// <remarks>
/// Accepts either so a URL can carry the short integer key a reader can recognise, rather than a
/// GUID, matching how the other modules address a record.
/// </remarks>
public sealed record GetVersionQuery : IQuery<VersionDto?>
{
    public GetVersionQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Version>();
    }

    public Expression<Func<Version, bool>> IdOrKeyFilter { get; }
}

public sealed class GetVersionQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetVersionQuery, VersionDto?>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    public async Task<VersionDto?> Handle(GetVersionQuery query, CancellationToken cancellationToken)
    {
        return await _productManagementDbContext.Versions
            .Where(query.IdOrKeyFilter)
            .ProjectToType<VersionDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
