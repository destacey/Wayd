using Ardalis.GuardClauses;

namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// One tag applied to one product.
/// </summary>
/// <remarks>
/// Carries the tag's category so that "what platform is this product?" is answerable without loading
/// the tag and its axis — the same denormalization reasoning as a record's status category.
/// </remarks>
public sealed class ProductTagAssignment : BaseAuditableEntity
{
    private ProductTagAssignment() { }

    internal ProductTagAssignment(Guid productId, Guid tagId, Guid categoryId)
    {
        ProductId = Guard.Against.Default(productId, nameof(productId));
        TagId = Guard.Against.Default(tagId, nameof(tagId));
        CategoryId = Guard.Against.Default(categoryId, nameof(categoryId));
    }

    /// <summary>The product carrying the tag.</summary>
    public Guid ProductId { get; private init; }

    /// <summary>The tag it carries.</summary>
    public Guid TagId { get; private init; }

    /// <summary>
    /// The tag's axis, denormalized so filtering by axis needs no join through the tag.
    /// </summary>
    public Guid CategoryId { get; private init; }
}
