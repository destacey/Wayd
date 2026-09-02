using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Releases.Dtos;

namespace Wayd.ProductManagement.Application.Releases.Queries;

/// <summary>
/// Releases, newest first.
/// </summary>
/// <remarks>
/// Ordered by released date then sequence, with unannounced releases first — never by the version
/// label, which is free text.
/// </remarks>
/// <param name="ProductId">
/// Narrows to releases announced under one product node. A release spanning product lines has no
/// owner and is deliberately excluded by this filter — it belongs to no single product, so listing it
/// under one would misstate what that product announced.
/// </param>
/// <param name="ContainingVersionId">
/// Releases that announce this exact version, whether directly or through one of their packages.
/// This is how a version's own page answers "what did this ship in?".
/// </param>
public sealed record GetReleasesQuery(
    Guid? ProductId = null,
    IReadOnlyCollection<StatusCategory>? StatusCategories = null,
    Guid? ContainingVersionId = null) : IQuery<IReadOnlyCollection<ReleaseDto>>;

public sealed class GetReleasesQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetReleasesQuery, IReadOnlyCollection<ReleaseDto>>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    public async Task<IReadOnlyCollection<ReleaseDto>> Handle(
        GetReleasesQuery query, CancellationToken cancellationToken)
    {
        var releases = _productManagementDbContext.Releases.AsQueryable();

        if (query.ProductId is not null)
        {
            releases = releases.Where(r => r.ProductId == query.ProductId);
        }

        if (query.StatusCategories is { Count: > 0 })
        {
            releases = releases.Where(r => query.StatusCategories.Contains(r.StatusCategory));
        }

        if (query.ContainingVersionId is not null)
        {
            // Both routes, because a release announces a version either way and a reader asking "what
            // did 4.12.0 ship in?" does not care which. Checking only the direct join would silently
            // miss every version that shipped inside a package, which is the common case.
            releases = releases.Where(r =>
                _productManagementDbContext.ReleaseVersions
                    .Any(rv => rv.ReleaseId == r.Id && rv.VersionId == query.ContainingVersionId)
                || _productManagementDbContext.ReleasePackageInclusions
                    .Any(rp => rp.ReleaseId == r.Id
                        && _productManagementDbContext.ReleasePackageComponents
                            .Any(c => c.PackageId == rp.PackageId && c.VersionId == query.ContainingVersionId)));
        }

        var ordered = releases
            .ProjectToType<ReleaseDto>(ReleaseDto.CreateTypeAdapterConfig(_productManagementDbContext))
            .OrderByDescending(r => r.ReleasedDate == null)
            .ThenByDescending(r => r.ReleasedDate);

        // Sequence orders one product's releases against each other and means nothing across
        // products — 2026.07 of one has no position relative to R4 of another beyond the date they
        // were announced. Applying it to a mixed list would let an ordering set for one product move
        // a second product's release that happens to share a released date.
        return await (query.ProductId is not null
                ? ordered.ThenByDescending(r => r.Sequence)
                : ordered)
            .ToListAsync(cancellationToken);
    }
}
