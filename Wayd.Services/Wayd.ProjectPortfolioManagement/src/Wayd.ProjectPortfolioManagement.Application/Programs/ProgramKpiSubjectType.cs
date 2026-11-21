using System.Globalization;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Kpis;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Programs;

/// <summary>
/// Who may read and change a program's KPIs.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Reading</strong> follows the program's read rule: the View Programs permission.
/// </para>
/// <para>
/// <strong>Changing</strong> needs what every other change to a program needs: the Update Programs permission and
/// delivery leadership — Owner or Manager on the program or its portfolio, or the PPM administrator, by
/// <c>Program.CanManageProgram</c>. The PPM administrator waives the leadership, never the permission. A closed
/// program's KPIs stay open to change. The KPI administrator's waiver is applied by
/// <see cref="KpiSubjectAccessResolver"/>.
/// </para>
/// </remarks>
public sealed class ProgramKpiSubjectType(
    IProjectPortfolioManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser) : IKpiSubjectType
{
    /// <summary>The permission changing a program's KPIs needs, beside delivery leadership.</summary>
    public static readonly string UpdatePermission =
        ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.Programs);

    private readonly IProjectPortfolioManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;

    /// <inheritdoc/>
    public string SubjectType => PpmKpiSubjects.Program.Key;

    /// <inheritdoc/>
    public async Task<KpiSubjectAccess?> Resolve(Guid subjectId, CancellationToken cancellationToken)
    {
        if (!await _currentPrincipal.HoldsPermission(ProgramReadRule.ViewPermission, cancellationToken))
            return null;

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is not { } id || !await _currentPrincipal.HoldsPermission(UpdatePermission, cancellationToken))
        {
            var summary = await _dbContext.Programs
                .Where(p => p.Id == subjectId)
                .Select(p => new { p.Name, p.Key })
                .FirstOrDefaultAsync(cancellationToken);

            return summary is null ? null : new KpiSubjectAccess(summary.Name, Code(summary.Key), CanManage: false);
        }

        // The portfolio's roles must be loaded: without them the check silently finds no inherited leadership and
        // refuses the portfolio's leader.
        var program = await _dbContext.Programs
            .AsNoTracking()
            .Include(p => p.Roles)
            .Include(p => p.Portfolio).ThenInclude(p => p!.Roles)
            .FirstOrDefaultAsync(p => p.Id == subjectId, cancellationToken);
        if (program is null)
            return null;

        var isPpmAdministrator = await _currentPrincipal.HoldsPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, cancellationToken);
        var canManage = program.CanManageProgram(new PpmActor(id, isPpmAdministrator, _currentUser.GetUserId()), program.AncestryRoles());

        return new KpiSubjectAccess(program.Name, Code(program.Key), canManage);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Guid, ImportRecordLink>> Describe(IReadOnlyCollection<Guid> subjectIds, CancellationToken cancellationToken) =>
        await _dbContext.Programs
            .AsNoTracking()
            .Where(p => subjectIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Key, p.Name })
            .ToDictionaryAsync(p => p.Id, p => new ImportRecordLink(ImportRecordType.Program, Code(p.Key), p.Name), cancellationToken);

    private static string Code(int key) => key.ToString(CultureInfo.InvariantCulture);
}
