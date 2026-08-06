namespace Wayd.Common.Application.Interfaces;

/// <summary>
/// The current caller's principal: "what is the current user allowed to do?" — the authorization half
/// of the identity/principal split. <see cref="ICurrentUser"/> answers only "who is the caller?"; a
/// principal is derived from that identity plus the permission store (<c>IUserService</c>), which
/// itself depends on <see cref="ICurrentUser"/> — folding permission checks into
/// <see cref="ICurrentUser"/> forces a service-locator workaround for that cycle.
/// </summary>
public interface ICurrentPrincipal
{
    Task<bool> HasPermission(string permission, CancellationToken cancellationToken = default);

    /// <summary>True when the current user holds at least one of the given permissions.</summary>
    Task<bool> HasAnyPermission(IReadOnlyCollection<string> permissions, CancellationToken cancellationToken = default);

    /// <summary>
    /// The employee this caller acts as, resolved from the user record rather than from a token claim,
    /// or <c>null</c> when the account has no employee link.
    /// </summary>
    /// <remarks>
    /// Prefer this over <see cref="ICurrentUser.GetEmployeeId"/> anywhere the answer gates behaviour.
    /// The claim is a snapshot: a JWT carries the link as it stood at sign-in, so linking a user has no
    /// effect until they re-authenticate, and a personal access token carries the link as it stood at
    /// token *creation* for that token's whole lifetime. This reads the current value once per scope,
    /// the same cost profile as the permission lookup beside it.
    /// </remarks>
    Task<Guid?> GetEmployeeId(CancellationToken cancellationToken = default);
}
