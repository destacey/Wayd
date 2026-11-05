using Wayd.ProductManagement.Application.Products.Dtos;

namespace Wayd.ProductManagement.Application.Products.Queries;

/// <summary>
/// The products a team or team of teams is linked to.
/// </summary>
/// <remarks>
/// Empty, rather than not found, for a team with no links: Organization owns whether a team exists, and a team
/// whose copy has not arrived yet has no links either.
/// </remarks>
/// <param name="TeamId">The team's id.</param>
public sealed record GetTeamProductsQuery(Guid TeamId) : IQuery<IReadOnlyList<ProductTeamLinkDto>>;

public sealed class GetTeamProductsQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetTeamProductsQuery, IReadOnlyList<ProductTeamLinkDto>>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    public async Task<IReadOnlyList<ProductTeamLinkDto>> Handle(GetTeamProductsQuery query, CancellationToken cancellationToken)
    {
        var links = await ProductTeamLinkRow.Read(
            _productManagementDbContext.ProductTeamLinks.Where(l => l.TeamId == query.TeamId),
            cancellationToken);

        if (links.Count == 0)
        {
            return [];
        }

        var catalog = await ProductCatalog.Load(_productManagementDbContext, cancellationToken);

        // By product, the id last so the order is stable across pages.
        return [.. links
            .OrderBy(l => catalog.Name(l.ProductId), StringComparer.OrdinalIgnoreCase)
            .ThenBy(l => l.ProductId)
            .Select(l => l.ToDto(catalog))];
    }
}
