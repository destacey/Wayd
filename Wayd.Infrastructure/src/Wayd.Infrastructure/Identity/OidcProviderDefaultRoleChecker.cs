using Microsoft.EntityFrameworkCore;

namespace Wayd.Infrastructure.Identity;

internal sealed class OidcProviderDefaultRoleChecker(WaydDbContext db) : IOidcProviderDefaultRoleChecker
{
    private readonly WaydDbContext _db = db;

    public async Task<int> CountProvidersUsingRole(string roleId, CancellationToken cancellationToken)
    {
        // A role can only be pinned by an enabled policy (a disabled one stores NULL),
        // so matching on DefaultRoleId directly counts exactly the providers the FK
        // would block this role's deletion for.
        return await _db.OidcProviders
            .CountAsync(p => p.RegistrationPolicy.DefaultRoleId == roleId, cancellationToken);
    }
}
