using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;

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
/// </remarks>
public sealed class ProductTag : BaseAuditableEntity
{
    private ProductTag() { }

    internal ProductTag(Guid categoryId, string name, string? description, int order)
    {
        CategoryId = categoryId;
        Name = name;
        Description = description;
        Order = order;
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

    /// <summary>Display position within its axis. Presentation only.</summary>
    public int Order { get; private set; }

    /// <summary>
    /// Whether products can still be tagged with this. Products already carrying it keep it.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Renames the tag. Safe: products reference it by id, so the new name shows everywhere at once —
    /// which is the point of a curated list over free text.
    /// </summary>
    internal void Rename(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    /// <summary>Takes the tag out of use without removing it from what already carries it.</summary>
    public Result Deactivate()
    {
        if (!IsActive)
        {
            return Result.Failure("This tag is already inactive.");
        }

        IsActive = false;

        return Result.Success();
    }

    /// <summary>Puts the tag back into use.</summary>
    public Result Activate()
    {
        if (IsActive)
        {
            return Result.Failure("This tag is already active.");
        }

        IsActive = true;

        return Result.Success();
    }
}
