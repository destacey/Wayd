using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.ProductManagement.Application.Products.Dtos;

namespace Wayd.ProductManagement.Application.Products.Queries;

/// <summary>
/// The teams linked to a product or anything beneath it, or <c>null</c> when the product does not exist.
/// </summary>
public sealed record GetProductTeamsQuery : IQuery<IReadOnlyList<ProductTeamLinkDto>?>
{
    /// <param name="idOrKey">The product's id or key.</param>
    public GetProductTeamsQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Product>();
    }

    public Expression<Func<Product, bool>> IdOrKeyFilter { get; }
}

public sealed class GetProductTeamsQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetProductTeamsQuery, IReadOnlyList<ProductTeamLinkDto>?>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    public async Task<IReadOnlyList<ProductTeamLinkDto>?> Handle(GetProductTeamsQuery query, CancellationToken cancellationToken)
    {
        var productId = await _productManagementDbContext.Products
            .Where(query.IdOrKeyFilter)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (productId is null)
        {
            return null;
        }

        var catalog = await ProductCatalog.Load(_productManagementDbContext, cancellationToken);
        var subtree = catalog.Subtree(productId.Value);

        var links = await ProductTeamLinkRow.Read(
            _productManagementDbContext.ProductTeamLinks.Where(l => subtree.Contains(l.ProductId)),
            cancellationToken);

        // By team, then the product's own link before those beneath it, the ids last so the order is stable across pages.
        return [.. links
            .OrderBy(l => l.TeamName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(l => l.ProductId != productId.Value)
            .ThenBy(l => catalog.Name(l.ProductId), StringComparer.OrdinalIgnoreCase)
            .ThenBy(l => l.TeamId)
            .ThenBy(l => l.ProductId)
            .Select(l => l.ToDto(catalog))];
    }
}

/// <summary>
/// A team link as the database holds it, with the team it names, before the catalog places its product.
/// </summary>
internal sealed record ProductTeamLinkRow(
    Guid ProductId, Guid TeamId, int TeamKey, string TeamName, string TeamCode, TeamType TeamType, bool TeamIsActive)
{
    public static Task<List<ProductTeamLinkRow>> Read(IQueryable<ProductTeamLink> links, CancellationToken cancellationToken) =>
        links
            .Select(l => new ProductTeamLinkRow(
                l.ProductId, l.TeamId, l.Team!.Key, l.Team.Name, l.Team.Code.Value, l.Team.Type, l.Team.IsActive))
            .ToListAsync(cancellationToken);

    public ProductTeamLinkDto ToDto(ProductCatalog catalog) => new()
    {
        Product = catalog.Navigation(ProductId),
        ProductPath = catalog.Ancestry(ProductId),
        Team = new ProductTeamNavigationDto
        {
            Id = TeamId,
            Key = TeamKey,
            Name = TeamName,
            Code = TeamCode,
            Type = TeamType,
            IsActive = TeamIsActive,
        },
    };
}
