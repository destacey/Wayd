using Wayd.Common.Domain.Links;

namespace Wayd.ProductManagement.Domain;

/// <summary>
/// The Product Management records that can carry links.
/// </summary>
/// <remarks>
/// Declared here rather than in Common so links carry no Product Management vocabulary. The key is persisted on every
/// link: <strong>never change it once shipped</strong>, or every incident's links are orphaned.
/// </remarks>
public static class ProductManagementLinkOwners
{
    /// <summary>An incident. Spelled as its follow owner type.</summary>
    public static readonly LinkOwnerTypeDescriptor Incident =
        new(ProductManagementFollowOwners.Incident.Key, "Incident", "Incident");

    /// <summary>Every Product Management owner type, for registration.</summary>
    public static readonly LinkOwnerTypeDescriptor[] All = [Incident];

    /// <summary>Registers the Product Management owner types with <see cref="LinkOwnerTypes"/>. Idempotent.</summary>
    public static void Register() => LinkOwnerTypes.Register(All);
}
