using Ardalis.GuardClauses;
using Wayd.Common.Domain.Tagging;

namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// One tag applied to one product.
/// </summary>
/// <remarks>
/// Carries the tag's category so that "what platform is this product?" is answerable without loading
/// the tag and its axis — the same denormalization reasoning as a record's status category.
/// </remarks>
public sealed class ProductTagAssignment : BaseAuditableEntity, ITagAssignment
{
    private ProductTagAssignment() { }

    internal ProductTagAssignment(Guid productId, Guid tagId, Guid? categoryId)
    {
        ProductId = Guard.Against.Default(productId, nameof(productId));
        TagId = Guard.Against.Default(tagId, nameof(tagId));
        if (categoryId == Guid.Empty)
            throw new ArgumentException("An axis id cannot be empty; a free tag has none.", nameof(categoryId));
        CategoryId = categoryId;
    }

    /// <summary>The product carrying the tag.</summary>
    public Guid ProductId { get; private init; }

    /// <summary>The tag it carries.</summary>
    public Guid TagId { get; private init; }

    /// <summary>The tag applied, when one is loaded.</summary>
    /// <remarks>For the read side only. No invariant depends on this being loaded.</remarks>
    public ProductTag? Tag { get; private init; }

    /// <summary>
    /// The tag's axis, denormalized so filtering by axis needs no join through the tag. Null for a free tag.
    /// </summary>
    public Guid? CategoryId { get; private init; }
}
