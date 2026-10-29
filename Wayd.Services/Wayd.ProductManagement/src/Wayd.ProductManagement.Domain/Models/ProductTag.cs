using Wayd.Common.Domain.Tagging;

namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// One label on one product axis — <c>ios</c> on Platform, <c>pci-scope</c> on Compliance.
/// </summary>
/// <remarks>
/// Descriptive only. Nothing in the domain branches on a tag — behaviour comes from the product's type — so adding
/// one can never change what a node is allowed to do. Changed only through <see cref="ProductTagCategory"/>.
/// </remarks>
public sealed class ProductTag : TagBase
{
    private ProductTag() { }

    internal ProductTag(Guid categoryId, string name, string? description)
        : base(categoryId, name, description)
    {
    }
}
