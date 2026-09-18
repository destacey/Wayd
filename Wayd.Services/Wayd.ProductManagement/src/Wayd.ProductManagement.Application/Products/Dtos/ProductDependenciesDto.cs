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
/// Carries both ends whichever list it is in, so a rolled-up row can say which descendant it starts or lands
/// on: Trio's Depends on list shows Trio VMS as the product, and Argo Platform's Used by list shows Argo
/// Identity as the product depended on.
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
}
