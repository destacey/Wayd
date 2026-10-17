using NodaTime;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Settings;
using Wayd.Organization.Application.EmployeeCapacities.Dtos;
using Wayd.Organization.Application.Models;

namespace Wayd.Organization.Application.EmployeeCapacities.Queries;

/// <summary>
/// The capacity report of a team, or of a team of teams with every team beneath it, from
/// <paramref name="From"/> to <paramref name="To"/>, both inclusive, bucketed by <paramref name="Interval"/>
/// when given. Null when there is no <paramref name="TeamType"/> with that id.
/// </summary>
/// <remarks>
/// A team beneath a team of teams counts only for the days it was in the hierarchy, read from the team
/// memberships as they were on each day. An allocation counts once, on the team it was made to.
/// </remarks>
public sealed record GetTeamCapacityReportQuery(Guid TeamId, TeamType TeamType, LocalDate From, LocalDate To, CapacityReportInterval? Interval) : IQuery<TeamCapacityReportDto?>;

/// <summary>Holds a capacity report request to a known team and a range of at most two years.</summary>
public sealed class GetTeamCapacityReportQueryValidator : CustomValidator<GetTeamCapacityReportQuery>
{
    public GetTeamCapacityReportQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(q => q.TeamId)
            .NotEmpty();

        RuleFor(q => q.Interval)
            .IsInEnum();

        RuleFor(q => q.To)
            .GreaterThanOrEqualTo(q => q.From)
            .WithMessage("The to date cannot be before the from date.")
            .Must((q, to) => Period.DaysBetween(q.From, to) < GetTeamCapacityQueryValidator.MaxDays)
            .WithMessage($"The date range cannot be longer than {GetTeamCapacityQueryValidator.MaxDays} days.");
    }
}

/// <summary>
/// Reads the hierarchy, the allocations of everyone with time in it, and what decides their availability, then
/// works the report out with <see cref="CapacityReportCalculator"/>.
/// </summary>
public sealed class GetTeamCapacityReportQueryHandler(
    IOrganizationDbContext organizationDbContext,
    ISettings<SchedulingSettings> schedulingSettings)
    : IQueryHandler<GetTeamCapacityReportQuery, TeamCapacityReportDto?>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;

    /// <summary>The report, or null when there is no team of the type asked for with that id.</summary>
    public async Task<TeamCapacityReportDto?> Handle(GetTeamCapacityReportQuery request, CancellationToken cancellationToken)
    {
        var rootId = request.TeamId;
        var firstDay = request.From;
        var lastDay = request.To;

        if (!await _organizationDbContext.BaseTeams.AnyAsync(t => t.Id == rootId && t.Type == request.TeamType, cancellationToken))
            return null;

        // Every membership in the range: which ones are beneath the root is only known once the tree has been
        // walked, and the table is small.
        var memberships = await _organizationDbContext.BaseTeams
            .SelectMany(t => t.ParentMemberships)
            .Where(m => m.DateRange.Start <= lastDay && (m.DateRange.End == null || m.DateRange.End >= firstDay))
            .Select(m => new { m.SourceId, m.TargetId, m.DateRange.Start, m.DateRange.End })
            .ToListAsync(cancellationToken);

        var hierarchy = TeamHierarchyPeriods.Descendants(
            rootId,
            firstDay,
            lastDay,
            memberships.Select(m => new HierarchyMembership(m.SourceId, m.TargetId, m.Start, m.End)));

        List<Guid> teamIds = [rootId, .. TeamIdsOf(hierarchy).Distinct()];

        // Everyone with time in the hierarchy, then all of their time in the range: what lies outside the
        // hierarchy is their time elsewhere. The join leaves out anyone whose employee record cannot be read, as
        // the rollup does, so every grouping of the report covers the same people.
        var employees = await (
            from allocation in _organizationDbContext.EmployeeTeamAllocations
            where teamIds.Contains(allocation.TeamId) && allocation.Start <= lastDay && (allocation.End == null || allocation.End >= firstDay)
            join employee in _organizationDbContext.Employees on allocation.EmployeeId equals employee.Id
            select new { employee.Id, employee.Key, employee.Name.FirstName, employee.Name.LastName, employee.HireDate, employee.IsActive })
            .Distinct()
            .ToDictionaryAsync(e => e.Id, cancellationToken);
        List<Guid> employeeIds = [.. employees.Keys];

        var allocations = await (
            from allocation in _organizationDbContext.EmployeeTeamAllocations
            where employeeIds.Contains(allocation.EmployeeId) && allocation.Start <= lastDay && (allocation.End == null || allocation.End >= firstDay)
            join role in _organizationDbContext.TeamMemberRoles on allocation.RoleId equals (Guid?)role.Id into roles
            from role in roles.DefaultIfEmpty()
            select new CapacityReportAllocation(
                allocation.EmployeeId,
                allocation.TeamId,
                allocation.RoleId,
                role == null || role.CountsTowardDeliveryCapacity,
                allocation.Start,
                allocation.End,
                allocation.Percent))
            .ToListAsync(cancellationToken);

        var teams = await _organizationDbContext.BaseTeams
            .Where(t => teamIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Key, t.Name, Code = t.Code.Value, t.Type })
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        List<Guid> roleIds = [.. allocations.Select(a => a.RoleId).OfType<Guid>().Distinct()];
        var roleNames = await _organizationDbContext.TeamMemberRoles
            .Where(r => roleIds.Contains(r.Id))
            .Select(r => new { r.Id, r.Key, r.Name })
            .ToDictionaryAsync(r => r.Id, r => NavigationDto.Create(r.Id, r.Key, r.Name), cancellationToken);

        var teamDays = await TeamWorkingDays.Load(_organizationDbContext, _schedulingSettings, teamIds, firstDay, lastDay, cancellationToken);
        var settings = await _schedulingSettings.Get(cancellationToken);
        var defaultDays = TeamWorkingDays.Default(settings.DefaultWorkingWeek(), firstDay, lastDay);

        // Employees carry no leaving date, but deactivating one ends their allocations that day, so the last day
        // their allocations cover is when an inactive person left.
        var employment = employees.Values.ToDictionary(
            e => e.Id,
            e => new EmploymentPeriod(
                e.HireDate?.InUtc().Date,
                e.IsActive || allocations.Any(a => a.EmployeeId == e.Id && a.End is null)
                    ? null
                    : allocations.Where(a => a.EmployeeId == e.Id).Max(a => a.End)));

        var availability = await AvailabilityLoader.Load(
            _organizationDbContext,
            _schedulingSettings,
            employeeIds,
            teamDays.Values.SelectMany(days => days).Select(d => d.HolidayCalendarId).Distinct(),
            firstDay,
            lastDay,
            cancellationToken);

        var report = CapacityReportCalculator.Calculate(
            rootId, firstDay, lastDay, hierarchy, teamDays, allocations, defaultDays, availability, request.Interval, employment);

        NavigationDto? RoleOf(Guid? roleId) => roleId is { } id ? roleNames.GetValueOrDefault(id) : null;
        string NameOf(Guid employeeId) => employees.TryGetValue(employeeId, out var e) ? $"{e.FirstName} {e.LastName}" : string.Empty;

        return new TeamCapacityReportDto
        {
            From = firstDay,
            To = lastDay,
            Interval = request.Interval,
            Buckets = [.. report.Buckets.Select(b => new TeamCapacityPeriodDto { Start = b.Start, End = b.End })],
            WorkingDayHours = settings.WorkingDayHours,
            Total = CapacityMeasuresDto.From(report.Lines.Aggregate(CapacityMeasures.Zero, (sum, l) => sum + l.Total)),
            Roles = [.. report.Lines
                .GroupBy(l => l.RoleId)
                .Select(role => new TeamCapacityReportRoleDto
                {
                    Role = RoleOf(role.Key),
                    CountsTowardDeliveryCapacity = role.First().CountsTowardDeliveryCapacity,
                    Total = CapacityMeasuresDto.From(role.Aggregate(CapacityMeasures.Zero, (sum, l) => sum + l.Total)),
                })
                .OrderBy(r => r.Role is null)
                .ThenBy(r => r.Role?.Name, StringComparer.OrdinalIgnoreCase)],
            Teams = [.. report.Nodes.Select(n =>
            {
                var team = teams[n.TeamId];
                return new TeamCapacityReportTeamDto
                {
                    NodeId = n.NodeId,
                    ParentNodeId = n.ParentNodeId,
                    Team = new TeamNavigationDto { Id = team.Id, Key = team.Key, Name = team.Name, Code = team.Code, Type = team.Type.GetDisplayName() },
                    Periods = [.. n.Periods.Select(p => new TeamCapacityPeriodDto { Start = p.Start, End = p.End })],
                    WorkingDays = n.WorkingDays,
                };
            })],
            People = [.. report.People
                .Where(p => employees.ContainsKey(p.EmployeeId))
                .Select(p => new TeamCapacityReportPersonDto
                {
                    Employee = NavigationDto.Create(p.EmployeeId, employees[p.EmployeeId].Key, NameOf(p.EmployeeId)),
                    OutsidePersonDays = p.Outside,
                    UnallocatedPersonDays = p.Unallocated,
                    OutsideBuckets = p.OutsideBuckets,
                    UnallocatedBuckets = p.UnallocatedBuckets,
                })
                .OrderBy(p => p.Employee.Name, StringComparer.OrdinalIgnoreCase)],
            Lines = [.. report.Lines
                .OrderBy(l => l.NodeId)
                .ThenBy(l => NameOf(l.EmployeeId), StringComparer.OrdinalIgnoreCase)
                .ThenBy(l => RoleOf(l.RoleId) is null)
                .ThenBy(l => RoleOf(l.RoleId)?.Name, StringComparer.OrdinalIgnoreCase)
                .Select(l => new TeamCapacityReportLineDto
                {
                    NodeId = l.NodeId,
                    EmployeeId = l.EmployeeId,
                    Role = RoleOf(l.RoleId),
                    CountsTowardDeliveryCapacity = l.CountsTowardDeliveryCapacity,
                    Total = CapacityMeasuresDto.From(l.Total),
                    Buckets = [.. l.Buckets.Select(CapacityMeasuresDto.From)],
                })],
        };
    }

    /// <summary>Every team in the tree, depth first; a team at two places appears twice.</summary>
    private static IEnumerable<Guid> TeamIdsOf(IReadOnlyList<HierarchyNode> nodes) =>
        nodes.SelectMany(n => TeamIdsOf(n.Children).Prepend(n.TeamId));
}
