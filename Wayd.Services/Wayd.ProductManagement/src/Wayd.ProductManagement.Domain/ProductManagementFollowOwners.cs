using Wayd.Common.Domain.Following;

namespace Wayd.ProductManagement.Domain;

/// <summary>
/// The Product Management records people can follow.
/// </summary>
/// <remarks>
/// Declared here rather than in Common so following carries no Product Management vocabulary. The keys are
/// persisted on every follow row: <strong>never change one once shipped</strong>, or every follow of that
/// kind is orphaned.
/// </remarks>
public static class ProductManagementFollowOwners
{
    /// <summary>An operational incident in the product catalog.</summary>
    public static readonly FollowOwnerDescriptor Incident = new("product.incident", "Incident");

    /// <summary>Every Product Management owner type, for registration.</summary>
    public static readonly FollowOwnerDescriptor[] All = [Incident];

    /// <summary>Registers the Product Management owner types with <see cref="FollowOwners"/>. Idempotent.</summary>
    public static void Register() => FollowOwners.Register(All);
}
