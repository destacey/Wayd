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
