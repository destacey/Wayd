using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Wayd.Infrastructure.Identity;

/// <summary>
/// The line user administration does not cross: accounts holding a privileged permission, and roles granting one,
/// are reached only by a caller holding <c>Permissions.PrivilegedUsers.Manage</c>; and nobody changes their own
/// roles or employee link.
/// </summary>
/// <remarks>
/// Everything short of that line is delegable and recorded in the Activity log, as on any help desk: whoever can reset
/// an ordinary user's password can act as that user. The privileged permissions are the ones that change other
/// people's access (<see cref="ApplicationPermissions.Privileged"/>), so reaching an account holding one is reaching
/// administration itself. Every check compares permissions; none asks which role an account is in.
/// </remarks>
internal partial class UserService
{
    /// <summary>Refusal for user administration on a privileged account without the permission to reach one.</summary>
    private const string PrivilegedAccount =
        "This user holds a privileged permission. Changing their account needs the Manage Privileged Users permission.";

    /// <summary>Refusal for granting a role that carries a privileged permission without the permission to.</summary>
    private const string PrivilegedRole =
        "This role grants a privileged permission. Assigning it needs the Manage Privileged Users permission.";

    /// <summary>Refusal for changing the employee linked to the caller's own account.</summary>
    private const string OwnEmployeeLink = "You cannot change the employee linked to your own account.";

    /// <summary>Refusal for changing the caller's own roles.</summary>
    private const string OwnRoles = "You cannot change your own roles. Ask another administrator.";

    private static readonly string _managePrivilegedUsers =
        ApplicationPermission.NameFor(ApplicationAction.Manage, ApplicationResource.PrivilegedUsers);

    private static readonly HashSet<string> _privileged = [.. ApplicationPermissions.Privileged.Select(p => p.Name)];

    private static readonly HashSet<string> _catalog = [.. ApplicationPermissions.Admin.Select(p => p.Name)];

    /// <summary>
    /// Throws <see cref="ForbiddenException"/> when <paramref name="target"/> is privileged and the caller may not
    /// reach privileged accounts. The target's roles count even while they are inactive, so a deactivated
    /// administrator cannot be taken over and then reactivated.
    /// </summary>
    private async Task EnsureMayManage(ApplicationUser target, CancellationToken cancellationToken)
    {
        if (!await MayManage(target, cancellationToken))
        {
            _logger.LogWarning("User {CallerId} was refused a change to privileged user {UserId}.", _currentUser.GetUserId(), target.Id);
            throw new ForbiddenException(PrivilegedAccount);
        }
    }

    /// <summary>Whether the caller may change <paramref name="target"/>'s account.</summary>
    private async Task<bool> MayManage(ApplicationUser target, CancellationToken cancellationToken) =>
        !await IsPrivileged(await _userManager.GetRolesAsync(target) ?? [], cancellationToken)
            || await CallerMayReachPrivileged(cancellationToken);

    /// <summary>
    /// Throws <see cref="ForbiddenException"/> when any role named in <paramref name="roleNames"/> grants a privileged
    /// permission and the caller may not grant one.
    /// </summary>
    private async Task EnsureMayGrant(IEnumerable<string> roleNames, CancellationToken cancellationToken)
    {
        if (await IsPrivileged(roleNames, cancellationToken) && !await CallerMayReachPrivileged(cancellationToken))
        {
            _logger.LogWarning("User {CallerId} was refused granting a privileged role.", _currentUser.GetUserId());
            throw new ForbiddenException(PrivilegedRole);
        }
    }

    /// <summary>
    /// Throws <see cref="ForbiddenException"/> with <paramref name="message"/> when <paramref name="userId"/> is the
    /// caller. Pass the id of the user as loaded, never as requested: the database matches ids without regard to case.
    /// </summary>
    private void EnsureNotSelf(string userId, string message)
    {
        if (string.Equals(userId, _currentUser.GetUserId(), StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException(message);
    }

    /// <summary>Whether the roles named <paramref name="roleNames"/> grant any privileged permission between them.</summary>
    private async Task<bool> IsPrivileged(IEnumerable<string> roleNames, CancellationToken cancellationToken) =>
        (await RolePermissions(roleNames, cancellationToken)).Overlaps(_privileged);

    /// <summary>
    /// Whether the caller holds <c>Permissions.PrivilegedUsers.Manage</c>. An anonymous, unknown or inactive caller
    /// holds nothing, so background work reaches no privileged account either.
    /// </summary>
    private async Task<bool> CallerMayReachPrivileged(CancellationToken cancellationToken)
    {
        var callerId = _currentUser.GetUserId();
        if (string.IsNullOrEmpty(callerId))
            return false;

        var caller = await _userManager.FindByIdAsync(callerId);
        if (caller is null || !caller.IsActive)
            return false;

        return (await RolePermissions(await _userManager.GetRolesAsync(caller) ?? [], cancellationToken)).Contains(_managePrivilegedUsers);
    }

    /// <summary>The catalog permissions the roles named <paramref name="roleNames"/> grant between them.</summary>
    /// <remarks>A claim the catalog does not define grants nothing, so it is left out.</remarks>
    private async Task<HashSet<string>> RolePermissions(IEnumerable<string> roleNames, CancellationToken cancellationToken)
    {
        var names = roleNames.ToList();
        var permissions = new HashSet<string>(StringComparer.Ordinal);
        if (names.Count == 0)
            return permissions;

        var roles = await _roleManager.Roles
            .Where(r => names.Contains(r.Name!))
            .ToListAsync(cancellationToken);

        foreach (var role in roles)
        {
            var claims = await _roleManager.GetClaimsAsync(role) ?? [];
            permissions.UnionWith(claims
                .Where(c => c.Type == ApplicationClaims.Permission && _catalog.Contains(c.Value))
                .Select(c => c.Value));
        }

        return permissions;
    }

    /// <summary>Every role, what it grants and who holds it, for the rule that one administrator always remains.</summary>
    private Task<Administrators> LoadAdministrators(CancellationToken cancellationToken) =>
        Administrators.Load(_userManager, _roleManager, cancellationToken);
}
