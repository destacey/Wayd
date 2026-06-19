using Wayd.Common.Application.Interfaces;

namespace Wayd.Common.Application.Identity.Roles;

/// <summary>
/// Reports how many OIDC providers pin a given role as their auto-registration
/// default. Used by role deletion to block removing a role that's still referenced
/// — mirroring the database FK guarantee with a friendly, pre-emptive error.
/// </summary>
public interface IOidcProviderDefaultRoleChecker : ITransientService
{
    Task<int> CountProvidersUsingRole(string roleId, CancellationToken cancellationToken);
}
