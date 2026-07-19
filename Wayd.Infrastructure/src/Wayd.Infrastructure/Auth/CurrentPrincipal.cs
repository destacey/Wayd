namespace Wayd.Infrastructure.Auth;

/// <summary>
/// Scoped principal for the current caller: resolves the permission set once per request/message
/// scope (lazily, on the first check — most scopes never ask) and caches it for the remaining checks
/// in that scope. Derived from <see cref="ICurrentUser"/> (identity) via <see cref="IUserService"/>
/// (permission store); keeping this a separate node is what breaks the CurrentUser ↔ UserService
/// dependency cycle without a service locator.
/// </summary>
internal class CurrentPrincipal(ICurrentUser currentUser, IUserService userService) : ICurrentPrincipal
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUserService _userService = userService;

    private HashSet<string>? _permissionsCache;

    public async Task<bool> HasPermission(string permission, CancellationToken cancellationToken = default)
    {
        switch (_currentUser.Kind)
        {
            // The platform acting on its own behalf (jobs, durable messages, startup) is not
            // permission-gated — the blog-model SystemPrincipal. Lets background flows pass through
            // permission-checked code paths without impersonating a user.
            case ActorKind.System:
                return true;

            // Deny-all for anonymous HTTP callers; also guards the store lookup below, which throws
            // NotFound for an empty user id.
            case ActorKind.Anonymous:
                return false;
        }

        _permissionsCache ??= [.. await _userService.GetPermissionsAsync(_currentUser.GetUserId(), cancellationToken)];

        return _permissionsCache.Contains(permission);
    }

    public async Task<bool> HasAnyPermission(IReadOnlyCollection<string> permissions, CancellationToken cancellationToken = default)
    {
        foreach (var permission in permissions)
        {
            if (await HasPermission(permission, cancellationToken))
                return true;
        }

        return false;
    }
}
