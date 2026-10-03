using Wayd.Common.Domain.Identity;

namespace Wayd.Common.Domain.Events.Identity;

/// <summary>
/// The label an OIDC provider shows on the login page, as <see cref="OidcProviderDetailsUpdatedEvent.Previous"/>
/// records the value an edit replaced.
/// </summary>
public sealed record OidcProviderDetails(string Label);

/// <summary>
/// How Wayd validates tokens from an OIDC provider and what the login page asks it for. Nothing in it is a
/// secret; a provider stores none.
/// </summary>
/// <param name="AllowedTenantIds">The tenant allowlist; null for a provider that does not restrict tenants.</param>
public sealed record OidcProviderConfiguration(
    string Authority,
    string ClientId,
    string Audience,
    string[] Scopes,
    string[]? AllowedTenantIds,
    int ClockSkewSeconds)
{
    /// <summary>Equal in every value, ignoring the order of scopes and tenants.</summary>
    public bool IsEquivalentTo(OidcProviderConfiguration other) =>
        Authority == other.Authority
        && ClientId == other.ClientId
        && Audience == other.Audience
        && ClockSkewSeconds == other.ClockSkewSeconds
        && SameSet(Scopes, other.Scopes, StringComparer.Ordinal)
        && SameSet(AllowedTenantIds ?? [], other.AllowedTenantIds ?? [], StringComparer.OrdinalIgnoreCase);

    private static bool SameSet(string[] a, string[] b, StringComparer comparer) =>
        a.ToHashSet(comparer).SetEquals(b);
}

/// <summary>
/// An OIDC provider's just-in-time provisioning policy, in <see cref="RegistrationPolicy"/>'s shape: the
/// dependent settings are null exactly when auto-registration is off.
/// </summary>
/// <param name="DefaultRoleId">The role auto-registered users are given.</param>
public sealed record OidcProviderRegistrationPolicy(bool AllowAutoRegistration, bool? RequireEmployeeRecord, string? DefaultRoleId)
{
    public static OidcProviderRegistrationPolicy From(RegistrationPolicy policy) =>
        new(policy.AllowAutoRegistration, policy.RequireEmployeeRecord, policy.DefaultRoleId);
}
