using NodaTime;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.Settings;

namespace Wayd.Organization.Application.EmployeeCapacities.Queries;

public sealed class GetTeamDayAvailabilityQueryHandler(
    IOrganizationDbContext organizationDbContext,
    ISettings<SchedulingSettings> schedulingSettings,
    IFeatureFlags featureFlags)
    : IQueryHandler<GetTeamDayAvailabilityQuery, IReadOnlyDictionary<LocalDate, TeamDayAvailability>?>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
    private readonly IFeatureFlags _featureFlags = featureFlags;

    public async Task<IReadOnlyDictionary<LocalDate, TeamDayAvailability>?> Handle(GetTeamDayAvailabilityQuery request, CancellationToken cancellationToken)
    {
        // Checked here, not by each caller: with the feature off nothing may read allocations, and this is
        // the one place another module's reads of them come through.
        if (!await _featureFlags.IsEnabled(FeatureFlags.Names.TeamCapacityManagement, cancellationToken))
            return null;

        var teamId = request.TeamId;
        var firstDay = request.From;
        var lastDay = request.To;

        var rows = await (
            from allocation in _organizationDbContext.EmployeeTeamAllocations
            where allocation.TeamId == teamId && allocation.Start <= lastDay && (allocation.End == null || allocation.End >= firstDay)
            join role in _organizationDbContext.TeamMemberRoles on allocation.RoleId equals (Guid?)role.Id into roles
            from role in roles.DefaultIfEmpty()
            select new
            {
                allocation.EmployeeId,
                allocation.Start,
                allocation.End,
                allocation.Percent,
                RoleCounts = role == null ? (bool?)null : role.CountsTowardDeliveryCapacity,
            })
            .ToListAsync(cancellationToken);

        List<CapacityAllocation> allocations = [.. rows
            .Select(a => new CapacityAllocation(a.EmployeeId, teamId, a.RoleCounts ?? true, a.Start, a.End, a.Percent))
            .Where(a => a.CountsTowardDeliveryCapacity)];
        if (allocations.Count == 0)
            return null;

        var availability = await AvailabilityLoader.Load(
            _organizationDbContext,
            _schedulingSettings,
            [.. allocations.Select(a => a.EmployeeId).Distinct()],
            [],
            firstDay,
            lastDay,
            cancellationToken);

        // Every day of the range: the caller applies the team's working week and its days off.
        List<TeamDay> days = [];
        for (var day = firstDay; day <= lastDay; day = day.PlusDays(1))
            days.Add(new TeamDay(day, null));

        return TeamCapacityCalculator.DeliveryAvailabilityByDay(days, allocations, availability);
    }
}
