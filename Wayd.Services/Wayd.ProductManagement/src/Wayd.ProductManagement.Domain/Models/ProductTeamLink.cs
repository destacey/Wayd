using Ardalis.GuardClauses;

namespace Wayd.ProductManagement.Domain.Models;

/// <summary>
/// A team or team of teams linked to a product it works on — Payments on Storefront Web.
/// </summary>
/// <remarks>
/// Owned by the product. The team only reads these: a team's products are a query over products' links.
/// <para>
/// Says only that the team works on the product: not who owns it, not who supports it, and not since when.
/// Unlinking deletes it; the product's Activity keeps the linking and unlinking.
/// </para>
/// </remarks>
public sealed class ProductTeamLink : BaseAuditableEntity
{
    private ProductTeamLink() { }

    internal ProductTeamLink(Guid productId, Guid teamId)
    {
        ProductId = Guard.Against.Default(productId, nameof(productId));
        TeamId = Guard.Against.Default(teamId, nameof(teamId));
    }

    /// <summary>The product the team is linked to.</summary>
    public Guid ProductId { get; private init; }

    /// <summary>The team or team of teams linked.</summary>
    public Guid TeamId { get; private init; }

    /// <summary>The team, when it is loaded.</summary>
    /// <remarks>For the read side only. No invariant depends on this being loaded.</remarks>
    public ProductTeam? Team { get; private init; }
}
