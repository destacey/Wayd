using CSharpFunctionalExtensions;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Identity;
using Wayd.Common.Application.Identity.OidcProviders;

namespace Wayd.Infrastructure.Identity;

internal partial class UserService(
    ILogger<UserService> logger,
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    WaydDbContext db,
    IEventPublisher events,
    ISender sender,
    IDateTimeProvider dateTimeProvider,
    ICurrentUser currentUser,
    IUserIdentityStore userIdentityStore,
    IOidcProviderRegistry oidcProviderRegistry) : IUserService
{
    private readonly ILogger<UserService> _logger = logger;
    private readonly SignInManager<ApplicationUser> _signInManager = signInManager;
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly RoleManager<ApplicationRole> _roleManager = roleManager;
    private readonly WaydDbContext _db = db;
    private readonly IEventPublisher _events = events;
    private readonly ISender _sender = sender;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IUserIdentityStore _userIdentityStore = userIdentityStore;
    private readonly IOidcProviderRegistry _oidcProviderRegistry = oidcProviderRegistry;

    public async Task<IReadOnlyList<UserDetailsDto>> SearchAsync(UserListFilter filter, CancellationToken cancellationToken)
    {
        return await _db.Users
            .Where(u => u.IsActive == filter.IsActive)
            .ProjectToType<UserDetailsDto>()
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsWithNameAsync(string name)
    {
        return await _userManager.FindByNameAsync(name) is not null;
    }

    public async Task<bool> ExistsWithEmailAsync(string email, string? exceptId = null)
    {
        return await _userManager.FindByEmailAsync(email.Normalize()) is ApplicationUser user && user.Id != exceptId;
    }

    public async Task<bool> ExistsWithPhoneNumberAsync(string phoneNumber, string? exceptId = null)
    {
        return await _userManager.Users.FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber) is ApplicationUser user && user.Id != exceptId;
    }

    public async Task<List<UserDetailsDto>> GetListAsync(CancellationToken cancellationToken)
    {
        return await _db.Users
            .ProjectToType<UserDetailsDto>()
            .ToListAsync(cancellationToken);
    }

    public Task<int> GetCountAsync(CancellationToken cancellationToken) =>
        _userManager.Users.CountAsync(cancellationToken);

    public async Task<UserDetailsDto?> GetAsync(string userId, CancellationToken cancellationToken)
    {
        return await _db.Users
            .Where(u => u.Id == userId)
            .ProjectToType<UserDetailsDto>()
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<UserDetailsDto>> GetUsersWithRole(string roleId, CancellationToken cancellationToken)
    {
        return await _db.Users
            .Where(u => u.UserRoles.Any(ur => ur.RoleId == roleId))
            .ProjectToType<UserDetailsDto>()
            .ToListAsync(cancellationToken);
    }

    public Task<int> GetUsersWithRoleCount(string roleId, CancellationToken cancellationToken)
    {
        return _db.Users
            .Where(u => u.UserRoles.Any(ur => ur.RoleId == roleId))
            .CountAsync(cancellationToken);
    }

    public async Task<string?> GetEmailAsync(string userId)
    {
        var user = await _userManager.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
        {
            _logger.LogError("UserId {UserId} not found", userId);
            throw new NotFoundException("User Not Found.");
        }

        return user.Email;
    }

    public async Task<Result> ActivateUserAsync(ActivateUserCommand command, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users.Where(u => u.Id == command.UserId).FirstOrDefaultAsync(cancellationToken);
        if (user is null)
            throw new NotFoundException("User Not Found.");

        if (user.IsActive)
            return Result.Failure("User is already active.");

        user.IsActive = true;
        await _userManager.UpdateAsync(user);
        await _events.PublishAsync(new ApplicationUserActivatedEvent(user.Id, _dateTimeProvider.Now));

        _logger.LogInformation("User {UserId} activated.", command.UserId);
        return Result.Success();
    }

    public async Task<Result> DeactivateUserAsync(DeactivateUserCommand command, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users.Where(u => u.Id == command.UserId).FirstOrDefaultAsync(cancellationToken);
        if (user is null)
            throw new NotFoundException("User Not Found.");

        if (!user.IsActive)
            return Result.Failure("User is already inactive.");

        if (command.UserId == _currentUser.GetUserId())
            return Result.Failure("You cannot deactivate your own account.");

        user.IsActive = false;
        await _userManager.UpdateAsync(user);
        await _events.PublishAsync(new ApplicationUserDeactivatedEvent(user.Id, _dateTimeProvider.Now));

        _logger.LogInformation("User {UserId} deactivated.", command.UserId);
        return Result.Success();
    }

    public async Task<Result> CancelTenantMigration(string userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            throw new NotFoundException("User Not Found.");

        if (user.PendingMigrationTenantId is null)
        {
            // Idempotent: clearing a flag that's already clear is a no-op. Returning
            // success keeps the admin UI simple — they can click Cancel without
            // worrying about whether the migration just completed in another tab.
            return Result.Success();
        }

        user.PendingMigrationTenantId = null;
        user.PendingMigrationStagedAt = null;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError("Failed to cancel tenant migration for user {UserId}: {Errors}", user.Id, errors);
            return Result.Failure(errors);
        }

        await _events.PublishAsync(new ApplicationUserUpdatedEvent(user.Id, _dateTimeProvider.Now));

        _logger.LogInformation("Tenant migration canceled for user {UserId}.", user.Id);
        return Result.Success();
    }

    public async Task<Result<BulkTenantMigrationResult>> StageBulkTenantMigration(
        StageBulkTenantMigrationCommand command, CancellationToken cancellationToken)
    {
        var provider = await _db.OidcProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == command.ProviderId, cancellationToken);
        if (provider is null)
            return Result.Failure<BulkTenantMigrationResult>("Provider not found.");

        if (provider.ProviderType != OidcProviderType.MicrosoftEntraId)
            return Result.Failure<BulkTenantMigrationResult>("Tenant migration is only available for Microsoft Entra ID providers.");

        // Both tenants must be in the provider's allowlist — a target the validator
        // can never accept at login would strand every staged user.
        var allowed = provider.AllowedTenantIds ?? [];
        if (!allowed.Contains(command.SourceTenantId, StringComparer.OrdinalIgnoreCase))
            return Result.Failure<BulkTenantMigrationResult>("Source tenant is not configured on this provider.");
        if (!allowed.Contains(command.TargetTenantId, StringComparer.OrdinalIgnoreCase))
            return Result.Failure<BulkTenantMigrationResult>("Target tenant is not configured on this provider.");

        // Snapshot of who actually sits on the source tenant right now, keyed by user.
        // Re-validating here (rather than trusting the client's userIds) closes the
        // TOCTOU gap between the candidate query and submit — a user may have signed
        // in, migrated, or had a migration staged in the interim.
        var requested = command.UserIds.Distinct().ToList();
        var onSourceTenantSet = (await _userIdentityStore.GetActiveUserIdsOnTenant(
            LoginProviders.MicrosoftEntraId, command.SourceTenantId, requested, cancellationToken)).ToHashSet();

        var users = await _userManager.Users
            .Where(u => requested.Contains(u.Id))
            .ToListAsync(cancellationToken);
        var usersById = users.ToDictionary(u => u.Id);

        var staged = new List<string>();
        var skipped = new List<SkippedUser>();
        var now = _dateTimeProvider.Now;

        // One transaction for the whole batch: either every qualifying user is staged
        // or none are. A staging failure mid-loop must not leave a partial batch.
        await _userIdentityStore.ExecuteInTransaction(async ct =>
        {
            foreach (var userId in requested)
            {
                if (!usersById.TryGetValue(userId, out var user))
                {
                    skipped.Add(new SkippedUser(userId, "User not found."));
                    continue;
                }

                if (user.LoginProvider != LoginProviders.MicrosoftEntraId)
                {
                    skipped.Add(new SkippedUser(userId, "Not a Microsoft Entra ID user."));
                    continue;
                }

                if (user.PendingMigrationTenantId is not null)
                {
                    skipped.Add(new SkippedUser(userId, "A migration is already in progress."));
                    continue;
                }

                if (!onSourceTenantSet.Contains(userId))
                {
                    skipped.Add(new SkippedUser(userId, "User is no longer on the source tenant."));
                    continue;
                }

                user.PendingMigrationTenantId = command.TargetTenantId;
                user.PendingMigrationStagedAt = now;
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    // Throw to roll the whole batch back — a partial stage is worse than none.
                    var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                    throw new InternalServerException(
                        $"Failed to stage tenant migration for user {userId}: {errors}");
                }

                staged.Add(userId);
            }
        }, cancellationToken);

        // Publish after commit so subscribers don't react to a rolled-back batch.
        foreach (var userId in staged)
        {
            await _events.PublishAsync(new ApplicationUserUpdatedEvent(userId, now));
        }

        _logger.LogInformation(
            "Bulk tenant migration staged for provider {ProviderId}: {StagedCount} staged, {SkippedCount} skipped (source {SourceTenant} → target {TargetTenant}).",
            command.ProviderId, staged.Count, skipped.Count, command.SourceTenantId, command.TargetTenantId);

        return Result.Success(new BulkTenantMigrationResult(staged, skipped));
    }

    public async Task<Result<IReadOnlyList<TenantMigrationCandidateDto>>> GetTenantMigrationCandidates(
        Guid providerId, string sourceTenantId, CancellationToken cancellationToken)
    {
        var provider = await _db.OidcProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == providerId, cancellationToken);
        if (provider is null)
            return Result.Failure<IReadOnlyList<TenantMigrationCandidateDto>>("Provider not found.");

        if (provider.ProviderType != OidcProviderType.MicrosoftEntraId)
            return Result.Failure<IReadOnlyList<TenantMigrationCandidateDto>>("Tenant migration is only available for Microsoft Entra ID providers.");

        // Source must be a tenant this provider actually accepts — keeps the candidate
        // query consistent with StageBulkTenantMigration, which allowlists both tenants.
        if (!(provider.AllowedTenantIds ?? []).Contains(sourceTenantId, StringComparer.OrdinalIgnoreCase))
            return Result.Failure<IReadOnlyList<TenantMigrationCandidateDto>>("Source tenant is not configured on this provider.");

        // Users with an active Entra identity on the source tenant and no pending
        // migration. Joining through UserIdentities scopes to "currently on this
        // tenant" — a pre-provisioned user with zero active identities correctly
        // drops out (nothing to migrate from).
        var candidates = await _db.UserIdentities
            .AsNoTracking()
            .Where(ui => ui.IsActive
                && ui.Provider == LoginProviders.MicrosoftEntraId
                && ui.ProviderTenantId == sourceTenantId)
            .Join(
                _db.Users.Where(u => u.PendingMigrationTenantId == null),
                ui => ui.UserId,
                u => u.Id,
                (ui, u) => new TenantMigrationCandidateDto
                {
                    UserId = u.Id,
                    UserName = u.UserName,
                    Email = u.Email,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    IsActive = u.IsActive,
                })
            .OrderBy(c => c.UserName)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<TenantMigrationCandidateDto>>(candidates);
    }

    public async Task<Result<IReadOnlyList<PendingTenantMigrationDto>>> GetPendingTenantMigrations(
        Guid providerId, CancellationToken cancellationToken)
    {
        var provider = await _db.OidcProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == providerId, cancellationToken);
        if (provider is null)
            return Result.Failure<IReadOnlyList<PendingTenantMigrationDto>>("Provider not found.");

        if (provider.ProviderType != OidcProviderType.MicrosoftEntraId)
            return Result.Failure<IReadOnlyList<PendingTenantMigrationDto>>("Tenant migration is only available for Microsoft Entra ID providers.");

        // Users on this Entra provider with a staged tenant migration. The current
        // (source) tenant comes from the active identity row; correlated subquery so
        // a user with no active row still shows (source null) rather than disappearing.
        var pending = await _db.Users
            .Where(u => u.LoginProvider == LoginProviders.MicrosoftEntraId
                && u.PendingMigrationTenantId != null)
            .Select(u => new PendingTenantMigrationDto
            {
                UserId = u.Id,
                UserName = u.UserName,
                Email = u.Email,
                SourceTenantId = _db.UserIdentities
                    .Where(ui => ui.IsActive
                        && ui.UserId == u.Id
                        && ui.Provider == LoginProviders.MicrosoftEntraId)
                    .Select(ui => ui.ProviderTenantId)
                    .FirstOrDefault(),
                TargetTenantId = u.PendingMigrationTenantId!,
                StagedAt = u.PendingMigrationStagedAt,
            })
            .OrderBy(p => p.UserName)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<PendingTenantMigrationDto>>(pending);
    }

    public async Task<Result> StageProviderMigration(StageProviderMigrationCommand command, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
            throw new NotFoundException("User Not Found.");

        // Can't migrate to the same provider — that's a tenant migration (or a no-op)
        if (string.Equals(user.LoginProvider, command.TargetProviderId, StringComparison.OrdinalIgnoreCase))
            return Result.Failure("Target provider is the same as the user's current provider.");

        // Target provider must be a known, enabled OIDC provider
        var targetProvider = await _oidcProviderRegistry.GetByName(command.TargetProviderId, cancellationToken);
        if (targetProvider is null)
            return Result.Failure($"Provider '{command.TargetProviderId}' does not exist.");
        if (!targetProvider.IsEnabled)
            return Result.Failure($"Provider '{command.TargetProviderId}' is disabled.");

        if (!await _userIdentityStore.ExistsActive(user.Id, user.LoginProvider, cancellationToken))
            return Result.Failure("User has no active identity to migrate.");

        // Last-write-wins semantics match the tenant migration pattern.
        user.PendingMigrationProviderId = command.TargetProviderId;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError("Failed to stage provider migration for user {UserId}: {Errors}", user.Id, errors);
            return Result.Failure(errors);
        }

        await _events.PublishAsync(new ApplicationUserUpdatedEvent(user.Id, _dateTimeProvider.Now));

        _logger.LogInformation(
            "Provider migration staged for user {UserId}: target provider {ProviderId}.",
            user.Id, command.TargetProviderId);
        return Result.Success();
    }

    public async Task<Result> CancelProviderMigration(string userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            throw new NotFoundException("User Not Found.");

        if (user.PendingMigrationProviderId is null)
        {
            // Idempotent: same semantics as CancelTenantMigration
            return Result.Success();
        }

        user.PendingMigrationProviderId = null;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError("Failed to cancel provider migration for user {UserId}: {Errors}", user.Id, errors);
            return Result.Failure(errors);
        }

        await _events.PublishAsync(new ApplicationUserUpdatedEvent(user.Id, _dateTimeProvider.Now));

        _logger.LogInformation("Provider migration canceled for user {UserId}.", userId);
        return Result.Success();
    }

    public async Task<Result> ConvertToLocalAccount(ConvertToLocalAccountCommand command, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
            throw new NotFoundException("User Not Found.");

        if (user.LoginProvider == LoginProviders.Wayd)
            return Result.Failure("User is already a local account.");

        // Validate the password before entering the transaction so a bad password
        // returns a clean Result.Failure without touching the database.
        foreach (var validator in _userManager.PasswordValidators)
        {
            var validationResult = await validator.ValidateAsync(_userManager, user, command.NewPassword);
            if (!validationResult.Succeeded)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.Description));
                return Result.Failure(errors);
            }
        }

        // Run deactivate + add local identity + set password + update user in one
        // transaction. Any failure rolls back all steps — without this we could
        // deactivate the OIDC identity then fail to set the password, locking the
        // user out of both paths.
        await _userIdentityStore.ExecuteInTransaction(async ct =>
        {
            await _userIdentityStore.DeactivateAllActive(
                user.Id, _dateTimeProvider.Now, UserIdentityUnlinkReasons.ProviderRelinked, ct);

            await _userIdentityStore.Add(new UserIdentity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Provider = LoginProviders.Wayd,
                ProviderTenantId = null,
                ProviderSubject = user.Id,
                IsActive = true,
                LinkedAt = _dateTimeProvider.Now,
            }, ct);

            // Reset any existing password hash before setting the new one so
            // this works regardless of whether the user previously had a password
            // (e.g. was on Wayd, migrated to OIDC, now converting back).
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var passwordResult = await _userManager.ResetPasswordAsync(user, token, command.NewPassword);
            if (!passwordResult.Succeeded)
            {
                var errors = string.Join(", ", passwordResult.Errors.Select(e => e.Description));
                throw new InternalServerException(
                    $"Failed to set password for user {user.Id} during local conversion: {errors}");
            }

            user.LoginProvider = LoginProviders.Wayd;
            user.MustChangePassword = true;
            user.PendingMigrationProviderId = null;
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                throw new InternalServerException(
                    $"Failed to update user {user.Id} during local conversion: {errors}");
            }
        }, cancellationToken);

        await _events.PublishAsync(new ApplicationUserUpdatedEvent(user.Id, _dateTimeProvider.Now));

        _logger.LogInformation(
            "User {UserId} converted from {Provider} to a local account.",
            user.Id, user.LoginProvider);
        return Result.Success();
    }

    public async Task<List<UserIdentityDto>> GetIdentityHistory(string userId, CancellationToken cancellationToken)
    {
        var userExists = await _userManager.Users.AnyAsync(u => u.Id == userId, cancellationToken);
        if (!userExists)
            throw new NotFoundException("User Not Found.");

        return await _db.UserIdentities
            .Where(ui => ui.UserId == userId)
            .OrderByDescending(ui => ui.IsActive)
            .ThenByDescending(ui => ui.LinkedAt)
            .ProjectToType<UserIdentityDto>()
            .ToListAsync(cancellationToken);
    }
}