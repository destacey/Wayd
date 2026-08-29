using CSharpFunctionalExtensions;
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

        var result = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
        if (!result.Succeeded)
        {
            _logger.LogError("Failed to remove roles from user.");
            throw new InternalServerException("Failed to remove roles from user.");
        }

        // ADD ROLES
        var rolesToAdd = command.RoleNames.Except(userCurrentRoles);

        result = await _userManager.AddToRolesAsync(user, rolesToAdd);
        if (!result.Succeeded)
        {
            _logger.LogError("Failed to add roles to user.");
            throw new InternalServerException("Failed to add roles to user.");
        }

        await _events.PublishAsync(new ApplicationUserUpdatedEvent(user.Id, EventActor.User(_currentUser.GetUserId()), _dateTimeProvider.Now, true));

        return Result.Success();
    }

    public async Task<Result> ManageRoleUsersAsync(ManageRoleUsersCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        var role = await _roleManager.FindByIdAsync(command.RoleId);
        if (role is null)
            return Result.Failure("Role not found.");

        var roleName = role.Name!;
        var usersUpdated = new List<string>();

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

            var result = await _userManager.AddToRoleAsync(user, roleName);
            if (!result.Succeeded)
            {
                _logger.LogError("Failed to add user {UserId} to role {RoleName}.", userId, roleName);
                return Result.Failure($"Failed to add user {userId} to role {roleName}.");
            }

            usersUpdated.Add(user.Id);
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

                var result = await _userManager.RemoveFromRoleAsync(user, roleName);
                if (!result.Succeeded)
                {
                    _logger.LogError("Failed to remove user {UserId} from role {RoleName}.", userId, roleName);
                    return Result.Failure($"Failed to remove user {userId} from role {roleName}.");
                }

                usersUpdated.Add(user.Id);
            }
        }

        // PUBLISH EVENTS
        foreach (var userId in usersUpdated)
        {
            await _events.PublishAsync(new ApplicationUserUpdatedEvent(userId, EventActor.User(_currentUser.GetUserId()), _dateTimeProvider.Now, true));
        }

        return Result.Success();
    }
}