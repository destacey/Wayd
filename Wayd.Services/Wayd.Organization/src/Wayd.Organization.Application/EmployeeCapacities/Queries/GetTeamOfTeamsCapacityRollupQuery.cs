using NodaTime;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Settings;
using Wayd.Organization.Application.EmployeeCapacities.Dtos;
using Wayd.Organization.Application.Models;

namespace Wayd.Organization.Application.EmployeeCapacities.Queries;

/// <summary>
/// The capacity of a team of teams from <paramref name="From"/> to <paramref name="To"/>, both inclusive, with
/// every team beneath it rolled up. Null when there is no team of teams with that id.
/// </summary>
/// <remarks>
/// A team beneath it counts only for the days it was in the hierarchy, over its own working days. An
/// allocation counts once, on the team it was made to, so someone allocated to the team of teams and to a
/// team beneath it has time at each level and none of it twice. Nothing is stored: the rollup is worked out
/// from the allocations and the team memberships as they stand.
/// </remarks>
public sealed record GetTeamOfTeamsCapacityRollupQuery(Guid TeamOfTeamsId, LocalDate From, LocalDate To) : IQuery<TeamCapacityRollupDto?>;

public sealed class GetTeamOfTeamsCapacityRollupQueryValidator : CustomValidator<GetTeamOfTeamsCapacityRollupQuery>
{
    public GetTeamOfTeamsCapacityRollupQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(q => q.TeamOfTeamsId)
            .NotEmpty();

        RuleFor(q => q.To)
            .GreaterThanOrEqualTo(q => q.From)
            .WithMessage("The to date cannot be before the from date.")
            .Must((q, to) => Period.DaysBetween(q.From, to) < GetTeamCapacityQueryValidator.MaxDays)
            .WithMessage($"The date range cannot be longer than {GetTeamCapacityQueryValidator.MaxDays} days.");
    }
}

public sealed class GetTeamOfTeamsCapacityRollupQueryHandler(
    IOrganizationDbContext organizationDbContext,
    IDispatcher dispatcher,
    ISettings<SchedulingSettings> schedulingSettings)
    : IQueryHandler<GetTeamOfTeamsCapacityRollupQuery, TeamCapacityRollupDto?>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;

    public async Task<TeamCapacityRollupDto?> Handle(GetTeamOfTeamsCapacityRollupQuery request, CancellationToken cancellationToken)
    {
        var rootId = request.TeamOfTeamsId;
        var firstDay = request.From;
        var lastDay = request.To;

        if (!await _organizationDbContext.BaseTeams.AnyAsync(t => t.Id == rootId && t.Type == TeamType.TeamOfTeams, cancellationToken))
            return null;

        // Every membership in the range, not only those beneath this team of teams: which ones are beneath
        // it is only known once the tree has been walked, and the table is small.
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

        var teams = await _organizationDbContext.BaseTeams
            .Where(t => teamIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Key, t.Name, Code = t.Code.Value, t.Type })
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        var workingDays = await TeamWorkingDays.Load(_organizationDbContext, _dispatcher, _schedulingSettings, teamIds, firstDay, lastDay, cancellationToken);

        var allocations = (await (
            from allocation in _organizationDbContext.EmployeeTeamAllocations
            where teamIds.Contains(allocation.TeamId) && allocation.Start <= lastDay && (allocation.End == null || allocation.End >= firstDay)
            join employee in _organizationDbContext.Employees on allocation.EmployeeId equals employee.Id
            join role in _organizationDbContext.TeamMemberRoles on allocation.RoleId equals (Guid?)role.Id into roles
            from role in roles.DefaultIfEmpty()
            select new AllocationRow(
                allocation.Id,
                allocation.TeamId,
                allocation.EmployeeId,
                employee.Key,
                employee.Name.FirstName,
                employee.Name.LastName,
                allocation.RoleId,
                role == null ? (int?)null : role.Key,
                role == null ? null : role.Name,
                role == null ? (bool?)null : role.CountsTowardDeliveryCapacity,
                allocation.Start,
                allocation.End,
                allocation.Percent))
            .ToListAsync(cancellationToken))
            .ToLookup(a => a.TeamId);

        // Every allocation the rollup counts, from the root and each node, for the figures that span the tree.
        List<(Guid EmployeeId, TeamCapacityAllocationDto Allocation)> counted = [];

        List<TeamCapacityRollupPersonDto> PeopleOf(Guid teamId, IReadOnlyList<HierarchyPeriod> periods, out int workingDayCount)
        {
            List<LocalDate> days = [.. workingDays[teamId].Where(d => periods.Any(p => p.Includes(d)))];
            workingDayCount = days.Count;

            List<TeamCapacityRollupPersonDto> people = [.. allocations[teamId]
                .Where(a => periods.Any(p => p.Overlaps(a.Start, a.End)))
                .GroupBy(a => a.EmployeeId)
                .Select(person =>
                {
                    var first = person.First();
                    List<TeamCapacityAllocationDto> lines = [.. person
                        .OrderBy(a => a.Start)
                        .ThenBy(a => a.RoleName, StringComparer.OrdinalIgnoreCase)
                        .Select(a => new TeamCapacityAllocationDto
                        {
                            Id = a.Id,
                            Role = a.RoleId is { } roleId && a.RoleKey is { } roleKey && a.RoleName is not null
                                ? NavigationDto.Create(roleId, roleKey, a.RoleName)
                                : null,
                            CountsTowardDeliveryCapacity = a.RoleCounts ?? true,
                            Start = a.Start,
                            End = a.End,
                            Percent = a.Percent,
                            PersonDays = TeamCapacityCalculator.PersonDays(days, a.Start, a.End, a.Percent),
                        })];

                    return new TeamCapacityRollupPersonDto
                    {
                        Employee = NavigationDto.Create(person.Key, first.EmployeeKey, $"{first.FirstName} {first.LastName}"),
                        AllocatedPersonDays = lines.Sum(a => a.PersonDays),
                        DeliveryPersonDays = lines.Where(a => a.CountsTowardDeliveryCapacity).Sum(a => a.PersonDays),
                        Allocations = lines,
                    };
                })
                .OrderBy(p => p.Employee.Name, StringComparer.OrdinalIgnoreCase)];

            counted.AddRange(people.SelectMany(p => p.Allocations.Select(a => (p.Employee.Id, a))));
            return people;
        }

        List<TeamCapacityRollupNodeDto> NodesOf(IReadOnlyList<HierarchyNode> nodes) => [.. nodes
            .Select(node =>
            {
                var team = teams[node.TeamId];
                var people = PeopleOf(node.TeamId, node.Periods, out var workingDayCount);
                var children = NodesOf(node.Children);
                var directAllocated = people.Sum(p => p.AllocatedPersonDays);
                var directDelivery = people.Sum(p => p.DeliveryPersonDays);

                return new TeamCapacityRollupNodeDto
                {
                    Team = new TeamNavigationDto
                    {
                        Id = team.Id,
                        Key = team.Key,
                        Name = team.Name,
                        Code = team.Code,
                        Type = team.Type.GetDisplayName(),
                    },
                    Periods = [.. node.Periods.Select(p => new TeamCapacityPeriodDto { Start = p.Start, End = p.End })],
                    WorkingDays = workingDayCount,
                    AllocatedPersonDays = directAllocated + children.Sum(c => c.AllocatedPersonDays),
                    DeliveryPersonDays = directDelivery + children.Sum(c => c.DeliveryPersonDays),
                    DirectAllocatedPersonDays = directAllocated,
                    DirectDeliveryPersonDays = directDelivery,
                    People = people,
                    Children = children,
                };
            })
            .OrderBy(n => n.Team.Name, StringComparer.OrdinalIgnoreCase)];

        var direct = PeopleOf(rootId, [new HierarchyPeriod(firstDay, lastDay)], out _);
        var rootChildren = NodesOf(hierarchy);
        var rootAllocated = direct.Sum(p => p.AllocatedPersonDays);
        var rootDelivery = direct.Sum(p => p.DeliveryPersonDays);

        List<TeamCapacityRoleDto> byRole = [.. counted
            .GroupBy(c => c.Allocation.Role?.Id)
            .Select(role => new TeamCapacityRoleDto
            {
                Role = role.First().Allocation.Role,
                CountsTowardDeliveryCapacity = role.First().Allocation.CountsTowardDeliveryCapacity,
                PersonDays = role.Sum(c => c.Allocation.PersonDays),
            })
            .OrderBy(r => r.Role is null)
            .ThenBy(r => r.Role?.Name, StringComparer.OrdinalIgnoreCase)];

        return new TeamCapacityRollupDto
        {
            From = firstDay,
            To = lastDay,
            AllocatedPersonDays = rootAllocated + rootChildren.Sum(c => c.AllocatedPersonDays),
            DeliveryPersonDays = rootDelivery + rootChildren.Sum(c => c.DeliveryPersonDays),
            DirectAllocatedPersonDays = rootAllocated,
            DirectDeliveryPersonDays = rootDelivery,
            People = counted.Select(c => c.EmployeeId).Distinct().Count(),
            Roles = byRole,
            Children = rootChildren,
        };
    }

    private static IEnumerable<Guid> TeamIdsOf(IReadOnlyList<HierarchyNode> nodes) =>
        nodes.SelectMany(n => TeamIdsOf(n.Children).Prepend(n.TeamId));

    /// <summary>An allocation to a team in the tree, with the person and role it names.</summary>
    private sealed record AllocationRow(
        Guid Id,
        Guid TeamId,
        Guid EmployeeId,
        int EmployeeKey,
        string FirstName,
        string LastName,
        Guid? RoleId,
        int? RoleKey,
        string? RoleName,
        bool? RoleCounts,
        LocalDate Start,
        LocalDate? End,
        int Percent);
}
