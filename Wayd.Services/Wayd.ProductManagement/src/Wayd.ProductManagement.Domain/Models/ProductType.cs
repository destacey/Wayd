using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;

namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// A node type an organization recognises — Product Line, Product, Service, Application, Tool, Module,
/// Interface. Every <see cref="Product"/> carries one.
/// </summary>
/// <remarks>
/// A table rather than an enum from phase one so that phase two — capability flags, distribution
/// model, sourcing, allowed-parent rules — stays additive, with no backfill of
/// <c>Product.ProductTypeId</c> and no re-import of hand-entered data.
/// </remarks>
public sealed class ProductType : BaseAuditableEntity, IHasIdAndKey
{
    private ProductType() { }

    private ProductType(string name, string? description, bool isReleasable, int order, bool isSystem)
    {
        Name = name;
        Description = description;
        IsReleasable = isReleasable;
        Order = order;
        IsSystem = isSystem;
    }

    /// <summary>
    /// Whether new products can be created with this type.
    /// </summary>
    /// <remarks>
    /// Deactivated rather than deleted, because products already using it still resolve their type.
    /// Reversible on purpose: an organization that stops shipping tools this year may start again, and
    /// making it recreate the type would lose the continuity for no reason.
    /// </remarks>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// The unique auto-generated key of the product type. This is an alternate key to the Id.
    /// </summary>
    public int Key { get; private init; }

    /// <summary>
    /// What the organization calls this type (e.g. "Service", "Product Line").
    /// </summary>
    public string Name
    {
        get;
        private set => field = Guard.Against.NullOrWhiteSpace(value, nameof(Name)).Trim();
    } = default!;

    /// <summary>
    /// Optional explanation shown to administrators choosing a type.
    /// </summary>
    public string? Description
    {
        get;
        private set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Whether releases can be cut against nodes of this type.
    /// </summary>
    public bool IsReleasable { get; private set; }

    /// <summary>
    /// Display position when choosing a type. Presentation only; implies no hierarchy.
    /// </summary>
    public int Order { get; private set; }

    /// <summary>
    /// Whether this is a platform-seeded default. System types cannot be deleted or edited, only
    /// deactivated, so the set a release ships stays recognisable across installs.
    /// </summary>
    public bool IsSystem { get; private init; }

    /// <summary>
    /// Renames the type and adjusts its display position.
    /// </summary>
    public Result Update(string name, string? description, int order)
    {
        if (IsSystem)
        {
            return Result.Failure("System product types cannot be modified.");
        }

        Name = name;
        Description = description;
        Order = order;

        return Result.Success();
    }

    /// <summary>
    /// Changes whether nodes of this type can carry releases.
    /// </summary>
    /// <remarks>
    /// Refuses new releases only; those already cut stand as historical records, which is why this
    /// raises no event.
    /// </remarks>
    public Result SetReleasable(bool isReleasable)
    {
        if (IsSystem)
        {
            return Result.Failure("System product types cannot be modified.");
        }

        IsReleasable = isReleasable;

        return Result.Success();
    }

    /// <summary>
    /// Takes the type out of use, so no new product can be created with it.
    /// </summary>
    /// <remarks>
    /// Says nothing about products already using it — they keep resolving their type, which is why this
    /// is deactivation rather than deletion. Seeded types can be deactivated: an organization that does
    /// not ship libraries should be able to hide the type without the seeder recreating it.
    /// </remarks>
    public Result Deactivate()
    {
        if (!IsActive)
        {
            return Result.Failure("This product type is already inactive.");
        }

        IsActive = false;

        return Result.Success();
    }

    /// <summary>
    /// Puts the type back into use.
    /// </summary>
    public Result Activate()
    {
        if (IsActive)
        {
            return Result.Failure("This product type is already active.");
        }

        IsActive = true;

        return Result.Success();
    }

    /// <summary>
    /// Creates an organization-defined product type.
    /// </summary>
    public static ProductType Create(string name, string? description, bool isReleasable, int order) =>
        new(name, description, isReleasable, order, isSystem: false);

    /// <summary>
    /// Creates a platform-seeded product type.
    /// </summary>
    public static ProductType CreateSystem(string name, string? description, bool isReleasable, int order) =>
        new(name, description, isReleasable, order, isSystem: true);
}
