using Microsoft.EntityFrameworkCore;

namespace Wayd.Infrastructure.Identity;

internal partial class UserService
{
    /// <summary>
    /// The permissions <paramref name="userId"/>'s roles grant, or none for a deactivated user, so a principal built
    /// before the deactivation cannot use a permission it no longer holds.
    /// </summary>
    public async Task<List<string>> GetPermissionsAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId);

        _ = user ?? throw new NotFoundException("User Not Found.");

        if (!user.IsActive)
            return [];

        var userRoles = await _userManager.GetRolesAsync(user);
        var permissions = new List<string>();

        var roles = await _roleManager.Roles
            .Where(r => userRoles.Contains(r.Name!))
            .ToListAsync(cancellationToken);

        foreach (var role in roles)
        {
            var claims = await _db.RoleClaims
                .Where(rc => rc.RoleId == role.Id && rc.ClaimType == ApplicationClaims.Permission)
                .Select(rc => rc.ClaimValue)
                .ToListAsync(cancellationToken);

            if (claims is not null && claims.Count != 0)
                permissions.AddRange(claims!);
        }

        return [.. permissions.Distinct()];
    }

    public async Task<bool> HasPermissionAsync(string userId, string permission, CancellationToken cancellationToken)
    {
        var permissions = await GetPermissionsAsync(userId, cancellationToken);

        return permissions?.Contains(permission) ?? false;
    }
}