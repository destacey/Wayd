namespace Wayd.Infrastructure.Auth;

/// <summary>
/// Scoped principal for the current caller: resolves what the caller may do (their permission set)
/// and which employee they act as, each lazily on first use — most scopes ask for neither — and
/// caches the answer for the remaining checks in that scope. Derived from <see cref="ICurrentUser"/>
/// (identity) via <see cref="IUserService"/> (the user store); keeping this a separate node is what
/// breaks the CurrentUser ↔ UserService dependency cycle without a service locator.
/// </summary>
/// <remarks>
/// Both lookups read the database rather than the caller's token. Claims are a snapshot taken when
/// the token was issued, which is why <see cref="ICurrentUser.GetEmployeeId"/> (claim-sourced) goes
/// stale — see <see cref="ICurrentPrincipal.GetEmployeeId"/>.
/// </remarks>
internal class CurrentPrincipal(ICurrentUser currentUser, IUserService userService) : ICurrentPrincipal
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUserService _userService = userService;

    private HashSet<string>? _permissionsCache;

    // Two fields, not a Guid? cache: "no link" is a normal, common answer (service accounts, admins,
    // external users), and a nullable cache alone cannot tell it apart from "not looked up yet" —
    // every unlinked caller would re-query on each check.
    private bool _employeeIdResolved;
    private Guid? _employeeIdCache;

    public async Task<bool> HasPermission(string permission, CancellationToken cancellationToken = default)
    {
        switch (_currentUser.Kind)
        {
            // The platform acting on its own behalf (jobs, durable messages, startup) is not
            // permission-gated: system scopes hold every permission, so background flows can pass
            // through permission-checked code paths without impersonating a user.
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

    public async Task<Guid?> GetEmployeeId(CancellationToken cancellationToken = default)
    {
        if (_employeeIdResolved)
        {
            return _employeeIdCache;
        }

        // The platform acting on its own behalf is not a person and has no employee record; an
        // anonymous caller has no user row to read. Neither should reach the store — the lookup below
        // would be a guaranteed miss on a non-existent user id.
        if (_currentUser.Kind is ActorKind.System or ActorKind.Anonymous)
        {
            _employeeIdResolved = true;
            return _employeeIdCache = null;
        }

        _employeeIdCache = await _userService.GetEmployeeIdAsync(_currentUser.GetUserId(), cancellationToken);
        _employeeIdResolved = true;

        return _employeeIdCache;
    }
}
