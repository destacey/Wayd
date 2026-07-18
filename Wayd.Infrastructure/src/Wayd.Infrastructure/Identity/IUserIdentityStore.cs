using NodaTime;

namespace Wayd.Infrastructure.Identity;

/// <summary>
/// Narrow seam over <see cref="UserIdentity"/> persistence. Introduced so identity-
/// write paths in <see cref="UserService"/> and <see cref="Auth.Local.TokenService"/>
/// can be unit-tested without spinning up a full <c>WaydDbContext</c>.
/// </summary>
// Public (not internal) because Wolverine's generated handler code news up the public
// DeleteOidcProviderCommandHandler, whose constructor takes this type — a public constructor cannot
// expose a less-accessible parameter type. This is the accepted encapsulation trade-off of the
// Wolverine migration (handlers must be public).
public interface IUserIdentityStore : IScopedService
{
    Task<UserIdentity?> FindActive(string provider, string? tenantId, string subject, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns up to two active rows matching (provider, subject) with NULL tenant.
    /// Used by the Entra null-tid upgrade path to detect ambiguous matches.
    /// </summary>
    Task<IReadOnlyList<UserIdentity>> FindActiveByNullTenant(string provider, string subject, CancellationToken cancellationToken = default);

    Task<bool> ExistsActive(string userId, string provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// From <paramref name="userIds"/>, returns the subset that currently have an
    /// active identity on (<paramref name="provider"/>, <paramref name="tenantId"/>).
    /// Used by bulk tenant migration to re-confirm — at submit time — which selected
    /// users are still on the source tenant, closing the gap since the candidate list
    /// was fetched.
    /// </summary>
    Task<IReadOnlyCollection<string>> GetActiveUserIdsOnTenant(
        string provider, string tenantId, IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts active identities for a given provider. Used when validating whether
    /// an <see cref="OidcProvider"/> row can be deleted — deletion is blocked while
    /// any user is still actively bound to the provider.
    /// </summary>
    Task<int> CountActiveByProvider(string provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a dictionary mapping <c>UserId</c> to the <c>ProviderSubject</c> of
    /// their active identity row for the given provider. Used by batch processes
    /// that need to correlate users to an external identifier (e.g., employee
    /// records keyed by the Entra <c>oid</c>).
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetActiveSubjectsByProvider(string provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks every active row for a user as inactive, setting <c>UnlinkedAt</c> and
    /// the given <c>unlinkReason</c>. Used to enforce the "exactly one active identity
    /// per user at rest" invariant when a new identity is being linked.
    /// </summary>
    Task<int> DeactivateAllActive(string userId, Instant unlinkedAt, string unlinkReason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Conditionally sets <c>ProviderTenantId</c> on a backfilled row — only if the
    /// row still has a NULL tenant. Returns true if this caller won the race, false
    /// if another concurrent caller already populated the tenant (in which case the
    /// caller must re-resolve rather than assume the row is theirs).
    /// </summary>
    Task<bool> TryPopulateTenant(Guid identityId, string tenantId, CancellationToken cancellationToken = default);

    Task Add(UserIdentity identity, CancellationToken cancellationToken = default);

    Task Update(UserIdentity identity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="action"/> inside a database transaction on the
    /// underlying <c>WaydDbContext</c>. Commits on success, rolls back on throw.
    /// Used to ensure that multi-step writes touching both <c>ApplicationUser</c>
    /// (via <c>UserManager</c>) and <c>UserIdentity</c> land atomically.
    /// </summary>
    Task ExecuteInTransaction(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}
