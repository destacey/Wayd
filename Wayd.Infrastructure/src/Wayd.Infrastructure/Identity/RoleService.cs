using CSharpFunctionalExtensions;
using FluentValidation.Results;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Wayd.Infrastructure.Identity;

internal class RoleService(
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager,
    WaydDbContext db,
    IOidcProviderDefaultRoleChecker defaultRoleChecker,
    ICurrentUser currentUser,
    IEventPublisher events,
    IDateTimeProvider dateTimeProvider,
    ILogger<RoleService> logger) : IRoleService
{
    private readonly RoleManager<ApplicationRole> _roleManager = roleManager;
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly WaydDbContext _db = db;
    private readonly IOidcProviderDefaultRoleChecker _defaultRoleChecker = defaultRoleChecker;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IEventPublisher _events = events;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<RoleService> _logger = logger;

    public async Task<List<RoleListDto>> GetList(CancellationToken cancellationToken)
        => await _roleManager.Roles.ProjectToType<RoleListDto>().ToListAsync(cancellationToken);

    public async Task<int> GetCount(CancellationToken cancellationToken) =>
        await _roleManager.Roles.CountAsync(cancellationToken);

    public async Task<bool> Exists(string roleName, string? excludeId) =>
        await _roleManager.FindByNameAsync(roleName)
            is ApplicationRole existingRole
            && existingRole.Id != excludeId;

    public async Task<RoleDto?> GetById(string id, CancellationToken cancellationToken)
    {
        var role = await _db.Roles.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return role?.Adapt<RoleDto>();
    }

    public async Task<RoleDto?> GetByIdWithPermissions(string roleId, CancellationToken cancellationToken)
    {
        var role = await GetById(roleId, cancellationToken);
        if (role == null)
        {
            _logger.LogDebug("Role {RoleId} not found when loading permissions.", roleId);
            return null;
        }

        role.Permissions = await _db.RoleClaims
            .Where(c => c.RoleId == roleId && c.ClaimType == ApplicationClaims.Permission)
            .Select(c => c.ClaimValue!)
            .ToListAsync(cancellationToken);

        return role;
    }

    public async Task<string> CreateOrUpdate(CreateOrUpdateRoleCommand request)
    {
        if (string.IsNullOrEmpty(request.Id))
        {
            _logger.LogInformation("Role create requested for {RoleName} by user {UserId}.", request.Name, _currentUser.GetUserId());

            // Create a new role.
            var role = new ApplicationRole(request.Name, request.Description);
            var result = await _roleManager.CreateAsync(role);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Role create failed for {RoleName}: {Errors}",
                    request.Name, string.Join("; ", result.Errors.Select(e => e.Description)));
                HandleValidationErrors(result);

                throw new InternalServerException("Register role failed");
            }

            await _events.PublishAsync(new ApplicationRoleCreatedEvent(role.Id, role.Name!, _dateTimeProvider.Now));

            _logger.LogInformation("Role {RoleName} ({RoleId}) created by user {UserId}.", role.Name, role.Id, _currentUser.GetUserId());

            return role.Id;
        }
        else
        {
            _logger.LogInformation("Role update requested for {RoleId} by user {UserId}.", request.Id, _currentUser.GetUserId());

            // Update an existing role.
            var role = await _roleManager.FindByIdAsync(request.Id);

            if (role is null)
            {
                _logger.LogWarning("Role update failed: role {RoleId} not found.", request.Id);
                throw new NotFoundException("Role Not Found");
            }

            if (ApplicationRoles.IsDefault(role.Name!))
            {
                _logger.LogWarning("Role update denied: {RoleName} ({RoleId}) is a built-in system role.", role.Name, role.Id);
                throw new ConflictException(string.Format("Not allowed to modify {0} Role.", role.Name));
            }

            role.Update(request.Name, request.Description);
            var result = await _roleManager.UpdateAsync(role);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Role update failed for {RoleId}: {Errors}",
                    role.Id, string.Join("; ", result.Errors.Select(e => e.Description)));
                HandleValidationErrors(result);

                throw new InternalServerException("Update role failed");
            }

            await _events.PublishAsync(new ApplicationRoleUpdatedEvent(role.Id, role.Name!, _dateTimeProvider.Now));

            _logger.LogInformation("Role {RoleName} ({RoleId}) updated by user {UserId}.", role.Name, role.Id, _currentUser.GetUserId());

            return role.Id;
        }
    }

    public async Task<Result> UpdatePermissions(UpdateRolePermissionsCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Role permissions update requested for {RoleId} by user {UserId}.", request.RoleId, _currentUser.GetUserId());

        var role = await _roleManager.FindByIdAsync(request.RoleId);
        if (role is null)
        {
            _logger.LogWarning("Role permissions update failed: role {RoleId} not found.", request.RoleId);
            throw new NotFoundException("Role Not Found");
        }

        if (role.Name == ApplicationRoles.Admin)
        {
            _logger.LogWarning("Role permissions update denied: {RoleName} ({RoleId}) permissions are not modifiable.", role.Name, role.Id);
            return Result.Failure("Not allowed to modify Permissions for this Role.");
        }

        //if (_currentTenant.Id != MultitenancyConstants.Root.Id)
        //{
        //    // Remove Root Permissions if the Role is not created for Root Tenant.
        //    request.Permissions.RemoveAll(u => u.StartsWith("Permissions.Root."));
        //}

        var currentClaims = await _roleManager.GetClaimsAsync(role);

        // Remove permissions that were previously selected
        var removed = 0;
        foreach (var claim in currentClaims.Where(c => !request.Permissions.Any(p => p == c.Value)))
        {
            var removeResult = await _roleManager.RemoveClaimAsync(role, claim);
            if (!removeResult.Succeeded)
            {
                _logger.LogWarning("Role permissions update failed removing claim {Permission} from {RoleId}: {Errors}",
                    claim.Value, role.Id, string.Join("; ", removeResult.Errors.Select(e => e.Description)));
                return Result.Failure("Update permissions failed.");
            }

            removed++;
        }

        // Add all permissions that were not previously selected
        var added = 0;
        foreach (string permission in request.Permissions.Where(c => !currentClaims.Any(p => p.Value == c)))
        {
            if (!string.IsNullOrEmpty(permission))
            {
                _db.RoleClaims.Add(new ApplicationRoleClaim
                {
                    RoleId = role.Id,
                    ClaimType = ApplicationClaims.Permission,
                    ClaimValue = permission,
                    CreatedBy = _currentUser.GetUserId()
                });
                await _db.SaveChangesAsync(cancellationToken);
                added++;
            }
        }

        await _events.PublishAsync(new ApplicationRoleUpdatedEvent(role.Id, role.Name!, _dateTimeProvider.Now, true));

        _logger.LogInformation("Role {RoleName} ({RoleId}) permissions updated by user {UserId}: {Added} added, {Removed} removed.",
            role.Name, role.Id, _currentUser.GetUserId(), added, removed);

        return Result.Success();
    }

    public async Task<string> Delete(string id)
    {
        _logger.LogInformation("Role delete requested for {RoleId} by user {UserId}.", id, _currentUser.GetUserId());

        var role = await _roleManager.FindByIdAsync(id);

        if (role is null)
        {
            _logger.LogWarning("Role delete failed: role {RoleId} not found.", id);
            throw new NotFoundException("Role Not Found");
        }

        if (ApplicationRoles.IsDefault(role.Name!))
        {
            _logger.LogWarning("Role delete denied: {RoleName} ({RoleId}) is a built-in system role.", role.Name, role.Id);
            throw new ConflictException(string.Format("Not allowed to delete the {0} Role.", role.Name));
        }

        var usersInRole = (await _userManager.GetUsersInRoleAsync(role.Name!)).Count;
        if (usersInRole > 0)
        {
            _logger.LogWarning("Role delete denied: {RoleName} ({RoleId}) is assigned to {UserCount} user(s).",
                role.Name, role.Id, usersInRole);
            throw new ConflictException(string.Format("Not allowed to delete the {0} Role as it is currently assigned to users.", role.Name));
        }

        // Mirror the user-assignment guard for providers that pin this role as
        // their auto-registration default. The DB FK (ON DELETE NO ACTION) is the
        // hard backstop; this check turns the raw constraint violation into a
        // clear, actionable error before we touch the database.
        var dependentProviders = await _defaultRoleChecker.CountProvidersUsingRole(role.Id, CancellationToken.None);
        if (dependentProviders > 0)
        {
            _logger.LogWarning("Role delete denied: {RoleName} ({RoleId}) is the default registration role for {ProviderCount} identity provider(s).",
                role.Name, role.Id, dependentProviders);
            throw new ConflictException(string.Format(
                "Not allowed to delete the {0} Role as it is the default registration role for {1} identity provider(s).",
                role.Name, dependentProviders));
        }

        await _roleManager.DeleteAsync(role);

        await _events.PublishAsync(new ApplicationRoleDeletedEvent(role.Id, role.Name!, _dateTimeProvider.Now));

        _logger.LogInformation("Role {RoleName} ({RoleId}) deleted by user {UserId}.", role.Name, role.Id, _currentUser.GetUserId());

        return string.Format("Role {0} Deleted.", role.Name);
    }

    /// <summary>Handles specific validation errors if they exist.</summary>
    /// <param name="result">The result.</param>
    /// <exception cref="Wayd.Common.Application.Exceptions.ValidationException"></exception>
    private void HandleValidationErrors(IdentityResult result)
    {
        if (result.Errors.Any(e => e.Code == "DuplicateRoleName"))
        {
            var duplicateRoleName = result.Errors.First(e => e.Code == "DuplicateRoleName");
            var failures = new List<ValidationFailure>()
            {
                new ValidationFailure("Name", duplicateRoleName.Description)
            };

            throw new ValidationException(failures);
        }
    }
}