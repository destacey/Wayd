using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Wayd.Infrastructure.Identity;

internal partial class UserService
{
    public async Task<List<UserRoleDto>> GetRolesAsync(string userId, bool includeUnassigned, CancellationToken cancellationToken)
    {
        var userRoles = new List<UserRoleDto>();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return [];

        var roles = await _roleManager.Roles.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var role in roles)
        {
            userRoles.Add(new UserRoleDto
            {
                RoleId = role.Id,
                RoleName = role.Name,
                Description = role.Description,
                Enabled = await _userManager.IsInRoleAsync(user, role.Name!)
            });
        }

        return includeUnassigned ? userRoles : [.. userRoles.Where(r => r.Enabled)];
    }

    public async Task<Result> AssignRolesAsync(AssignUserRolesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);

        _ = user ?? throw new NotFoundException("User Not Found.");

        var userCurrentRoles = await _userManager.GetRolesAsync(user);

        // REMOVE ROLES
        var rolesToRemove = userCurrentRoles.Except(command.RoleNames);

        if (rolesToRemove.Contains(ApplicationRoles.Admin))
        {
            var adminCount = (await _userManager.GetUsersInRoleAsync(ApplicationRoles.Admin)).Count;
            if (adminCount <= 1)
            {
                _logger.LogWarning("Wayd should have at least 1 Admin.");
                throw new ConflictException("Wayd should have at least 1 Admin.");
            }
        }

        var roleIdsByName = await GetRoleIdsByName(cancellationToken);

        // The manager saves the removals and the additions separately; one transaction keeps a failed addition
        // from leaving the removals, and the event recording both, committed on their own.
        await _userIdentityStore.ExecuteInTransaction(async ct =>
        {
            var result = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!result.Succeeded)
            {
                _logger.LogError("Failed to remove roles from user.");
                throw new InternalServerException("Failed to remove roles from user.");
            }

            // Raised before the additions so their save records it.
            user.RecordRolesChange(
                RoleIds(userCurrentRoles, roleIdsByName),
                RoleIds(command.RoleNames, roleIdsByName),
                CurrentActor(),
                _dateTimeProvider.Now);

            var rolesToAdd = command.RoleNames.Except(userCurrentRoles);

            result = await _userManager.AddToRolesAsync(user, rolesToAdd);
            if (!result.Succeeded)
            {
                user.ClearDomainEvents();
                _logger.LogError("Failed to add roles to user.");
                throw new InternalServerException("Failed to add roles to user.");
            }
        }, cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ManageRoleUsersAsync(ManageRoleUsersCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var role = await _roleManager.FindByIdAsync(command.RoleId);
        if (role is null)
            return Result.Failure("Role not found.");

        var roleName = role.Name!;
        var roleIdsByName = await GetRoleIdsByName(cancellationToken);

        // ADD USERS TO ROLE (first, so admin swap scenarios work correctly)
        foreach (var userId in command.UserIdsToAdd)
        {
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user is null)
            {
                _logger.LogWarning("User with ID {UserId} not found. Skipping addition.", userId);
                continue;
            }

            if (await _userManager.IsInRoleAsync(user, roleName))
                continue;

            var result = await AddToRoleRecorded(user, roleName, roleIdsByName, CurrentActor());
            if (!result.Succeeded)
            {
                _logger.LogError("Failed to add user {UserId} to role {RoleName}.", userId, roleName);
                return Result.Failure($"Failed to add user {userId} to role {roleName}.");
            }
        }

        // REMOVE USERS FROM ROLE
        if (command.UserIdsToRemove.Count > 0)
        {
            if (roleName == ApplicationRoles.Admin)
            {
                var adminCount = (await _userManager.GetUsersInRoleAsync(ApplicationRoles.Admin)).Count;
                var removalCount = command.UserIdsToRemove.Count;
                if (adminCount - removalCount < 1)
                {
                    _logger.LogWarning("Wayd should have at least 1 Admin.");
                    return Result.Failure("Wayd should have at least 1 Admin.");
                }
            }

            foreach (var userId in command.UserIdsToRemove)
            {
                var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
                if (user is null)
                {
                    _logger.LogWarning("User with ID {UserId} not found. Skipping removal.", userId);
                    continue;
                }

                if (!await _userManager.IsInRoleAsync(user, roleName))
                    continue;

                var currentRoles = await _userManager.GetRolesAsync(user);
                user.RecordRolesChange(
                    RoleIds(currentRoles, roleIdsByName),
                    RoleIds(currentRoles.Where(r => r != roleName), roleIdsByName),
                    CurrentActor(),
                    _dateTimeProvider.Now);

                var result = await _userManager.RemoveFromRoleAsync(user, roleName);
                if (!result.Succeeded)
                {
                    user.ClearDomainEvents();
                    _logger.LogError("Failed to remove user {UserId} from role {RoleName}.", userId, roleName);
                    return Result.Failure($"Failed to remove user {userId} from role {roleName}.");
                }
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Adds the user to a role and records the change, raised before the manager's save so that save drains it.
    /// A failed add clears it, since nothing was saved.
    /// </summary>
    private async Task<IdentityResult> AddToRoleRecorded(
        ApplicationUser user, string roleName, IReadOnlyDictionary<string, string> roleIdsByName, EventActor actor)
    {
        var currentRoles = await _userManager.GetRolesAsync(user);
        user.RecordRolesChange(
            RoleIds(currentRoles, roleIdsByName),
            RoleIds(currentRoles.Append(roleName), roleIdsByName),
            actor,
            _dateTimeProvider.Now);

        var result = await _userManager.AddToRoleAsync(user, roleName);
        if (!result.Succeeded)
        {
            user.ClearDomainEvents();
        }

        return result;
    }

    /// <summary>
    /// Role ids keyed by role name. The manager works in names, and a role change records ids because a role can
    /// be renamed.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>> GetRoleIdsByName(CancellationToken cancellationToken)
    {
        var roles = await _roleManager.Roles
            .Select(r => new { r.Id, r.Name })
            .ToListAsync(cancellationToken);

        return roles.ToDictionary(r => r.Name!, r => r.Id, StringComparer.OrdinalIgnoreCase);
    }

    /// <remarks>
    /// A name with no role is left out rather than thrown on: the manager call that follows rejects it, and the
    /// change is then discarded along with the event.
    /// </remarks>
    private static IEnumerable<string> RoleIds(IEnumerable<string> roleNames, IReadOnlyDictionary<string, string> roleIdsByName) =>
        roleNames.Select(name => roleIdsByName.GetValueOrDefault(name)).OfType<string>();
}