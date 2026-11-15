using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Wayd.Infrastructure.Identity;

/// <summary>
/// The accounts that keep Wayd administrable: active users whose roles between them grant every permission in the
/// catalog. Wayd refuses any change that would leave none, since nobody could then restore one.
/// </summary>
/// <remarks>
/// Decided by permissions rather than by membership of the Admin role, so an organisation that builds its own full
/// administrator role is protected the same way. A snapshot is taken per check; it reads every role and its members,
/// which is small next to the user count and avoids a query per user.
/// </remarks>
internal sealed class Administrators
{
    /// <summary>Wayd's answer when a change would leave no administrator.</summary>
    public const string KeepOne = "Wayd must keep at least one active user who holds every permission.";

    private static readonly HashSet<string> _catalog = [.. ApplicationPermissions.Admin.Select(p => p.Name)];

    private readonly Dictionary<string, HashSet<string>> _rolePermissions;
    private readonly Dictionary<string, HashSet<string>> _userRoles;
    private readonly Dictionary<string, ApplicationUser> _users;

    private Administrators(
        Dictionary<string, HashSet<string>> rolePermissions,
        Dictionary<string, HashSet<string>> userRoles,
        Dictionary<string, ApplicationUser> users)
    {
        _rolePermissions = rolePermissions;
        _userRoles = userRoles;
        _users = users;
    }

    /// <summary>Reads every role, what it grants and who holds it.</summary>
    public static async Task<Administrators> Load(
        UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager, CancellationToken cancellationToken)
    {
        var rolePermissions = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var userRoles = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var users = new Dictionary<string, ApplicationUser>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in await roleManager.Roles.ToListAsync(cancellationToken))
        {
            var name = role.Name!;
            rolePermissions[name] = CatalogPermissions(await roleManager.GetClaimsAsync(role) ?? []);

            foreach (var member in await userManager.GetUsersInRoleAsync(name) ?? [])
            {
                users[member.Id] = member;
                if (!userRoles.TryGetValue(member.Id, out var held))
                    userRoles[member.Id] = held = new(StringComparer.OrdinalIgnoreCase);
                held.Add(name);
            }
        }

        return new Administrators(rolePermissions, userRoles, users);
    }

    /// <summary>
    /// Whether <paramref name="user"/> is an active administrator. Reads only that user's roles, for the sign-in path,
    /// where nearly every user is not one.
    /// </summary>
    public static async Task<bool> IsAdministrator(
        UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager, ApplicationUser user, CancellationToken cancellationToken)
    {
        if (!user.IsActive)
            return false;

        var names = (await userManager.GetRolesAsync(user) ?? []).ToList();
        var granted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in await roleManager.Roles.Where(r => names.Contains(r.Name!)).ToListAsync(cancellationToken))
            granted.UnionWith(CatalogPermissions(await roleManager.GetClaimsAsync(role) ?? []));

        return granted.IsSupersetOf(_catalog);
    }

    /// <summary>The active administrators, optionally leaving out those <paramref name="exclude"/> matches.</summary>
    public IReadOnlyList<ApplicationUser> Active(Func<ApplicationUser, bool>? exclude = null) =>
        [.. _users.Values.Where(u => u.IsActive && (exclude is null || !exclude(u)) && Holds(u.Id))];

    /// <summary>Whether a change from this snapshot to <paramref name="after"/> would leave no active administrator.</summary>
    /// <remarks>A change made when there is already none is not refused, so it can be what restores one.</remarks>
    public bool LosesTheLast(Administrators after) => Active().Count > 0 && after.Active().Count == 0;

    /// <summary>This snapshot with <paramref name="userId"/> no longer active.</summary>
    public Administrators WithoutUser(string userId) =>
        new(_rolePermissions, _userRoles, _users.Where(u => !string.Equals(u.Key, userId, StringComparison.OrdinalIgnoreCase)).ToDictionary(u => u.Key, u => u.Value, StringComparer.OrdinalIgnoreCase));

    /// <summary>This snapshot with <paramref name="user"/> holding exactly <paramref name="roleNames"/>.</summary>
    public Administrators WithUserRoles(ApplicationUser user, IEnumerable<string> roleNames)
    {
        var userRoles = CopyUserRoles();
        userRoles[user.Id] = new(roleNames, StringComparer.OrdinalIgnoreCase);
        var users = new Dictionary<string, ApplicationUser>(_users, StringComparer.OrdinalIgnoreCase) { [user.Id] = user };
        return new(_rolePermissions, userRoles, users);
    }

    /// <summary>This snapshot with <paramref name="users"/> put in <paramref name="roleName"/>.</summary>
    public Administrators WithRoleMembersAdded(string roleName, IEnumerable<ApplicationUser> users)
    {
        var userRoles = CopyUserRoles();
        var members = new Dictionary<string, ApplicationUser>(_users, StringComparer.OrdinalIgnoreCase);
        foreach (var user in users)
        {
            members[user.Id] = user;
            if (!userRoles.TryGetValue(user.Id, out var held))
                userRoles[user.Id] = held = new(StringComparer.OrdinalIgnoreCase);
            held.Add(roleName);
        }
        return new(_rolePermissions, userRoles, members);
    }

    /// <summary>This snapshot with <paramref name="userIds"/> taken out of <paramref name="roleName"/>.</summary>
    public Administrators WithoutRoleMembers(string roleName, IEnumerable<string> userIds)
    {
        var userRoles = CopyUserRoles();
        foreach (var id in userIds)
            if (userRoles.TryGetValue(id, out var held))
                held.Remove(roleName);
        return new(_rolePermissions, userRoles, _users);
    }

    /// <summary>This snapshot with <paramref name="roleName"/> granting exactly <paramref name="permissions"/>.</summary>
    public Administrators WithRolePermissions(string roleName, IEnumerable<string> permissions)
    {
        var rolePermissions = new Dictionary<string, HashSet<string>>(_rolePermissions, StringComparer.OrdinalIgnoreCase)
        {
            [roleName] = [.. permissions.Where(_catalog.Contains)],
        };
        return new(rolePermissions, _userRoles, _users);
    }

    private bool Holds(string userId)
    {
        if (!_userRoles.TryGetValue(userId, out var roles))
            return false;

        var granted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roles)
            if (_rolePermissions.TryGetValue(role, out var permissions))
                granted.UnionWith(permissions);

        return granted.IsSupersetOf(_catalog);
    }

    private Dictionary<string, HashSet<string>> CopyUserRoles() =>
        _userRoles.ToDictionary(r => r.Key, r => new HashSet<string>(r.Value, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

    private static HashSet<string> CatalogPermissions(IEnumerable<System.Security.Claims.Claim> claims) =>
        [.. claims.Where(c => c.Type == ApplicationClaims.Permission && _catalog.Contains(c.Value)).Select(c => c.Value)];
}
