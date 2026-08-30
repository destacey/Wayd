namespace Wayd.ProductManagement.Application.ProductTagCategories.Dtos;

/// <summary>
/// A tag axis and the tags on it.
/// </summary>
public sealed record ProductTagCategoryDto
{
    public Guid Id { get; init; }
    public int Key { get; init; }
    public string Name { get; init; } = default!;
    public string? Description { get; init; }

    /// <summary>Whether a product can carry several tags from this axis.</summary>
    public bool AllowsMany { get; init; }

    /// <summary>Display position when presenting the axes. Presentation only.</summary>
    public int Order { get; init; }

    /// <summary>Whether products can still be tagged along this axis.</summary>
    public bool IsActive { get; init; }

    /// <summary>Platform-seeded axes are read-only, so an upgrade can reseed them.</summary>
    public bool IsSystem { get; init; }

    public IReadOnlyCollection<ProductTagOptionDto> Tags { get; init; } = [];
}

public sealed record ProductTagOptionDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public int Order { get; init; }
    public bool IsActive { get; init; }

    /// <summary>
    /// How many products currently carry this tag, so a caller can see what deactivating would affect.
    /// </summary>
    public int ProductCount { get; init; }
}
