using System.Security.Claims;

namespace Wayd.Common.Application.Identity.Users;

public interface IUserService : ITransientService
{
    Task<IReadOnlyList<UserDetailsDto>> SearchAsync(UserListFilter filter, CancellationToken cancellationToken);

    Task<bool> ExistsWithNameAsync(string name);

    Task<bool> ExistsWithEmailAsync(string email, string? exceptId = null);

    Task<bool> ExistsWithPhoneNumberAsync(string phoneNumber, string? exceptId = null);

    Task<List<UserDetailsDto>> GetListAsync(CancellationToken cancellationToken);

    Task<UserDetailsDto?> GetAsync(string userId, CancellationToken cancellationToken);

    Task<List<UserDetailsDto>> GetUsersWithRole(string roleId, CancellationToken cancellationToken);

    Task<int> GetUsersWithRoleCount(string roleId, CancellationToken cancellationToken);

    Task<int> GetCountAsync(CancellationToken cancellationToken);

    Task<string?> GetEmailAsync(string userId);

    Task<List<UserRoleDto>> GetRolesAsync(string userId, bool includeUnassigned, CancellationToken cancellationToken);

    Task<Result> AssignRolesAsync(AssignUserRolesCommand command, CancellationToken cancellationToken);

    Task<Result> ManageRoleUsersAsync(ManageRoleUsersCommand command, CancellationToken cancellationToken);

    Task<List<string>> GetPermissionsAsync(string userId, CancellationToken cancellationToken);

    Task<bool> HasPermissionAsync(string userId, string permission, CancellationToken cancellationToken = default);

    Task<Result> ActivateUserAsync(ActivateUserCommand command, CancellationToken cancellationToken);

    Task<Result> DeactivateUserAsync(DeactivateUserCommand command, CancellationToken cancellationToken);

    Task<(string Id, string? EmployeeId)> GetOrCreateFromPrincipalAsync(ClaimsPrincipal principal);

    /// <summary>
    /// Resolves a user from a validated GenericOidc token principal. Unlike the
    /// Entra path this does not provision new users — it handles the pending
    /// provider migration case and looks up existing identities. Returns null if
    /// the user cannot be resolved (new user, no pending migration).
    /// </summary>
    Task<(string Id, string? EmployeeId)?> ResolveFromGenericOidcPrincipalAsync(
        string providerName,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);

    Task UpdateAsync(UpdateUserCommand command, string userId);

    Task<Result<string>> CreateAsync(CreateUserCommand command, CancellationToken cancellationToken);

    Task<Result> ChangePasswordAsync(string userId, ChangePasswordCommand command);

    Task<Result> ResetPasswordAsync(ResetPasswordCommand command);

    Task<Result> UnlockUserAsync(string userId);

    Task<Result> UpdateMissingEmployeeIds(CancellationToken cancellationToken);

    Task<Result> SyncUsersFromEmployeeRecords(List<IExternalEmployee> externalEmployees, CancellationToken cancellationToken);

    Task<UserPreferencesDto> GetPreferences(string userId, CancellationToken cancellationToken);

    Task<Result> UpdatePreferences(string userId, UserPreferencesDto preferences, CancellationToken cancellationToken);

    Task<UserThemeConfigDto?> GetThemeConfig(string userId, CancellationToken cancellationToken);

    Task<Result> UpdateThemeConfig(string userId, UserThemeConfigDto? themeConfig, CancellationToken cancellationToken);

    /// <summary>
    /// Stages an Entra tenant migration for multiple users in one transaction. Users
    /// are re-validated server-side (still Entra, still on the source tenant, no
    /// pending migration) so a stale client list can't stage the wrong users; the
    /// result reports which were staged and which were skipped.
    /// </summary>
    Task<Result<BulkTenantMigrationResult>> StageBulkTenantMigration(StageBulkTenantMigrationCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Lists users bound to the given provider via an active identity on the given
    /// source tenant, excluding any with a migration already pending — the selectable
    /// candidates for a bulk migration off that tenant.
    /// </summary>
    Task<Result<IReadOnlyList<TenantMigrationCandidateDto>>> GetTenantMigrationCandidates(Guid providerId, string sourceTenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists users on the given provider with a staged-but-incomplete tenant migration.
    /// </summary>
    Task<Result<IReadOnlyList<PendingTenantMigrationDto>>> GetPendingTenantMigrations(Guid providerId, CancellationToken cancellationToken);

    // Staging a tenant migration is now a bulk, provider-page action
    // (StageBulkTenantMigration). Cancelling a single user's pending migration remains
    // available — there is no bulk-cancel equivalent.
    Task<Result> CancelTenantMigration(string userId, CancellationToken cancellationToken);

    Task<Result> StageProviderMigration(StageProviderMigrationCommand command, CancellationToken cancellationToken);

    Task<Result> CancelProviderMigration(string userId, CancellationToken cancellationToken);

    Task<Result> ConvertToLocalAccount(ConvertToLocalAccountCommand command, CancellationToken cancellationToken);

    Task<List<UserIdentityDto>> GetIdentityHistory(string userId, CancellationToken cancellationToken);
}