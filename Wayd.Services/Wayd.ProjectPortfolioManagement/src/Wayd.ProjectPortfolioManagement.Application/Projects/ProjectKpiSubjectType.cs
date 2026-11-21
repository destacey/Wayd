using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Kpis;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Projects;

/// <summary>
/// Who may read and change a project's KPIs.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Reading</strong> follows the project's read rule: the View Projects permission.
/// </para>
/// <para>
/// <strong>Changing</strong> needs what every other change to a project needs: the Update Projects permission and
/// delivery leadership — Owner or Manager on the project, its program or its portfolio, or the PPM administrator, by
/// <c>Project.CanManageProject</c>. The PPM administrator waives the leadership, never the permission. A closed
/// project's KPIs stay open to change: benefits are often measured after a project ends. The KPI administrator's
/// waiver is applied by <see cref="KpiSubjectAccessResolver"/>.
/// </para>
/// </remarks>
public sealed class ProjectKpiSubjectType(
    IProjectPortfolioManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser) : IKpiSubjectType
{
    /// <summary>The permission changing a project's KPIs needs, beside delivery leadership.</summary>
    public static readonly string UpdatePermission =
        ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.Projects);

    private readonly IProjectPortfolioManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;

    /// <inheritdoc/>
    public string SubjectType => PpmKpiSubjects.Project.Key;

    /// <inheritdoc/>
    public async Task<KpiSubjectAccess?> Resolve(Guid subjectId, CancellationToken cancellationToken)
    {
        if (!await _currentPrincipal.HoldsPermission(ProjectReadRule.ViewPermission, cancellationToken))
            return null;

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is not { } id || !await _currentPrincipal.HoldsPermission(UpdatePermission, cancellationToken))
        {
            var summary = await _dbContext.Projects
                .Where(p => p.Id == subjectId)
                .Select(p => new { p.Name, p.Key })
                .FirstOrDefaultAsync(cancellationToken);

            return summary is null ? null : new KpiSubjectAccess(summary.Name, summary.Key.Value, CanManage: false);
        }

        // The ancestors' roles must be loaded: without them the check silently finds no inherited leadership and
        // refuses a program's or portfolio's leader.
        var project = await _dbContext.Projects
            .AsNoTracking()
            .Include(p => p.Roles)
            .Include(p => p.Portfolio).ThenInclude(p => p!.Roles)
            .Include(p => p.Program).ThenInclude(p => p!.Roles)
            .FirstOrDefaultAsync(p => p.Id == subjectId, cancellationToken);
        if (project is null)
            return null;

        var isPpmAdministrator = await _currentPrincipal.HoldsPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, cancellationToken);
        var canManage = project.CanManageProject(new PpmActor(id, isPpmAdministrator, _currentUser.GetUserId()), project.AncestryRoles());

        return new KpiSubjectAccess(project.Name, project.Key.Value, canManage);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Guid, ImportRecordLink>> Describe(IReadOnlyCollection<Guid> subjectIds, CancellationToken cancellationToken) =>
        await _dbContext.Projects
            .AsNoTracking()
            .Where(p => subjectIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Key, p.Name })
            .ToDictionaryAsync(p => p.Id, p => new ImportRecordLink(ImportRecordType.Project, p.Key.Value, p.Name), cancellationToken);
}
