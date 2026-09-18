using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums.ProductManagement;

namespace Wayd.ProductManagement.Application.Products.Dtos;

/// <summary>
/// A product's dependencies in both directions, rolled up across everything beneath it.
/// </summary>
/// <remarks>
/// Computed when read, never stored, so it cannot go stale when a product moves. A link with both ends
/// inside the product's own subtree appears in neither list: from outside, it is the product depending on
/// itself.
/// </remarks>
public sealed record ProductDependenciesDto
{
    /// <summary>
    /// Links from the product, or anything beneath it, to products outside it.
    /// </summary>
    public IReadOnlyCollection<ProductDependencyDto> DependsOn { get; init; } = [];

    /// <summary>
    /// Links from products outside it to the product, or anything beneath it.
    /// </summary>
    public IReadOnlyCollection<ProductDependencyDto> UsedBy { get; init; } = [];
}

/// <summary>
/// One product depending on another over a period.
/// </summary>
/// <remarks>
/// Carries both ends whichever list it is in, so a rolled-up row can say which descendant it starts or
/// lands on: Storefront's Depends On list shows Storefront Web as the product, and Core Platform's Used By
/// list shows Identity Service as the product depended on.
/// </remarks>
public sealed record ProductDependencyDto
{
    public Guid Id { get; init; }

    /// <summary>The product that has the dependency.</summary>
    public NavigationDto Product { get; init; } = default!;

    /// <summary>The product depended on.</summary>
    public NavigationDto DependsOnProduct { get; init; } = default!;

    public DependencyStrength Strength { get; init; }

    public string? Description { get; init; }

    /// <summary>The day the dependency began.</summary>
    public LocalDate StartsOn { get; init; }

    /// <summary>The last day the dependency held, or <c>null</c> while it still holds.</summary>
    public LocalDate? EndsOn { get; init; }

    /// <summary>Everything <see cref="Product"/> sits inside, from its root down to its parent.</summary>
    /// <inheritdoc cref="DependsOnProductPath" path="/remarks"/>
    public IReadOnlyList<NavigationDto> ProductPath { get; init; } = [];

    /// <summary>Everything <see cref="DependsOnProduct"/> sits inside, from its root down to its parent.</summary>
    /// <remarks>
    /// Where each end sits in the catalog, which the ends alone cannot say: a row carrying "Accounts API"
    /// does not tell a reader whether that is a child of the product being read or a service of some other
    /// product line. Full chains rather than chains relative to the product being read, so the same row
    /// serves a reader drawing the near side inside the product and the far side under whatever owns it.
    /// </remarks>
    public IReadOnlyList<NavigationDto> DependsOnProductPath { get; init; } = [];
}
