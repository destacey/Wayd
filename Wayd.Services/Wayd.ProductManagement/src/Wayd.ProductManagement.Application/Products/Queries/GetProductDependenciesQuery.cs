using System.Linq.Expressions;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Extensions;
using Wayd.ProductManagement.Application.Products.Dtos;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Products.Queries;

/// <summary>
/// What a product depends on and what depends on it, rolled up across its subtree, or <c>null</c> when the
/// product does not exist.
/// </summary>
public sealed record GetProductDependenciesQuery : IQuery<ProductDependenciesDto?>
{
    /// <param name="idOrKey">The product's id or key.</param>
    /// <param name="includeEnded">
    /// Ended links are left out by default. They still count for the period they held, but no longer hold.
    /// </param>
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

    /// <summary>
    /// A link as the database holds it, styles still as stored flags — the split into a collection happens
    /// in <see cref="Rows"/>, which runs in memory.
    /// </summary>
    private sealed record Link(
        Guid Id, Guid ProductId, Guid DependsOnProductId, DependencyStrength Strength,
        InteractionStyle? InteractionStyle, string? Description, LocalDate StartsOn, LocalDate? EndsOn);

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

        var catalog = await ProductCatalog.Load(_productManagementDbContext, cancellationToken);
        var subtree = catalog.Subtree(productId.Value);

        var links = _productManagementDbContext.ProductDependencies.AsQueryable();
        if (!query.IncludeEnded)
        {
            links = links.Where(d => d.Period.End == null);
        }

        // Narrowed to links touching the subtree in the database, then split here: a link inside the subtree
        // matches on both ends and belongs in neither list.
        var touching = await links
            .Where(d => subtree.Contains(d.ProductId) || subtree.Contains(d.DependsOnProductId))
            .Select(d => new Link(d.Id, d.ProductId, d.DependsOnProductId, d.Strength, d.InteractionStyle, d.Description, d.Period.Start, d.Period.End))
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
    private static List<ProductDependencyDto> Rows(
        IEnumerable<Link> links,
        ProductCatalog catalog,
        Func<Link, Guid> farEnd) =>
        [.. links
            .OrderBy(l => l.EndsOn is not null)
            .ThenBy(l => catalog.Name(farEnd(l)), StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(l => l.StartsOn)
            .Select(l => new ProductDependencyDto
            {
                Id = l.Id,
                Product = catalog.Navigation(l.ProductId),
                DependsOnProduct = catalog.Navigation(l.DependsOnProductId),
                Strength = l.Strength,
                InteractionStyles = l.InteractionStyle.ToFlags(),
                Description = l.Description,
                StartsOn = l.StartsOn,
                EndsOn = l.EndsOn,
                ProductPath = catalog.Ancestry(l.ProductId),
                DependsOnProductPath = catalog.Ancestry(l.DependsOnProductId),
            })];
}
