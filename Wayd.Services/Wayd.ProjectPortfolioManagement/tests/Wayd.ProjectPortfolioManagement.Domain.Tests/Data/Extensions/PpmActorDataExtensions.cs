using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Data.Extensions;

/// <summary>
/// Helpers for building the authorization arguments the PPM aggregates now require.
///
/// Tests fall into two groups. Tests *about* authorization should build actors and ancestry explicitly
/// so the arrangement states the rule under test. Tests about anything else should use
/// <see cref="AnAuthorizedActor"/> (or a faker's role helpers) so the authorization argument stays out
/// of the way and the test reads as being about its real subject.
/// </summary>
public static class PpmActorDataExtensions
{
    /// <summary>
    /// An actor who passes every membership check by virtue of the PPM administrator grant. Use in tests
    /// whose subject is not authorization — it keeps arrangement to one argument.
    /// </summary>
    public static PpmActor AnAuthorizedActor() => new(Guid.NewGuid(), IsPpmAdministrator: true, Guid.NewGuid().ToString());

    /// <summary>
    /// An actor holding no roles and no administrator grant — denied by every membership check.
    /// </summary>
    public static PpmActor AnUnauthorizedActor() => new(Guid.NewGuid(), IsPpmAdministrator: false, Guid.NewGuid().ToString());

    /// <summary>
    /// An ordinary (non-administrator) actor for the given employee. Whether they are authorized depends
    /// entirely on the roles assigned to them on the aggregate or its ancestors.
    /// </summary>
    public static PpmActor AsActor(this Guid employeeId) => new(employeeId, IsPpmAdministrator: false, Guid.NewGuid().ToString());

    /// <summary>
    /// A PPM administrator for the given employee.
    /// </summary>
    public static PpmActor AsPpmAdministrator(this Guid employeeId) => new(employeeId, IsPpmAdministrator: true, Guid.NewGuid().ToString());

    /// <summary>
    /// An ordinary actor for the given employee, attributed to a specific user account. Use where the
    /// test asserts on the recorded user rather than on the authorization outcome.
    /// </summary>
    public static PpmActor AsActorForUser(this Guid employeeId, string userId) =>
        new(employeeId, IsPpmAdministrator: false, userId);

    /// <summary>
    /// Ancestry conferring no inherited leadership — the common case when a test assigns roles directly
    /// on the aggregate under test.
    /// </summary>
    public static ProjectAncestryRoles NoProjectAncestry() => ProjectAncestryRoles.None;

    /// <summary>
    /// Ancestry conferring no inherited leadership.
    /// </summary>
    public static ProgramAncestryRoles NoProgramAncestry() => ProgramAncestryRoles.None;

    /// <summary>
    /// Project ancestry granting the given employee the specified portfolio role.
    /// </summary>
    public static ProjectAncestryRoles WithPortfolioRole(Guid portfolioId, Guid employeeId, ProjectPortfolioRole role) =>
        new([new RoleAssignment<ProjectPortfolioRole>(portfolioId, role, employeeId)], null);

    /// <summary>
    /// Project ancestry granting the given employee the specified program role.
    /// </summary>
    public static ProjectAncestryRoles WithProgramRole(Guid programId, Guid employeeId, ProgramRole role) =>
        new([], [new RoleAssignment<ProgramRole>(programId, role, employeeId)]);

    /// <summary>
    /// Program ancestry granting the given employee the specified portfolio role.
    /// </summary>
    public static ProgramAncestryRoles WithPortfolioRoleForProgram(Guid portfolioId, Guid employeeId, ProjectPortfolioRole role) =>
        new([new RoleAssignment<ProjectPortfolioRole>(portfolioId, role, employeeId)]);

    /// <summary>
    /// Gives the employee the Owner role on the project, so an ordinary actor for that employee is
    /// authorized without needing the administrator grant or any ancestry.
    /// </summary>
    public static ProjectFaker WithOwner(this ProjectFaker faker, Guid employeeId) =>
        faker.WithRoles(new Dictionary<ProjectRole, HashSet<Guid>> { [ProjectRole.Owner] = [employeeId] });

    /// <summary>
    /// Gives the employee the Owner role on the program.
    /// </summary>
    public static ProgramFaker WithOwner(this ProgramFaker faker, Guid employeeId) =>
        faker.WithRoles(new Dictionary<ProgramRole, HashSet<Guid>> { [ProgramRole.Owner] = [employeeId] });

    /// <summary>
    /// Gives the employee the Owner role on the portfolio.
    /// </summary>
    public static ProjectPortfolioFaker WithOwner(this ProjectPortfolioFaker faker, Guid employeeId) =>
        faker.WithRoles(new Dictionary<ProjectPortfolioRole, HashSet<Guid>> { [ProjectPortfolioRole.Owner] = [employeeId] });

    /// <summary>
    /// Gives the employee the Sponsor role. Sponsors are deliberately NOT authorized to manage delivery,
    /// so this exists to arrange the negative case.
    /// </summary>
    public static ProjectFaker WithSponsor(this ProjectFaker faker, Guid employeeId) =>
        faker.WithRoles(new Dictionary<ProjectRole, HashSet<Guid>> { [ProjectRole.Sponsor] = [employeeId] });
}
