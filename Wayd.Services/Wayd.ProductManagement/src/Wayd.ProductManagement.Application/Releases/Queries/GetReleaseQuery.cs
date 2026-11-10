using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.ProductManagement.Application.Incidents;
using Wayd.ProductManagement.Application.Releases.Dtos;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Releases.Queries;

/// <summary>
/// A single release by id or key, or <c>null</c> when it does not exist.
/// </summary>
/// <remarks>
/// Accepts either so a URL can carry the short integer key a reader can recognise, rather than a
/// GUID, matching how the other modules address a record.
/// </remarks>
public sealed record GetReleaseQuery : IQuery<ReleaseDto?>
{
    public GetReleaseQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Release>();
    }

    public Expression<Func<Release, bool>> IdOrKeyFilter { get; }
}

public sealed class GetReleaseQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetReleaseQuery, ReleaseDto?>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    public async Task<ReleaseDto?> Handle(GetReleaseQuery query, CancellationToken cancellationToken)
    {
        var release = await _productManagementDbContext.Releases
            .Where(query.IdOrKeyFilter)
            .ProjectToType<ReleaseDto>(ReleaseDto.CreateTypeAdapterConfig(_productManagementDbContext))
            .FirstOrDefaultAsync(cancellationToken);

        if (release is null)
        {
            return null;
        }

        var severity = await IncidentActivity.ForReleases(
            _productManagementDbContext,
            [release.Id],
            cancellationToken);

        return severity.TryGetValue(release.Id, out var top)
            ? release with
            {
                ActiveIncidentSeverityName = top.Name,
                ActiveIncidentSeverityColor = top.Color,
                ActiveIncidents = top.ToLinkDtos(),
            }
            : release;
    }
}
