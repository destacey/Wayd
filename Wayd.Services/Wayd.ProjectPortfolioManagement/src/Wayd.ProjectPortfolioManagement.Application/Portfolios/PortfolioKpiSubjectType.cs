using System.Globalization;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Kpis;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.ProjectPortfolioManagement.Application.Common;
using Wayd.ProjectPortfolioManagement.Domain;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;

namespace Wayd.ProjectPortfolioManagement.Application.Portfolios;

/// <summary>
/// Who may read and change a portfolio's KPIs.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Reading</strong> follows the portfolio endpoints: the View Portfolios permission.
/// </para>
/// <para>
/// <strong>Changing</strong> needs what every other change to a portfolio needs: the Update Portfolios permission and
/// delivery leadership — Owner or Manager on the portfolio, or the PPM administrator, by
/// <c>ProjectPortfolio.CanManagePortfolio</c>. A portfolio has no parent, so nothing is inherited. The PPM
/// administrator waives the leadership, never the permission. A closed portfolio's KPIs stay open to change. The KPI
/// administrator's waiver is applied by <see cref="KpiSubjectAccessResolver"/>.
/// </para>
/// </remarks>
public sealed class PortfolioKpiSubjectType(
    IProjectPortfolioManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser) : IKpiSubjectType
{
    /// <summary>The permission reading a portfolio's KPIs needs.</summary>
    public static readonly string ViewPermission =
        ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.ProjectPortfolios);

    /// <summary>The permission changing a portfolio's KPIs needs, beside delivery leadership.</summary>
    public static readonly string UpdatePermission =
        ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.ProjectPortfolios);

    private readonly IProjectPortfolioManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;

    /// <inheritdoc/>
    public string SubjectType => PpmKpiSubjects.Portfolio.Key;

    /// <inheritdoc/>
    public async Task<KpiSubjectAccess?> Resolve(Guid subjectId, CancellationToken cancellationToken)
    {
        if (!await _currentPrincipal.HoldsPermission(ViewPermission, cancellationToken))
            return null;

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        if (employeeId is not { } id || !await _currentPrincipal.HoldsPermission(UpdatePermission, cancellationToken))
        {
            var summary = await _dbContext.Portfolios
                .Where(p => p.Id == subjectId)
                .Select(p => new { p.Name, p.Key })
                .FirstOrDefaultAsync(cancellationToken);

            return summary is null ? null : new KpiSubjectAccess(summary.Name, Code(summary.Key), CanManage: false);
        }

        var portfolio = await _dbContext.Portfolios
            .AsNoTracking()
            .Include(p => p.Roles)
            .FirstOrDefaultAsync(p => p.Id == subjectId, cancellationToken);
        if (portfolio is null)
            return null;

        var isPpmAdministrator = await _currentPrincipal.HoldsPermission(PpmAuthorizationExtensions.PpmAdministratorPermission, cancellationToken);
        var canManage = portfolio.CanManagePortfolio(new PpmActor(id, isPpmAdministrator, _currentUser.GetUserId()));

        return new KpiSubjectAccess(portfolio.Name, Code(portfolio.Key), canManage);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Guid, ImportRecordLink>> Describe(IReadOnlyCollection<Guid> subjectIds, CancellationToken cancellationToken) =>
        await _dbContext.Portfolios
            .AsNoTracking()
            .Where(p => subjectIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Key, p.Name })
            .ToDictionaryAsync(p => p.Id, p => new ImportRecordLink(ImportRecordType.Portfolio, Code(p.Key), p.Name), cancellationToken);

    private static string Code(int key) => key.ToString(CultureInfo.InvariantCulture);
}
