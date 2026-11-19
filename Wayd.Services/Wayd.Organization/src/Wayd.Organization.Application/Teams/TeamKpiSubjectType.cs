using NodaTime;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Kpis;
using Wayd.Common.Domain.Authorization;
using Wayd.Organization.Application.Teams.Imports;
using Wayd.Organization.Domain;

namespace Wayd.Organization.Application.Teams;

/// <summary>
/// Who may read and change a team's or a team of teams' KPIs.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Reading</strong> needs what reading the team needs: the Teams View permission.
/// </para>
/// <para>
/// <strong>Changing</strong> is membership, in any role: of the team itself, or of any team of teams above it as the
/// hierarchy stands today — not only the direct parent, so a leader can set a KPI on any team in their part of the
/// organization. No permission claim is needed or sufficient; the KPI administrator's waiver is applied by
/// <see cref="KpiSubjectAccessResolver"/>.
/// </para>
/// </remarks>
public sealed class TeamKpiSubjectType(
    IOrganizationDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ISystemCalendar systemCalendar) : IKpiSubjectType
{
    /// <summary>The permission reading a team's KPIs needs.</summary>
    public static readonly string ViewPermission = ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Teams);

    /// <summary>
    /// The deepest chain of teams of teams walked. Well beyond any real hierarchy; it stops a cycle a bad import left
    /// in the memberships from looping.
    /// </summary>
    private const int MaxDepth = 32;

    private readonly IOrganizationDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ISystemCalendar _systemCalendar = systemCalendar;

    /// <inheritdoc/>
    public string SubjectType => OrganizationKpiSubjects.Team.Key;

    /// <inheritdoc/>
    public async Task<KpiSubjectAccess?> Resolve(Guid subjectId, CancellationToken cancellationToken)
    {
        if (!await _currentPrincipal.HoldsPermission(ViewPermission, cancellationToken))
            return null;

        var team = await _dbContext.BaseTeams
            .Where(t => t.Id == subjectId)
            .Select(t => new { t.Name, Code = t.Code.Value })
            .FirstOrDefaultAsync(cancellationToken);
        if (team is null)
            return null;

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        var canManage = employeeId is { } id
            && await IsMemberOfTeamOrAbove(subjectId, id, await _systemCalendar.Today(cancellationToken), cancellationToken);

        return new KpiSubjectAccess(team.Name, team.Code, canManage);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<Guid, ImportRecordLink>> Describe(IReadOnlyCollection<Guid> subjectIds, CancellationToken cancellationToken) =>
        TeamImportLinks.ForTeams(_dbContext, subjectIds, cancellationToken);

    private async Task<bool> IsMemberOfTeamOrAbove(Guid teamId, Guid employeeId, LocalDate asOf, CancellationToken cancellationToken)
    {
        var chain = new List<Guid> { teamId };
        var current = teamId;

        while (chain.Count < MaxDepth)
        {
            var parentId = await _dbContext.BaseTeams
                .Where(t => t.Id == current)
                .SelectMany(t => t.ParentMemberships)
                .Where(pm => pm.DateRange.Start <= asOf && (pm.DateRange.End == null || pm.DateRange.End >= asOf))
                .Select(pm => (Guid?)pm.TargetId)
                .FirstOrDefaultAsync(cancellationToken);

            if (parentId is not { } parent || chain.Contains(parent))
                break;

            chain.Add(parent);
            current = parent;
        }

        return await _dbContext.BaseTeams
            .Where(t => chain.Contains(t.Id))
            .AnyAsync(t => t.Members.Any(m => m.EmployeeId == employeeId), cancellationToken);
    }
}
