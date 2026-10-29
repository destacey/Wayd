namespace Wayd.ProductManagement.Application.ProductTags.Dtos;

/// <summary>
/// One product tag, on an axis or free, with how many products carry it.
/// </summary>
public sealed record ProductTagListItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string? Description { get; init; }

    /// <summary>The tag's axis, or null for a free tag.</summary>
    public Guid? CategoryId { get; init; }

    /// <summary>The axis's name, or null for a free tag.</summary>
    public string? CategoryName { get; init; }

    /// <summary>Whether this is a free tag: one on no axis, created by typing its name while tagging.</summary>
    public bool IsFree { get; init; }

    /// <summary>Whether products can still be given this tag. Always true for a free tag.</summary>
    public bool IsActive { get; init; }

    /// <summary>How many products carry the tag.</summary>
    public int ProductCount { get; init; }
}
