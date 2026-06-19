using System.Linq.Expressions;
using Wayd.ProjectPortfolioManagement.Domain.Models;

namespace Wayd.ProjectPortfolioManagement.Application.Projects.HealthChecks;

/// <summary>
/// EF-translatable expressions for querying <see cref="ProjectHealthCheck"/> entities.
/// These encode the same rules as the domain methods (e.g. <see cref="ProjectHealthCheck.IsExpired"/>)
/// in a form that can be pushed to the database rather than evaluated in memory.
/// </summary>
internal static class ProjectHealthCheckExpressions
{
    /// <summary>
    /// Returns a predicate that matches the single active (non-expired, non-deleted) health check
    /// at the given instant. The domain invariant guarantees at most one such record exists.
    /// </summary>
    internal static Expression<Func<ProjectHealthCheck, bool>> IsActive(Instant now) =>
        h => !h.IsDeleted && h.Expiration > now;
}
