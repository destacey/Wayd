using System.Linq.Expressions;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.Products.Dtos;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Products.Queries;

/// <summary>
/// What a product depends on and what depends on it, rolled up across its subtree, or <c>null</c> when the
/// product does not exist.
/// </summary>
/// <param name="IncludeEnded">
/// Ended links are left out by default. They still count for the period they held, but no longer hold.
/// </param>
public sealed record GetProductDependenciesQuery : IQuery<ProductDependenciesDto?>
{
    public GetProductDependenciesQuery(IdOrKey idOrKey, bool includeEnded = false)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Product>();
        IncludeEnded = includeEnded;
    }

    public Expression<Func<Product, bool>> IdOrKeyFilter { get; }
    public bool IncludeEnded { get; }
}

public sealed class GetProductDependenciesQueryHandler(IProductManagementDbContext productManagementDbContext)
    : IQueryHandler<GetProductDependenciesQuery, ProductDependenciesDto?>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;

    private sealed record CatalogNode(Guid Id, int Key, string Name, Guid? ParentId);

    private sealed record Link(
        Guid Id, Guid ProductId, Guid DependsOnProductId, DependencyStrength Strength,
        string? Description, LocalDate StartsOn, LocalDate? EndsOn);

    public async Task<ProductDependenciesDto?> Handle(GetProductDependenciesQuery query, CancellationToken cancellationToken)
    {
        var productId = await _productManagementDbContext.Products
            .Where(query.IdOrKeyFilter)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (productId is null)
        {
            return null;
        }

        // The catalog is curated reference data — tens of rows, not thousands — so the subtree is resolved by
        // walking it here. A recursive CTE would be the alternative, and LINQ does not express one.
        var catalog = await _productManagementDbContext.Products
            .Select(p => new CatalogNode(p.Id, p.Key, p.Name, p.ParentId))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var subtree = Subtree(productId.Value, catalog.Values);

        var links = _productManagementDbContext.ProductDependencies.AsQueryable();
        if (!query.IncludeEnded)
        {
            links = links.Where(d => d.Period.End == null);
        }

        // Narrowed to links touching the subtree in the database, then split here: a link inside the subtree
        // matches on both ends and belongs in neither list.
        var touching = await links
            .Where(d => subtree.Contains(d.ProductId) || subtree.Contains(d.DependsOnProductId))
            .Select(d => new Link(d.Id, d.ProductId, d.DependsOnProductId, d.Strength, d.Description, d.Period.Start, d.Period.End))
            .ToListAsync(cancellationToken);

        return new ProductDependenciesDto
        {
            DependsOn = Rows(
                touching.Where(l => subtree.Contains(l.ProductId) && !subtree.Contains(l.DependsOnProductId)),
                catalog,
                l => l.DependsOnProductId),
            UsedBy = Rows(
                touching.Where(l => subtree.Contains(l.DependsOnProductId) && !subtree.Contains(l.ProductId)),
                catalog,
                l => l.ProductId),
        };
    }

    /// <summary>
    /// Open links first, then by the product at the far end, then newest first.
    /// </summary>
    private static List<ProductDependencyDto> Rows(IEnumerable<Link> links, Dictionary<Guid, CatalogNode> catalog, Func<Link, Guid> farEnd) =>
        [.. links
            .OrderBy(l => l.EndsOn is not null)
            .ThenBy(l => catalog[farEnd(l)].Name, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(l => l.StartsOn)
            .Select(l => new ProductDependencyDto
            {
                Id = l.Id,
                Product = Navigation(catalog[l.ProductId]),
                DependsOnProduct = Navigation(catalog[l.DependsOnProductId]),
                Strength = l.Strength,
                Description = l.Description,
                StartsOn = l.StartsOn,
                EndsOn = l.EndsOn,
            })];

    private static NavigationDto Navigation(CatalogNode node) => NavigationDto.Create(node.Id, node.Key, node.Name);

    /// <summary>A node and everything beneath it.</summary>
    /// <remarks>
    /// Tracks what it has seen so data that already contains a cycle cannot loop forever. The domain
    /// refuses to create one, so this guards against a catalog that is already wrong.
    /// </remarks>
    private static HashSet<Guid> Subtree(Guid root, IEnumerable<CatalogNode> catalog)
    {
        var childrenByParent = catalog
            .Where(p => p.ParentId is not null)
            .ToLookup(p => p.ParentId!.Value, p => p.Id);

        var subtree = new HashSet<Guid> { root };
        var pending = new Queue<Guid>([root]);

        while (pending.TryDequeue(out var id))
        {
            foreach (var child in childrenByParent[id].Where(subtree.Add))
            {
                pending.Enqueue(child);
            }
        }

        return subtree;
    }
}
