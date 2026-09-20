using Ardalis.GuardClauses;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Models;

namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// One product needing another, over a period — Storefront Web depending on Identity Service since March.
/// </summary>
/// <remarks>
/// Owned by the product that has the dependency. The product depended on only reads these; two owners of
/// one fact would be two writers, with the one-open-link rule enforced in two places.
/// <para>
/// Ended rather than deleted when a dependency stops, because what a product depended on over a period is
/// what later attributes a provider's downtime to its consumers. Deleting a link that has since ended would
/// rewrite that history. <see cref="Product.RemoveDependency"/> is for a link that was never true.
/// </para>
/// <para>
/// Dated by day rather than by instant: dependencies are recorded by hand, and nobody knows the minute one
/// began. <see cref="Period"/> includes its end date, so a link ending on the 31st held through the 31st.
/// </para>
/// <para>
/// <see cref="Strength"/> and <see cref="InteractionStyle"/> never change on a link: a change of either ends
/// it and opens another, so a period of downtime is judged by the terms that held at the time.
/// </para>
/// </remarks>
public sealed class ProductDependency : BaseAuditableEntity
{
    private ProductDependency() { }

    internal ProductDependency(Guid productId, Guid dependsOnProductId, DependencyStrength strength, InteractionStyle? interactionStyle, string? description, LocalDate startsOn)
    {
        ProductId = Guard.Against.Default(productId, nameof(productId));
        DependsOnProductId = Guard.Against.Default(dependsOnProductId, nameof(dependsOnProductId));
        Strength = Guard.Against.EnumOutOfRange(strength, nameof(strength));
        InteractionStyle = interactionStyle;
        Description = description;
        Period = new FlexibleDateRange(startsOn);
    }

    /// <summary>The product that has the dependency.</summary>
    public Guid ProductId { get; private init; }

    /// <summary>The product depended on.</summary>
    public Guid DependsOnProductId { get; private init; }

    /// <summary>The product depended on, when one is loaded.</summary>
    /// <remarks>For the read side only. No invariant depends on this being loaded.</remarks>
    public Product? DependsOnProduct { get; private init; }

    /// <summary>Whether the product stops working without the one it depends on.</summary>
    public DependencyStrength Strength { get; private init; }

    /// <summary>
    /// How the product reaches the one it depends on, or <c>null</c> where nobody has recorded it.
    /// </summary>
    /// <remarks>
    /// Several styles at once, since a pair commonly both calls and subscribes. Settable after construction
    /// only to fill a blank: a style that changes ends the link and opens another, the same as
    /// <see cref="Strength"/>, so that a period of downtime is judged by how the two products talked at the
    /// time. <see cref="Product.UpdateDependencyDetails"/> refuses a change to styles already recorded.
    /// </remarks>
    public InteractionStyle? InteractionStyle
    {
        get;
        internal set
        {
            if (value is { } style && !style.IsValidFlagCombination())
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), style, "An interaction style must name at least one style. Record none as null.");
            }

            field = value;
        }
    }

    /// <summary>What the dependency is for — "validates SSO tokens".</summary>
    public string? Description
    {
        get;
        internal set => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// The days the dependency held, from the day it began through the last day it held. No end while it still
    /// holds.
    /// </summary>
    public FlexibleDateRange Period { get; internal set; } = default!;

    /// <summary>Whether the dependency still holds.</summary>
    public bool IsOpen => Period.End is null;
}
