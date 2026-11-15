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

        EnsureNotSelf(user.Id, OwnRoles);
        await EnsureMayManage(user, cancellationToken);

        var userCurrentRoles = await _userManager.GetRolesAsync(user);
        await EnsureMayGrant(command.RoleNames.Except(userCurrentRoles, StringComparer.OrdinalIgnoreCase), cancellationToken);

        var administrators = await LoadAdministrators(cancellationToken);
        if (administrators.LosesTheLast(administrators.WithUserRoles(user, command.RoleNames)))
        {
            _logger.LogWarning("Refused to change the roles of user {UserId}, the only active user holding every permission.", user.Id);
            throw new ConflictException(Administrators.KeepOne);
        }

        // REMOVE ROLES
        var rolesToRemove = userCurrentRoles.Except(command.RoleNames);

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

        // Ids are matched as the database matches them, which ignores case, so every rule below works on the users
        // it found: comparing the request's ids instead would let one in another case pass for someone else.
        var toAdd = await _userManager.Users.Where(u => command.UserIdsToAdd.Contains(u.Id)).ToListAsync(cancellationToken);
        var toRemove = await _userManager.Users.Where(u => command.UserIdsToRemove.Contains(u.Id)).ToListAsync(cancellationToken);

        foreach (var user in toAdd.Concat(toRemove))
        {
            EnsureNotSelf(user.Id, OwnRoles);
            await EnsureMayManage(user, cancellationToken);
        }

        if (toAdd.Count > 0)
            await EnsureMayGrant([roleName], cancellationToken);

        // Checked against the additions too, so swapping one administrator for another is allowed, and before
        // anything is saved, so a refusal leaves nothing half done.
        if (toRemove.Count > 0)
        {
            var administrators = await LoadAdministrators(cancellationToken);
            var after = administrators
                .WithRoleMembersAdded(roleName, toAdd)
                .WithoutRoleMembers(roleName, toRemove.Select(u => u.Id));
            if (administrators.LosesTheLast(after))
            {
                _logger.LogWarning("Refused to remove from role {RoleName} every active user holding every permission.", roleName);
                return Result.Failure(Administrators.KeepOne);
            }
        }

        foreach (var userId in command.UserIdsToAdd.Except(toAdd.Select(u => u.Id), StringComparer.OrdinalIgnoreCase))
            _logger.LogWarning("User with ID {UserId} not found. Skipping addition.", userId);

        foreach (var user in toAdd)
        {
            if (await _userManager.IsInRoleAsync(user, roleName))
                continue;

            var result = await AddToRoleRecorded(user, roleName, roleIdsByName, CurrentActor());
            if (!result.Succeeded)
            {
                _logger.LogError("Failed to add user {UserId} to role {RoleName}.", user.Id, roleName);
                return Result.Failure($"Failed to add user {user.Id} to role {roleName}.");
            }
        }

        // REMOVE USERS FROM ROLE
        if (toRemove.Count > 0)
        {
            foreach (var userId in command.UserIdsToRemove.Except(toRemove.Select(u => u.Id), StringComparer.OrdinalIgnoreCase))
                _logger.LogWarning("User with ID {UserId} not found. Skipping removal.", userId);

            foreach (var user in toRemove)
            {
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
                    _logger.LogError("Failed to remove user {UserId} from role {RoleName}.", user.Id, roleName);
                    return Result.Failure($"Failed to remove user {user.Id} from role {roleName}.");
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