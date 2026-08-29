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
    /// Whether this is a platform-seeded default. System types are read-only so an upgrade can reseed
    /// them safely.
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
