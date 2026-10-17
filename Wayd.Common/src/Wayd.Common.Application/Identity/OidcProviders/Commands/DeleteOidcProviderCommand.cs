namespace Wayd.Common.Application.Identity.OidcProviders.Commands;

/// <summary>
/// Deletes an OIDC provider configuration. The handler refuses if any user
/// is still actively bound to the provider through <c>UserIdentity</c> —
/// deleting the row would orphan those identities and lock the affected
/// users out. Admin must rebind or unlink the users first.
/// </summary>
public sealed record DeleteOidcProviderCommand(Guid Id) : ICommand<DeleteOidcProviderResult>;

/// <summary>
/// Outcome of deleting an OIDC provider: either it was deleted, or deletion was refused
/// because users still sign in through it, with how many active identities remain bound.
/// </summary>
/// <remarks>
/// Lets the controller distinguish "deleted" from "blocked because N users still depend on this provider".
/// </remarks>
public sealed record DeleteOidcProviderResult(bool Deleted, int ActiveIdentityCount);
