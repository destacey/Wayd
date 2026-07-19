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
}
