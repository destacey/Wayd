using Ardalis.GuardClauses;

namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// One label on one axis — <c>ios</c> on Platform, <c>pci-scope</c> on Compliance.
/// </summary>
/// <remarks>
/// A row rather than a string on the product, so the list can populate a picker, a rename happens once
/// rather than across every product, and "everything tagged ios" is a join rather than a scan.
/// <para>
/// Descriptive only. Nothing in the domain branches on a tag — behaviour comes from the product's type
/// — so adding one can never change what a node is allowed to do.
/// </para>
/// <para>Changed only through <see cref="ProductTagCategory"/>.</para>
/// </remarks>
public sealed class ProductTag : BaseAuditableEntity
{
    private ProductTag() { }

    internal ProductTag(Guid categoryId, string name, string? description)
    {
        CategoryId = categoryId;
        Name = name;
        Description = description;
    }

    /// <summary>The axis this tag belongs to.</summary>
    public Guid CategoryId { get; private init; }

    /// <summary>The label. Unique within its axis.</summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>What the label means, where it is not obvious.</summary>
    public string? Description
    {
        get;
        private set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Whether products can still be tagged with this. Products already carrying it keep it.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    internal void UpdateDetails(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    internal void SetActive(bool isActive) => IsActive = isActive;
}
