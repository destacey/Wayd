namespace Wayd.ProductManagement.Application.ProductTypes.Dtos;

/// <summary>
/// A product type as a catalog row.
/// </summary>
public sealed record ProductTypeDto
{
    public Guid Id { get; init; }
    public int Key { get; init; }
    public string Name { get; init; } = default!;
    public string? Description { get; init; }

    /// <summary>Whether releases can be cut against nodes of this type.</summary>
    public bool IsReleasable { get; init; }

    /// <summary>Display position when presenting the catalog. Presentation only.</summary>
    public int Order { get; init; }

    /// <summary>Whether new products can still be created with this type.</summary>
    public bool IsActive { get; init; }

    /// <summary>Platform-seeded types are read-only: deactivate rather than delete or edit.</summary>
    public bool IsSystem { get; init; }

    /// <summary>
    /// How many products currently carry this type. Lets a caller see what deactivating would affect.
    /// </summary>
    public int ProductCount { get; init; }
}
