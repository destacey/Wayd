using NodaTime;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.Settings;

namespace Wayd.Organization.Application.EmployeeCapacities.Queries;

public sealed class GetTeamsCapacityByPeriodQueryValidator : CustomValidator<GetTeamsCapacityByPeriodQuery>
{
    public GetTeamsCapacityByPeriodQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(q => q.TeamIds)
            .NotEmpty();

        RuleFor(q => q.Periods)
            .NotEmpty()
            .Must(periods => periods.All(p => p.End >= p.Start))
            .WithMessage("A period cannot end before it starts.")
            .Must(periods => Period.DaysBetween(periods.Min(p => p.Start), periods.Max(p => p.End)) < GetTeamCapacityQueryValidator.MaxDays)
            .WithMessage($"The periods cannot span more than {GetTeamCapacityQueryValidator.MaxDays} days.");
    }
}

public sealed class GetTeamsCapacityByPeriodQueryHandler(
    IOrganizationDbContext organizationDbContext,
    ISettings<SchedulingSettings> schedulingSettings,
    IFeatureFlags featureFlags)
    : IQueryHandler<GetTeamsCapacityByPeriodQuery, IReadOnlyDictionary<Guid, IReadOnlyList<TeamPeriodCapacity>>?>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
    private readonly IFeatureFlags _featureFlags = featureFlags;

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<TeamPeriodCapacity>>?> Handle(GetTeamsCapacityByPeriodQuery request, CancellationToken cancellationToken)
    {
        // Checked here, not by each caller: with the feature off nothing may read allocations, and another
        // module's reads of them come through here.
        if (!await _featureFlags.IsEnabled(FeatureFlags.Names.TeamCapacityManagement, cancellationToken))
            return null;

        var firstDay = request.Periods.Min(p => p.Start);
        var lastDay = request.Periods.Max(p => p.End);
        List<Guid> requestedIds = [.. request.TeamIds.Distinct()];

        var teams = await _organizationDbContext.BaseTeams
            .Where(t => requestedIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Type })
            .ToListAsync(cancellationToken);

        Dictionary<Guid, IReadOnlyList<HierarchyNode>> descendantsByTeam = teams.ToDictionary(t => t.Id, _ => (IReadOnlyList<HierarchyNode>)[]);
        if (teams.Any(t => t.Type == TeamType.TeamOfTeams))
        {
            // Every membership in the range: which ones are beneath a team of teams is only known once the tree
            // has been walked, and the table is small.
            var memberships = (await _organizationDbContext.BaseTeams
                .SelectMany(t => t.ParentMemberships)
                .Where(m => m.DateRange.Start <= lastDay && (m.DateRange.End == null || m.DateRange.End >= firstDay))
                .Select(m => new { m.SourceId, m.TargetId, m.DateRange.Start, m.DateRange.End })
                .ToListAsync(cancellationToken))
                .Select(m => new HierarchyMembership(m.SourceId, m.TargetId, m.Start, m.End))
                .ToList();

            foreach (var teamOfTeams in teams.Where(t => t.Type == TeamType.TeamOfTeams))
                descendantsByTeam[teamOfTeams.Id] = TeamHierarchyPeriods.Descendants(teamOfTeams.Id, firstDay, lastDay, memberships);
        }

        List<Guid> teamIds = [.. descendantsByTeam.Keys, .. descendantsByTeam.Values.SelectMany(TeamIdsOf)];
        teamIds = [.. teamIds.Distinct()];

        var teamDays = await TeamWorkingDays.Load(_organizationDbContext, _schedulingSettings, teamIds, firstDay, lastDay, cancellationToken);

        var rows = await (
            from allocation in _organizationDbContext.EmployeeTeamAllocations
            where teamIds.Contains(allocation.TeamId) && allocation.Start <= lastDay && (allocation.End == null || allocation.End >= firstDay)
            join role in _organizationDbContext.TeamMemberRoles on allocation.RoleId equals (Guid?)role.Id into roles
            from role in roles.DefaultIfEmpty()
            select new
            {
                allocation.EmployeeId,
                allocation.TeamId,
                allocation.Start,
                allocation.End,
                allocation.Percent,
                RoleCounts = role == null ? (bool?)null : role.CountsTowardDeliveryCapacity,
            })
            .ToListAsync(cancellationToken);

        var allocationsByTeam = rows
            .Select(a => new CapacityAllocation(a.EmployeeId, a.TeamId, a.RoleCounts ?? true, a.Start, a.End, a.Percent))
            .ToLookup(a => a.TeamId);

        var availability = await AvailabilityLoader.Load(
            _organizationDbContext,
            _schedulingSettings,
            [.. rows.Select(a => a.EmployeeId).Distinct()],
            [],
            firstDay,
            lastDay,
            cancellationToken);

        return descendantsByTeam.ToDictionary(
            team => team.Key,
            team => TeamPeriodCapacityCalculator.Calculate(team.Key, request.Periods, team.Value, teamDays, allocationsByTeam, availability, request.TeamDaysOff));
    }

    private static IEnumerable<Guid> TeamIdsOf(IReadOnlyList<HierarchyNode> nodes) =>
        nodes.SelectMany(n => TeamIdsOf(n.Children).Prepend(n.TeamId));
}
