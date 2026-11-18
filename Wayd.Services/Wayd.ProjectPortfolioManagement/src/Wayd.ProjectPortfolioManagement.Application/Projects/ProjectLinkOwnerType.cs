using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Authorization;
using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Projects;

/// <summary>
/// Who may read and change a project's links.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Reading</strong> needs what reading the project needs, by <see cref="ProjectReadRule"/>'s rule.
/// </para>
/// <para>
/// <strong>Changing</strong> is editing the project, by the rule <c>Project.UpdateDetails</c> applies: the Update
/// permission, delivery leadership (Owner or Manager on the project, its program or its portfolio, or the PPM
/// administrator) and a linked employee.
/// </para>
/// </remarks>
public sealed class ProjectLinkOwnerType(
    IProjectPortfolioManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser) : ILinkOwnerType
{
    /// <summary>The permission editing a project needs, beside delivery leadership.</summary>
    public static readonly string UpdatePermission = ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.Projects);

    private readonly IProjectPortfolioManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;

    /// <inheritdoc/>
    public string OwnerType => PpmLinkOwners.Project.Key;

    /// <inheritdoc/>
    public async Task<LinkOwnerAccess?> Resolve(Guid ownerId, CancellationToken cancellationToken)
    {
        if (!await _currentPrincipal.HoldsPermission(ProjectReadRule.ViewPermission, cancellationToken))
            return null;

        // The ancestors' roles must be loaded: without them the check silently finds no inherited leadership and
        // refuses a program's or portfolio's leader.
        var project = await _dbContext.Projects
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.Roles)
            .Include(p => p.Portfolio).ThenInclude(p => p!.Roles)
            .Include(p => p.Program).ThenInclude(p => p!.Roles)
            .FirstOrDefaultAsync(p => p.Id == ownerId, cancellationToken);
        if (project is null)
            return null;

        if (!await _currentPrincipal.HasPermission(UpdatePermission, cancellationToken))
            return new LinkOwnerAccess(false);

        // Null for a read-only token and for an account with no employee, neither of which can lead a project.
        var (employeeId, isPpmAdministrator) = await _currentPrincipal.ResolveManagementHint(cancellationToken);
        if (employeeId is not { } id)
            return new LinkOwnerAccess(false);

        var actor = new PpmActor(id, isPpmAdministrator, _currentUser.GetUserId());

        return new LinkOwnerAccess(project.CanManageProject(actor, project.AncestryRoles()));
    }
}
