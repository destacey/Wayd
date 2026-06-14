using System.Security.Claims;
using Ardalis.GuardClauses;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;
using Wayd.Common.Application.Employees.Queries;
using Wayd.Common.Application.Identity;
using Wayd.Common.Extensions;
using NotFoundException = Wayd.Common.Application.Exceptions.NotFoundException;

namespace Wayd.Infrastructure.Identity;

internal partial class UserService
{
    /// <summary>
    /// Used when authenticating with Microsoft Entra ID.
    ///
    /// Resolves the user via the <c>UserIdentity</c> table, keyed by
    /// (Provider, ProviderTenantId, ProviderSubject) = (MicrosoftEntraId, tid, oid).
    /// If no active identity matches and exactly one identity matches (MicrosoftEntraId, NULL, oid),
    /// that row's ProviderTenantId is populated from the token — the one-time upgrade
    /// path for users backfilled before tenant was persisted.
    /// If still no match and a user has a staged tenant migration whose target matches
    /// the token's tenant, that user's active identity is rebound to (tid, oid) inside
    /// a transaction (see <see cref="TryApplyPendingTenantMigration"/>).
    /// If still no match, the user is created and a new UserIdentity row is inserted.
    /// </summary>
    public async Task<(string Id, string? EmployeeId)> GetOrCreateFromPrincipalAsync(ClaimsPrincipal principal)
    {
        string? objectId = principal.GetObjectId();
        if (string.IsNullOrWhiteSpace(objectId))
        {
            _logger.LogError("Invalid principal objectId");
            throw new InternalServerException("Invalid objectId");
        }

        string? tenantId = principal.GetTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            _logger.LogError("Invalid principal tenantId");
            throw new InternalServerException("Invalid tenantId");
        }

        // UPN is the signed-in-token identifier we trust for matching a staged
        // migration. Falls back to the email claim if absent (some tokens don't
        // emit UPN). Null means no migration rebind will be attempted.
        string? upn = principal.FindFirstValue(ClaimTypes.Upn) ?? principal.FindFirstValue(ClaimTypes.Email);

        var isFirstUser = !await _userManager.Users.AnyAsync();

        // The Entra provider's UserIdentity.Provider key is the well-known constant,
        // which is also its OidcProvider.Name. Load its registration policy so the
        // create path can honor the admin's auto-registration / employee-record /
        // default-role settings. First-user bootstrap ignores the policy entirely
        // (see below) — a fresh install must always be able to create its admin.
        var policy = await ResolveRegistrationPolicy(LoginProviders.MicrosoftEntraId);

        var user = await ResolveUserByEntraIdentityAsync(tenantId, objectId, upn)
            ?? await CreateOrUpdateFromPrincipalAsync(principal, isFirstUser, policy);

        if (isFirstUser)
        {
            await _userManager.AddToRoleAsync(user, ApplicationRoles.Admin);
            await _events.PublishAsync(new ApplicationUserUpdatedEvent(user.Id, _dateTimeProvider.Now, true));
        }
        else
        {
            var roles = await _userManager.GetRolesAsync(user);
            if (roles is null || !roles.Any())
            {
                await _userManager.AddToRoleAsync(user, await ResolveDefaultRoleName(policy));
                await _events.PublishAsync(new ApplicationUserUpdatedEvent(user.Id, _dateTimeProvider.Now, true));
            }
        }

        return (user.Id, user.EmployeeId?.ToString());
    }

    /// <summary>
    /// Resolves the provider's <see cref="RegistrationPolicy"/> from the registry.
    /// A missing row is treated as a disabled policy: the token was already
    /// validated against a configured provider, so a cache miss here is effectively
    /// impossible, and denying is the safe default if it ever occurs.
    /// </summary>
    private async Task<RegistrationPolicy> ResolveRegistrationPolicy(string providerName)
    {
        var provider = await _oidcProviderRegistry.GetByName(providerName, CancellationToken.None);
        return provider?.RegistrationPolicy ?? RegistrationPolicy.Disabled();
    }

    /// <summary>
    /// Resolves the role name to assign to an auto-created user. An enabled policy
    /// always names a role (it's required), so this looks it up directly. The FK on
    /// <c>DefaultRoleId</c> plus the delete guard in <c>RoleService.Delete</c> keep the
    /// reference valid, so a missing role here is an invariant violation — there is no
    /// fallback. Auto-registration cannot proceed without a valid configured role, so
    /// this throws rather than inventing one.
    /// </summary>
    private async Task<string> ResolveDefaultRoleName(RegistrationPolicy policy)
    {
        var roleId = policy.DefaultRoleId
            ?? throw new InvalidOperationException("Enabled registration policy is missing its default role.");

        var role = await _roleManager.FindByIdAsync(roleId);
        if (role?.Name is null)
        {
            _logger.LogError(
                "Default role {RoleId} configured for auto-registration no longer exists; cannot provision user.",
                roleId);
            throw new InternalServerException("The configured registration role no longer exists. Contact an administrator.");
        }

        return role.Name;
    }

    private async Task<ApplicationUser?> ResolveUserByEntraIdentityAsync(string tenantId, string objectId, string? upn)
    {
        var identity = await _userIdentityStore.FindActive(LoginProviders.MicrosoftEntraId, tenantId, objectId);
        if (identity is not null)
        {
            return identity.User;
        }

        // One-time upgrade path: rows backfilled from ApplicationUser.ObjectId have
        // ProviderTenantId = NULL. On the user's next login, populate tenant from the
        // token. If more than one such row matches the subject (extremely unlikely
        // oid collision across tenants), bail out — do not auto-link.
        var pending = await _userIdentityStore.FindActiveByNullTenant(LoginProviders.MicrosoftEntraId, objectId);

        if (pending.Count > 1)
        {
            _logger.LogError(
                "Ambiguous tenant upgrade for Entra subject {ObjectId}: {Count} active rows with NULL tenant. Manual resolution required.",
                objectId, pending.Count);
            throw new InternalServerException("Identity resolution ambiguous. Contact an administrator.");
        }

        if (pending.Count == 1)
        {
            var row = pending[0];

            // Conditional UPDATE guards against a concurrent login racing us. If another
            // login populated the tenant first, TryPopulateTenant returns false and we
            // re-resolve — the other login may have set a tenant that matches ours (fine,
            // both users converge on the same row) or a different one (our token has no
            // matching active row, so we fall into the create path).
            var won = await _userIdentityStore.TryPopulateTenant(row.Id, tenantId);
            if (!won)
            {
                _logger.LogInformation(
                    "Tenant upgrade for Entra subject {ObjectId} lost the race to a concurrent login; re-resolving.",
                    objectId);
                return await _userIdentityStore.FindActive(LoginProviders.MicrosoftEntraId, tenantId, objectId)
                    is { } winnerIdentity
                    ? winnerIdentity.User
                    : null;
            }

            return row.User;
        }

        // No active identity for (tid, oid) and no NULL-tenant backfill row to upgrade.
        // Last resort before falling through to create: an admin may have staged a
        // tenant migration for this user, in which case we rebind their existing
        // identity to (tid, oid) instead of creating a duplicate user.
        return await TryApplyPendingTenantMigration(tenantId, objectId, upn);
    }

    /// <summary>
    /// If a user has <c>PendingMigrationTenantId == tenantId</c> and a UPN matching the
    /// signed-in token, atomically:
    ///   1. Deactivate their active <see cref="UserIdentity"/> row (UnlinkReason = TenantMigration).
    ///   2. Insert a new active row with (provider, tenantId, objectId).
    ///   3. Clear PendingMigrationTenantId.
    /// Returns the user on success; null if no migration was staged or no match found.
    /// The user's <c>Id</c> is preserved, so all downstream FKs remain valid.
    /// </summary>
    /// <remarks>
    /// Internal (rather than private) so unit tests can exercise the rebind decision
    /// directly, without driving the surrounding <see cref="GetOrCreateFromPrincipalAsync"/>
    /// which depends on the Graph client.
    /// </remarks>
    internal async Task<ApplicationUser?> TryApplyPendingTenantMigration(string tenantId, string objectId, string? upn)
    {
        if (string.IsNullOrWhiteSpace(upn))
        {
            return null;
        }

        // Match on UPN (case-insensitive) to scope the lookup. A bare email match
        // would risk rebinding the wrong user if two ApplicationUser rows share an
        // email — match by both UserName (UPN-shaped) and NormalizedEmail to cover
        // tokens whose UPN is the email and accounts whose Email isn't UPN-shaped.
        var normalized = upn.ToUpperInvariant();
        var candidate = await _userManager.Users
            .FirstOrDefaultAsync(u =>
                u.PendingMigrationTenantId == tenantId &&
                u.LoginProvider == LoginProviders.MicrosoftEntraId &&
                (u.NormalizedUserName == normalized || u.NormalizedEmail == normalized));

        if (candidate is null)
        {
            return null;
        }

        // Run the deactivate + insert + flag-clear together. Failure of any step
        // rolls back the others — without the transaction we could deactivate the
        // old identity then fail to insert the new one (locking the user out), or
        // commit identity changes but leave PendingMigrationTenantId set (which would
        // cause the rebind path to re-trigger on the next login and produce a
        // duplicate-active-row error).
        await _userIdentityStore.ExecuteInTransaction(async ct =>
        {
            await _userIdentityStore.DeactivateAllActive(candidate.Id, _dateTimeProvider.Now, UserIdentityUnlinkReasons.TenantMigration, ct);

            await _userIdentityStore.Add(new UserIdentity
            {
                Id = Guid.NewGuid(),
                UserId = candidate.Id,
                Provider = LoginProviders.MicrosoftEntraId,
                ProviderTenantId = tenantId,
                ProviderSubject = objectId,
                IsActive = true,
                LinkedAt = _dateTimeProvider.Now,
            }, ct);

            candidate.PendingMigrationTenantId = null;
            var updateResult = await _userManager.UpdateAsync(candidate);
            if (!updateResult.Succeeded)
            {
                // Throw so ExecuteInTransaction rolls back — the deactivate + insert
                // above must not commit if we couldn't clear the flag.
                var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                throw new InternalServerException(
                    $"Failed to clear pending migration flag for user {candidate.Id}: {errors}");
            }
        });

        _logger.LogInformation(
            "Tenant migration completed for user {UserId}: rebound to tenant {TenantId} (subject {ObjectId}).",
            candidate.Id, tenantId, objectId);

        await _events.PublishAsync(new ApplicationUserUpdatedEvent(candidate.Id, _dateTimeProvider.Now));

        return candidate;
    }

    /// <summary>
    /// Resolves a user from a validated GenericOidc token. Handles the pending
    /// provider migration case and finds existing identities by (provider, sub).
    /// Does not create new users — GenericOidc user provisioning is a follow-up.
    /// </summary>
    public async Task<(string Id, string? EmployeeId)?> ResolveFromGenericOidcPrincipalAsync(
        string providerName,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        // Standard OIDC subject claim
        var subject = principal.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subject))
        {
            _logger.LogError("GenericOidc token from provider {Provider} is missing the 'sub' claim.", providerName);
            throw new InternalServerException("Token missing subject claim.");
        }

        // Email for migration matching; standard OIDC email claim
        var email = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("email");

        // Reject an explicitly unverified email — some providers issue tokens
        // with email_verified=false for addresses the user hasn't confirmed.
        // Treating such a claim as a trusted account binding would let an attacker
        // register with someone else's email and take over any pending migration
        // staged for that address. Absent email_verified is accepted (well-behaved
        // providers that always verify simply omit the claim).
        var emailVerifiedClaim = principal.FindFirstValue("email_verified");
        if (emailVerifiedClaim is not null &&
            emailVerifiedClaim.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Provider migration skipped for provider {Provider}: email_verified=false on token.",
                providerName);
            email = null; // treat as no email — rebind won't fire
        }

        // GenericOidc providers are single-tenant per row — no tid claim; tenant
        // is implicit from the provider configuration itself. Store null to keep
        // the schema consistent with how single-tenant rows are represented.
        var identity = await _userIdentityStore.FindActive(providerName, tenantId: null, subject);
        if (identity is not null)
        {
            return (identity.User!.Id, identity.User.EmployeeId?.ToString());
        }

        // No active identity for this provider + subject. Check for a staged
        // provider migration — an admin may have marked this user to switch to
        // this provider, in which case we rebind on this first login.
        var migrated = await TryApplyPendingProviderMigration(providerName, providerTenantId: null, subject, email);
        if (migrated is not null)
        {
            return (migrated.Id, migrated.EmployeeId?.ToString());
        }

        // User not found — new-user provisioning for GenericOidc is not yet
        // implemented. Return null so the caller can reject with an appropriate
        // message rather than creating a partial user record.
        return null;
    }

    /// <summary>
    /// If a user has <c>PendingMigrationProviderId == providerName</c> and an email
    /// matching the signed-in token, atomically:
    ///   1. Deactivate their active <see cref="UserIdentity"/> row (UnlinkReason = ProviderRelinked).
    ///   2. Insert a new active row with (providerName, providerTenantId, subject).
    ///   3. Update <c>LoginProvider</c> to the new provider and clear <c>PendingMigrationProviderId</c>.
    /// Returns the user on success; null if no migration was staged or no match found.
    /// The user's <c>Id</c> is preserved, so all downstream FKs remain valid.
    /// </summary>
    /// <remarks>
    /// Internal (rather than private) so unit tests can exercise the rebind decision
    /// directly.
    /// </remarks>
    internal async Task<ApplicationUser?> TryApplyPendingProviderMigration(
        string providerName,
        string? providerTenantId,
        string subject,
        string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        // Match on email (case-insensitive) to scope the lookup. The admin staged
        // the migration on a specific user; email is the cross-provider identifier
        // we can reliably compare at login time.
        var normalized = email.ToUpperInvariant();
        var candidate = await _userManager.Users
            .FirstOrDefaultAsync(u =>
                u.PendingMigrationProviderId == providerName &&
                u.LoginProvider != LoginProviders.Wayd &&
                (u.NormalizedUserName == normalized || u.NormalizedEmail == normalized));

        if (candidate is null)
        {
            return null;
        }

        var now = _dateTimeProvider.Now;

        await _userIdentityStore.ExecuteInTransaction(async ct =>
        {
            await _userIdentityStore.DeactivateAllActive(candidate.Id, now, UserIdentityUnlinkReasons.ProviderRelinked, ct);

            await _userIdentityStore.Add(new UserIdentity
            {
                Id = Guid.NewGuid(),
                UserId = candidate.Id,
                Provider = providerName,
                ProviderTenantId = providerTenantId,
                ProviderSubject = subject,
                IsActive = true,
                LinkedAt = now,
            }, ct);

            candidate.LoginProvider = providerName;
            candidate.PendingMigrationProviderId = null;
            var updateResult = await _userManager.UpdateAsync(candidate);
            if (!updateResult.Succeeded)
            {
                var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                throw new InternalServerException(
                    $"Failed to apply pending provider migration for user {candidate.Id}: {errors}");
            }
        });

        // If the user was previously a local account, clear the password hash now
        // that the Wayd identity is deactivated. The hash is unreachable at login
        // (TokenService gates on an active Wayd identity), but removing it makes
        // the intent explicit and prevents a future bug if that gate ever moves.
        if (candidate.PasswordHash is not null)
        {
            await _userManager.RemovePasswordAsync(candidate);
        }

        _logger.LogInformation(
            "Provider migration completed for user {UserId}: rebound to provider {Provider} (subject {Subject}).",
            candidate.Id, providerName, subject);

        await _events.PublishAsync(new ApplicationUserUpdatedEvent(candidate.Id, _dateTimeProvider.Now));

        return candidate;
    }

    private async Task<ApplicationUser> CreateOrUpdateFromPrincipalAsync(ClaimsPrincipal principal, bool isFirstUser, RegistrationPolicy policy)
    {
        string principalObjectId = principal.GetObjectId() ?? throw new InternalServerException("Principal ObjectId is missing or null.");

        string? email = principal.FindFirstValue(ClaimTypes.Upn);
        string? username = principal.GetDisplayName();

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(username))
        {
            _logger.LogError("Username {Username} or Email {Email} not valid", username, email);
            throw new InternalServerException("Username or Email not valid.");
        }

        var user = await _userManager.FindByNameAsync(username);
        if (user is not null && await _userIdentityStore.ExistsActive(user.Id, LoginProviders.MicrosoftEntraId))
        {
            _logger.LogError("Username or Email {Username} not valid", username);
            throw new InternalServerException($"Username {username} is already taken.");
        }

        if (user is null)
        {
            user = await _userManager.FindByEmailAsync(email);
            if (user is not null && await _userIdentityStore.ExistsActive(user.Id, LoginProviders.MicrosoftEntraId))
            {
                _logger.LogError("Email {email} is already taken.", email);
                throw new InternalServerException($"Email {email} is already taken.");
            }
        }

        string principalTenantId = principal.GetTenantId() ?? throw new InternalServerException("Principal TenantId is missing or null.");

        IdentityResult? result;
        if (user is not null)
        {
            // Existing user — linking them to Entra. The UserIdentity row is inserted
            // downstream by EnsureEntraIdentityRowAsync, which also deactivates any
            // prior active identity (e.g., a Wayd local identity being migrated to SSO).
            result = await _userManager.UpdateAsync(user);

            await _events.PublishAsync(new ApplicationUserUpdatedEvent(user.Id, _dateTimeProvider.Now));
        }
        else
        {
            // The first-ever user bootstraps the system and bypasses all policy
            // gates — a fresh install must always be able to create its admin even
            // with auto-registration disabled or no employee records seeded yet.
            if (!isFirstUser && !policy.AllowAutoRegistration)
            {
                _logger.LogWarning("Registration denied for user {Username} (Email: {Email}). Auto-registration is disabled for this provider.",
                    username, email);
                throw new ForbiddenException("Registration is disabled for this identity provider. Contact an administrator.");
            }

            var employeeId = await GetEmployeeIdByEmail(email);

            // RequireEmployeeRecord is null only on a disabled policy, which the
            // gate above already rejected for non-first users — so `== true` here
            // safely means "auto-reg on and the employee gate is set".
            if (!employeeId.HasValue && !isFirstUser && policy.RequireEmployeeRecord == true)
            {
                _logger.LogWarning("Registration denied for user {Username} (Email: {Email}). No matching employee record found.",
                    username, email);
                throw new ForbiddenException("Registration is restricted to users with an employee record in Wayd.");
            }

            user = new ApplicationUser
            {
                FirstName = Guard.Against.NullOrWhiteSpace(principal.FindFirstValue(ClaimTypes.GivenName), nameof(ClaimTypes.GivenName)),
                LastName = Guard.Against.NullOrWhiteSpace(principal.FindFirstValue(ClaimTypes.Surname), nameof(ClaimTypes.Surname)),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                UserName = username,
                NormalizedUserName = username.ToUpperInvariant(),
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                IsActive = true,
                EmployeeId = employeeId,
                LoginProvider = LoginProviders.MicrosoftEntraId,
            };
            result = await _userManager.CreateAsync(user);

            await _events.PublishAsync(new ApplicationUserCreatedEvent(user.Id, _dateTimeProvider.Now));
        }

        if (!result.Succeeded)
        {
            _logger.LogError("Error creating user from principal: {Errors}", result.Errors.Select(e => e.Description));
            throw new InternalServerException("Validation Errors Occurred.");
        }

        await EnsureEntraIdentityRowAsync(user.Id, principalTenantId, principalObjectId);

        return user;
    }

    private async Task EnsureEntraIdentityRowAsync(string userId, string tenantId, string objectId)
    {
        // Idempotent for the new-user case, and enforces the "exactly one active
        // identity per user" invariant when an existing user is being linked to
        // Entra (e.g., a Wayd-local user moving to SSO). Any prior active row —
        // including a Wayd identity — is marked inactive with reason ProviderRelinked.
        var exists = await _userIdentityStore.ExistsActive(userId, LoginProviders.MicrosoftEntraId);
        if (exists)
        {
            return;
        }

        await _userIdentityStore.DeactivateAllActive(userId, _dateTimeProvider.Now, UserIdentityUnlinkReasons.ProviderRelinked);

        await _userIdentityStore.Add(new UserIdentity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Provider = LoginProviders.MicrosoftEntraId,
            ProviderTenantId = tenantId,
            ProviderSubject = objectId,
            IsActive = true,
            LinkedAt = _dateTimeProvider.Now,
        });
    }

    public async Task<Result<string>> CreateAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        // Use email as the username for admin-created users
        var user = new ApplicationUser
        {
            FirstName = command.FirstName,
            LastName = command.LastName,
            Email = command.Email,
            NormalizedEmail = command.Email.ToUpperInvariant(),
            UserName = command.Email,
            NormalizedUserName = command.Email.ToUpperInvariant(),
            EmailConfirmed = true,
            PhoneNumberConfirmed = true,
            IsActive = true,
            LockoutEnabled = true,
            EmployeeId = command.EmployeeId,
            PhoneNumber = command.PhoneNumber,
            LoginProvider = command.LoginProvider,
            MustChangePassword = command.LoginProvider == LoginProviders.Wayd && command.MustChangePassword,
        };

        // User creation, role assignment, and the Wayd identity row must land
        // together. Partial failure — user exists with no active Wayd identity —
        // would leave a local user who cannot log in (TokenService gates on the
        // identity row). Run the whole thing in a transaction so any failure rolls
        // back the user.
        IdentityResult? result = null;
        try
        {
            await _userIdentityStore.ExecuteInTransaction(async ct =>
            {
                result = command.LoginProvider == LoginProviders.Wayd
                    ? await _userManager.CreateAsync(user, command.Password!)
                    : await _userManager.CreateAsync(user);

                if (!result.Succeeded)
                {
                    // Throw to force rollback. Outer catch swallows this sentinel;
                    // `result` carries the validation errors back to the caller.
                    throw new UserCreationRollbackException();
                }

                result = await _userManager.AddToRolesAsync(user, command.RoleNames);
                if (!result.Succeeded)
                {
                    // Same rollback contract as the create failure above: throw the
                    // sentinel so the transaction unwinds and the user isn't left
                    // persisted without roles. `result` carries the errors back.
                    throw new UserCreationRollbackException();
                }

                // Wayd (local) users get an identity row immediately, keyed by the
                // stable ApplicationUser.Id (usernames are mutable). Entra users
                // created by an admin have no oid/tid yet — the row is inserted on
                // their first SSO login via EnsureEntraIdentityRowAsync.
                if (command.LoginProvider == LoginProviders.Wayd)
                {
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
                }
            }, cancellationToken);
        }
        catch (UserCreationRollbackException)
        {
            // Expected when UserManager.CreateAsync returned a failing IdentityResult
            // inside the transaction. Fall through — the block below formats the
            // error response from `result`.
        }

        if (result is null || !result.Succeeded)
        {
            var errors = result is null
                ? "User creation failed."
                : string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError("Error creating user: {Errors}", errors);
            return Result.Failure<string>(errors);
        }

        // Publish after commit so subscribers don't see events for rolled-back users.
        await _events.PublishAsync(new ApplicationUserCreatedEvent(user.Id, _dateTimeProvider.Now));

        _logger.LogInformation("User {UserId} created successfully.", user.Id);
        return Result.Success(user.Id);
    }

    private sealed class UserCreationRollbackException : Exception { }

    public async Task UpdateAsync(UpdateUserCommand command, string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            _logger.LogError("User with id {UserId} not found.", userId);
            throw new NotFoundException("User Not Found.");
        }

        user.FirstName = command.FirstName;
        user.LastName = command.LastName;
        user.PhoneNumber = command.PhoneNumber;
        user.EmployeeId = command.EmployeeId;

        if (!string.Equals(user.Email, command.Email, StringComparison.OrdinalIgnoreCase))
        {
            user.Email = command.Email;
            user.NormalizedEmail = command.Email.ToUpperInvariant();
            user.UserName = command.Email;
            user.NormalizedUserName = command.Email.ToUpperInvariant();
        }

        string? phoneNumber = await _userManager.GetPhoneNumberAsync(user);
        if (command.PhoneNumber != phoneNumber)
            await _userManager.SetPhoneNumberAsync(user, command.PhoneNumber);

        var result = await _userManager.UpdateAsync(user);

        await _signInManager.RefreshSignInAsync(user);

        await _events.PublishAsync(new ApplicationUserUpdatedEvent(user.Id, _dateTimeProvider.Now));

        if (!result.Succeeded)
        {
            _logger.LogError("Error updating user: {Errors}", result.Errors.Select(e => e.Description));
            throw new InternalServerException("Update profile failed");
        }
    }

    public async Task<Result> ChangePasswordAsync(string userId, ChangePasswordCommand command)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            _logger.LogError("User with id {UserId} not found.", userId);
            throw new NotFoundException("User Not Found.");
        }

        if (user.LoginProvider != LoginProviders.Wayd)
        {
            return Result.Failure("Password change is only available for local accounts.");
        }

        var result = await _userManager.ChangePasswordAsync(user, command.CurrentPassword, command.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("Password change failed for user {UserId}: {Errors}", userId, errors);
            return Result.Failure(errors);
        }

        if (user.MustChangePassword)
        {
            user.MustChangePassword = false;
            await _userManager.UpdateAsync(user);
        }

        _logger.LogInformation("Password changed successfully for user {UserId}.", userId);
        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordCommand command)
    {
        var user = await _userManager.FindByIdAsync(command.UserId);
        if (user is null)
        {
            _logger.LogError("User with id {UserId} not found.", command.UserId);
            throw new NotFoundException("User Not Found.");
        }

        if (user.LoginProvider != LoginProviders.Wayd)
        {
            return Result.Failure("Password reset is only available for local accounts.");
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, command.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("Password reset failed for user {UserId}: {Errors}", command.UserId, errors);
            return Result.Failure(errors);
        }

        user.MustChangePassword = true;
        await _userManager.UpdateAsync(user);

        if (await _userManager.IsLockedOutAsync(user))
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);
            _logger.LogInformation("Lockout cleared for user {UserId} during password reset.", command.UserId);
        }

        _logger.LogInformation("Password reset successfully for user {UserId}.", command.UserId);
        return Result.Success();
    }

    public async Task<Result> UnlockUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            _logger.LogError("User with id {UserId} not found.", userId);
            throw new NotFoundException("User Not Found.");
        }

        if (!await _userManager.IsLockedOutAsync(user))
        {
            return Result.Failure("User is not currently locked out.");
        }

        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);

        _logger.LogInformation("User {UserId} unlocked by admin.", userId);
        return Result.Success();
    }

    public async Task<Result> UpdateMissingEmployeeIds(CancellationToken cancellationToken)
    {
        var users = await _userManager.Users.Where(u => !u.EmployeeId.HasValue && !string.IsNullOrEmpty(u.Email)).ToListAsync(cancellationToken);
        if (users.Count == 0)
            return Result.Success();

        // Email is the cross-source-stable User↔Employee link key. The active PeopleSync
        // connector owns Employee.Email; the user's email is set by registration and updated by
        // SyncUsersFromEmployeeRecords. Case-insensitive match.
        var employees = await _db.Employees
            .Where(e => !e.IsDeleted)
            .Select(e => new { e.Id, Email = e.Email.Value })
            .ToListAsync(cancellationToken);
        var employeeIdByEmail = employees
            .GroupBy(e => e.Email, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last().Id, StringComparer.OrdinalIgnoreCase);

        foreach (var user in users)
        {
            if (string.IsNullOrEmpty(user.Email)) continue;
            if (!employeeIdByEmail.TryGetValue(user.Email, out var employeeId)) continue;

            user.EmployeeId = employeeId;
            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                await _events.PublishAsync(new ApplicationUserUpdatedEvent(user.Id, _dateTimeProvider.Now));
            }
            else
            {
                _logger.LogError("Error updating employeeId on user {UserId}: {Errors}", user.Id, result.Errors.Select(e => e.Description));
            }
        }

        return Result.Success();
    }

    public async Task<Result> SyncUsersFromEmployeeRecords(List<IExternalEmployee> externalEmployees, CancellationToken cancellationToken)
    {
        // Match users to the external payload by email. This works regardless of which PeopleSync
        // connector is currently active (Entra, Workday, future). Users whose email isn't in the
        // payload are left untouched — separate from the Employee deactivation pass.
        var users = await _userManager.Users
            .Where(u => !string.IsNullOrEmpty(u.Email))
            .ToListAsync(cancellationToken);

        _logger.LogDebug("{UserCount} users found.", users.Count);
        if (users.Count == 0)
            return Result.Success();

        // Pre-index by email (case-insensitive) for O(1) lookup.
        var employeesByEmail = externalEmployees
            .GroupBy(e => e.Email.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

        foreach (var user in users)
        {
            if (string.IsNullOrEmpty(user.Email)) continue;

            var employee = employeesByEmail.GetValueOrDefault(user.Email);
            if (employee is null)
            {
                // Not an error — just means this user isn't in this sync's payload.
                continue;
            }

            user.FirstName = employee.Name.FirstName;
            user.LastName = employee.Name.LastName;
            user.Email = employee.Email;
            user.IsActive = employee.IsActive;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                _logger.LogInformation("User {UserId} updated.", user.Id);
                await _events.PublishAsync(new ApplicationUserUpdatedEvent(user.Id, _dateTimeProvider.Now));
            }
            else
            {
                _logger.LogError("Error updating user {UserId}: {Errors}", user.Id, result.Errors.Select(e => e.Description));
            }
        }

        return Result.Success();
    }

    private async Task<Guid?> GetEmployeeIdByEmail(string email)
    {
        // User-to-Employee linkage is keyed on email regardless of which PeopleSync connector
        // is currently active. EmployeeNumber's value depends on the source's preferences
        // (Entra User.Id, Workday Employee_ID, etc.) and isn't a stable cross-source key.
        var employeeId = await _sender.Send(new GetEmployeeByEmailQuery(email));
        if (employeeId.IsNullEmptyOrDefault())
        {
            employeeId = null;
            _logger.LogWarning("Employee with email {Email} not found.", email);
        }

        return employeeId;
    }
}
