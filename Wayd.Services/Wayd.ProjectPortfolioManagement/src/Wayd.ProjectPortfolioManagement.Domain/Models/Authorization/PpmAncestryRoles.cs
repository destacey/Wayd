using Wayd.ProjectPortfolioManagement.Domain.Enums;

namespace Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

/// <summary>
/// The role assignments on a project's ancestors — the parent portfolio, and the parent program when
/// the project is assigned to one. A <see cref="Project"/> cannot navigate upward to load these, so
/// the application layer supplies them.
///
/// Delivery leadership inherits downward: an Owner/Manager on the portfolio or program may manage
/// every project beneath it. This type carries the inputs that rule needs.
/// </summary>
/// <param name="PortfolioRoles">Role assignments on the parent portfolio.</param>
/// <param name="ProgramRoles">Role assignments on the parent program, or null when unassigned.</param>
public sealed record ProjectAncestryRoles(
    IEnumerable<RoleAssignment<ProjectPortfolioRole>> PortfolioRoles,
    IEnumerable<RoleAssignment<ProgramRole>>? ProgramRoles)
{
    /// <summary>
    /// Ancestry with no inherited leadership. Used where the caller has already established that
    /// membership is irrelevant — for example a <see cref="PpmActor.System"/> import path.
    /// </summary>
    public static ProjectAncestryRoles None { get; } = new([], null);
}

/// <summary>
/// The role assignments on a program's ancestor — its parent portfolio. A <see cref="Program"/>
/// cannot navigate upward to load these, so the application layer supplies them.
/// </summary>
/// <param name="PortfolioRoles">Role assignments on the parent portfolio.</param>
public sealed record ProgramAncestryRoles(IEnumerable<RoleAssignment<ProjectPortfolioRole>> PortfolioRoles)
{
    /// <summary>
    /// Ancestry with no inherited leadership.
    /// </summary>
    public static ProgramAncestryRoles None { get; } = new([]);
}
