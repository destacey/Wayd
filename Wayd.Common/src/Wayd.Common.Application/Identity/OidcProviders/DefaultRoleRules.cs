using Wayd.Common.Domain.Authorization;
using Wayd.Common.Application.Identity.Roles;

namespace Wayd.Common.Application.Identity.OidcProviders;

/// <summary>
/// What a role must be to serve as a provider's default role: one granted, without anyone choosing it, to every
/// account auto-registration creates. A built-in role or one granting a privileged permission would hand
/// administration to whoever can sign in.
/// </summary>
public static class DefaultRoleRules
{
    /// <summary>The refusal of a built-in or privileged role as a provider's default role.</summary>
    public const string PrivilegedRole =
        "The default role cannot be a built-in role or one that grants a privileged permission.";

    private static readonly HashSet<string> _privileged =
        [.. ApplicationPermissions.Privileged.Select(p => p.Name)];

    /// <summary>Whether a role granting <paramref name="permissions"/> may be a provider's default role.</summary>
    public static bool MayBeDefault(string roleName, IEnumerable<string> permissions) =>
        !ApplicationRoles.IsDefault(roleName) && !permissions.Any(_privileged.Contains);

    /// <summary>
    /// Whether the role <paramref name="roleId"/> may be a provider's default role. A role that does not exist passes:
    /// that is the existence rule's refusal to make.
    /// </summary>
    public static async Task<bool> MayBeDefault(IRoleService roleService, string? roleId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(roleId))
            return true;

        var role = await roleService.GetByIdWithPermissions(roleId.Trim(), cancellationToken);
        return role is null || MayBeDefault(role.Name, role.Permissions ?? []);
    }
}
