namespace Wayd.Common.Application.Identity.OidcProviders.Commands;

/// <summary>
/// Deletes an OIDC provider configuration. The handler refuses if any user
/// is still actively bound to the provider through <c>UserIdentity</c> —
/// deleting the row would orphan those identities and lock the affected
/// users out. Admin must rebind or unlink the users first.
/// </summary>
public sealed record DeleteOidcProviderCommand(Guid Id) : ICommand<DeleteOidcProviderResult>;

/// <summary>
/// Result envelope so the controller can distinguish "deleted" from
/// "blocked because N users still depend on this provider".
/// </summary>
public sealed record DeleteOidcProviderResult(bool Deleted, int ActiveIdentityCount);
