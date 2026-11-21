using System.Globalization;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Kpis;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.Imports;
using Wayd.ProjectPortfolioManagement.Domain;

namespace Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives;

/// <summary>
/// Who may read and change a strategic initiative's KPIs.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Reading</strong> follows the initiative endpoints: the View Strategic Initiatives permission.
/// </para>
/// <para>
/// <strong>Changing</strong> needs the Update Strategic Initiatives permission alone, as every other change to an
/// initiative does: unlike a project, program or portfolio, editing an initiative needs no delivery leadership. A
/// completed or canceled initiative's KPIs stay open to change, as a closed project's do: benefits are often measured
/// after an initiative ends. The KPI administrator's waiver is applied by <see cref="KpiSubjectAccessResolver"/>.
/// </para>
/// </remarks>
public sealed class StrategicInitiativeKpiSubjectType(
    IProjectPortfolioManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal) : IKpiSubjectType
{
    /// <summary>The permission reading an initiative's KPIs needs.</summary>
    public static readonly string ViewPermission =
        ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.StrategicInitiatives);

    /// <summary>The permission changing an initiative's KPIs needs.</summary>
    public static readonly string UpdatePermission =
        ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.StrategicInitiatives);

    private readonly IProjectPortfolioManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    /// <inheritdoc/>
    public string SubjectType => PpmKpiSubjects.StrategicInitiative.Key;

    /// <inheritdoc/>
    public async Task<KpiSubjectAccess?> Resolve(Guid subjectId, CancellationToken cancellationToken)
    {
        if (!await _currentPrincipal.HoldsPermission(ViewPermission, cancellationToken))
            return null;

        var summary = await _dbContext.StrategicInitiatives
            .Where(s => s.Id == subjectId)
            .Select(s => new { s.Name, s.Key })
            .FirstOrDefaultAsync(cancellationToken);
        if (summary is null)
            return null;

        var canManage = await _currentPrincipal.HoldsPermission(UpdatePermission, cancellationToken);

        return new KpiSubjectAccess(summary.Name, Code(summary.Key), canManage);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Guid, ImportRecordLink>> Describe(IReadOnlyCollection<Guid> subjectIds, CancellationToken cancellationToken) =>
        await _dbContext.StrategicInitiatives
            .AsNoTracking()
            .Where(s => subjectIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Key, s.Name })
            .ToDictionaryAsync(s => s.Id, s => new ImportRecordLink(ImportRecordType.StrategicInitiative, Code(s.Key), s.Name), cancellationToken);

    private static string Code(int key) => key.ToString(CultureInfo.InvariantCulture);
}
