using Wayd.Common.Domain.Authorization;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Common;

/// <summary>
/// Bridges the application layer's identity and claims to the <see cref="PpmActor"/> the PPM aggregates
/// require, so every gated handler resolves the actor the same way.
/// </summary>
public static class PpmAuthorizationExtensions
{
    /// <summary>
    /// The domain-wide PPM administrator permission. Waives role membership across PPM; it does NOT waive
    /// the resource permission, which the controller's <c>[MustHavePermission]</c> attribute still enforces.
    /// </summary>
    public static readonly string PpmAdministratorPermission =
        ApplicationPermission.NameFor(ApplicationAction.Administer, ApplicationResource.ProjectPortfolioManagement);

    /// <summary>
    /// Resolves the current principal into a <see cref="PpmActor"/>. Commands using this must be marked
    /// <c>IRequireLinkedEmployee</c> — a managed PPM action is always attributable to an employee, and
    /// membership cannot be evaluated without one.
    /// </summary>
    /// <exception cref="Exception">Thrown via <c>LinkedEmployeeRequired</c> when the user has no linked employee.</exception>
    public static async Task<PpmActor> ResolvePpmActor(this ICurrentPrincipal currentPrincipal, CancellationToken cancellationToken)
    {
        Guid? employeeId = await currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
            LinkedEmployeeRequired.Throw();

        var isPpmAdministrator = await currentPrincipal.HasPermission(PpmAdministratorPermission, cancellationToken);

        return new PpmActor(employeeId!.Value, isPpmAdministrator);
    }

    /// <summary>
    /// Reads the ancestor role assignments a project's authorization check needs. The caller must have
    /// loaded <c>Portfolio.Roles</c> and (when assigned) <c>Program.Roles</c> — an unloaded navigation
    /// silently yields no inherited leadership, which would deny a legitimately authorized actor.
    /// </summary>
    public static ProjectAncestryRoles AncestryRoles(this Project project) =>
        new(project.Portfolio?.Roles ?? [], project.Program?.Roles);

    /// <summary>
    /// Reads the ancestor role assignments a program's authorization check needs. The caller must have
    /// loaded <c>Portfolio.Roles</c>.
    /// </summary>
    public static ProgramAncestryRoles AncestryRoles(this Program program) =>
        new(program.Portfolio?.Roles ?? []);
}
