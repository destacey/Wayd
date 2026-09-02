using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Versions.Dtos;

namespace Wayd.ProductManagement.Application.Versions.Queries;

/// <summary>
/// Versions, newest first.
/// </summary>
/// <remarks>
/// Ordered by released date then sequence, with undated (planned) versions first — never by version,
/// which is free text.
/// </remarks>
public sealed record GetVersionsQuery(
    Guid? ProductId = null,
    IReadOnlyCollection<StatusCategory>? StatusCategories = null) : IQuery<IReadOnlyCollection<VersionDto>>;

public sealed class GetVersionsQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetVersionsQuery, IReadOnlyCollection<VersionDto>>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    public async Task<IReadOnlyCollection<VersionDto>> Handle(
        GetVersionsQuery query, CancellationToken cancellationToken)
    {
        var versions = _productManagementDbContext.Versions.AsQueryable();

        if (query.ProductId is not null)
        {
            versions = versions.Where(r => r.ProductId == query.ProductId);
        }

        if (query.StatusCategories is { Count: > 0 })
        {
            versions = versions.Where(r => query.StatusCategories.Contains(r.StatusCategory));
        }

        var ordered = versions
            .ProjectToType<VersionDto>()
            .OrderByDescending(r => r.ReleasedDate == null)
            .ThenByDescending(r => r.ReleasedDate);

        // Sequence orders one product's versions against each other and means nothing across
        // products — 4.8.2 of one has no position relative to 2026.04 of another beyond the date they
        // shipped. Applying it to a mixed list would let an ordering set for one product move a
        // second product's version that happens to share a released date.
        return await (query.ProductId is not null
                ? ordered.ThenByDescending(r => r.Sequence)
                : ordered)
            .ToListAsync(cancellationToken);
    }

}
