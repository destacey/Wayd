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
    ///
    /// Takes <paramref name="currentUser"/> alongside the principal rather than reading the user id from
    /// the principal: the two interfaces are a deliberate identity/authorization split, and
    /// <see cref="ICurrentPrincipal"/> answers only what the caller may do.
    /// </summary>
    /// <exception cref="Exception">Thrown via <c>LinkedEmployeeRequired</c> when the user has no linked employee.</exception>
    public static async Task<PpmActor> ResolvePpmActor(this ICurrentPrincipal currentPrincipal, ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        Guid? employeeId = await currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is null)
            LinkedEmployeeRequired.Throw();

        var isPpmAdministrator = await currentPrincipal.HasPermission(PpmAdministratorPermission, cancellationToken);

        return new PpmActor(employeeId!.Value, isPpmAdministrator, currentUser.GetUserId());
    }

    /// <summary>
    /// Builds an actor for attribution only, on a path that does not evaluate delivery leadership —
    /// creation, where there is no existing record to hold roles on. The result carries no
    /// administrator standing and must not be passed to a method that gates on membership; use
    /// <see cref="ResolvePpmActor"/> there instead.
    /// </summary>
    public static PpmActor AttributionOnlyActor(this ICurrentUser currentUser) =>
        new(currentUser.GetEmployeeId() ?? Guid.Empty, IsPpmAdministrator: false, currentUser.GetUserId());

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
