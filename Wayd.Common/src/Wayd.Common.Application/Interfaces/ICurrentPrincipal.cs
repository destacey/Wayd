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
    /// <summary>
    /// Whether the caller may use <paramref name="permission"/>: the user holds it, and the scope of the token the
    /// request carries allows it. Use it for what the caller may do.
    /// </summary>
    Task<bool> HasPermission(string permission, CancellationToken cancellationToken = default);

    /// <summary>True when the caller may use at least one of the given permissions, as <see cref="HasPermission"/> decides.</summary>
    Task<bool> HasAnyPermission(IReadOnlyCollection<string> permissions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the user holds <paramref name="permission"/>, whatever the scope of the token the request carries. Use
    /// it only to decide what the caller may see, so a read-only token reads what its user can: a permission such as
    /// Review or Administer also widens what its holder sees.
    /// </summary>
    /// <remarks>
    /// A rule that reads and commands share may use it: a read-only token never reaches a command, because the API
    /// refuses it every request that could change data.
    /// </remarks>
    Task<bool> HoldsPermission(string permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the token the request carries is limited to reading, so the caller can change nothing whatever they
    /// hold. A hint that answers whether the caller may change a record (<c>canManage</c>, <c>canEdit</c>) answers
    /// false when it is, including one decided by who the caller is rather than by a permission.
    /// </summary>
    bool IsReadOnly { get; }

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
