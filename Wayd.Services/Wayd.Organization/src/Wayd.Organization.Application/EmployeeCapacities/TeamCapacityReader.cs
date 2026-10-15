using NodaTime;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Settings;
using Wayd.Organization.Application.EmployeeCapacities.Dtos;
using Wayd.Organization.Application.Models;

namespace Wayd.Organization.Application.EmployeeCapacities;

/// <summary>An allocation to the team being read, with the person and role it names.</summary>
internal sealed record TeamAllocationRow(
    Guid Id,
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
    int Percent)
{
    /// <summary>Whether the time adds to delivery capacity: true when it has no role.</summary>
    public bool CountsTowardDeliveryCapacity => RoleCounts ?? true;
}

/// <summary>An allocation of someone on the team being read to another team.</summary>
internal sealed record ElsewhereAllocationRow(
    Guid Id,
    Guid EmployeeId,
    Guid TeamId,
    int TeamKey,
    string TeamName,
    string TeamCode,
    TeamType TeamType,
    Guid? RoleId,
    int? RoleKey,
    string? RoleName,
    LocalDate Start,
    LocalDate? End,
    int Percent);

/// <summary>
/// Everything a team's capacity over a range is worked out from: its working days, the allocations to it and
/// elsewhere of the people on it, when each of them is there, and which teams the caller may manage.
/// </summary>
internal sealed record TeamCapacityReading(
    Guid TeamId,
    LocalDate From,
    LocalDate To,
    IReadOnlyList<TeamDay> TeamDays,
    IReadOnlyList<TeamAllocationRow> Here,
    IReadOnlyList<ElsewhereAllocationRow> Elsewhere,
    AvailabilityCalculator Availability,
    IReadOnlySet<Guid> Manageable,
    decimal WorkingDayHours)
{
    /// <summary>The team's capacity over the range, person by person.</summary>
    public TeamCapacityDto ToCapacity()
    {
        var capacityByEmployee = TeamCapacityCalculator.Calculate(
            TeamId,
            TeamDays,
            Here.Select(a => new CapacityAllocation(a.EmployeeId, TeamId, a.CountsTowardDeliveryCapacity, a.Start, a.End, a.Percent))
                .Concat(Elsewhere.Select(a => new CapacityAllocation(a.EmployeeId, a.TeamId, true, a.Start, a.End, a.Percent))),
            Availability);

        var elsewhereByEmployee = Elsewhere.ToLookup(a => a.EmployeeId);

        List<TeamCapacityPersonDto> people = [.. Here
            .GroupBy(a => a.EmployeeId)
            .Select(person =>
            {
                var first = person.First();
                var capacity = capacityByEmployee[person.Key];

                return new TeamCapacityPersonDto
                {
                    Employee = NavigationDto.Create(person.Key, first.EmployeeKey, $"{first.FirstName} {first.LastName}"),
                    AllocatedPersonDays = capacity.AllocatedPersonDays,
                    DeliveryPersonDays = capacity.DeliveryPersonDays,
                    ElsewherePersonDays = capacity.ElsewherePersonDays,
                    AwayPersonDays = capacity.AwayPersonDays,
                    TimeAway = [.. Availability.TimeAwayOf(person.Key)
                        .Select(t => new TeamCapacityTimeAwayDto { Start = t.Start, End = t.End, PercentAway = t.PercentAway })],
                    Allocations = [.. person
                        .OrderBy(a => a.Start)
                        .ThenBy(a => a.RoleName, StringComparer.OrdinalIgnoreCase)
                        .Select(a => new TeamCapacityAllocationDto
                        {
                            Id = a.Id,
                            Role = RoleOf(a.RoleId, a.RoleKey, a.RoleName),
                            CountsTowardDeliveryCapacity = a.CountsTowardDeliveryCapacity,
                            Start = a.Start,
                            End = a.End,
                            Percent = a.Percent,
                            PersonDays = TeamCapacityCalculator.PersonDays(TeamDays, person.Key, a.Start, a.End, a.Percent, Availability),
                        })],
                    ElsewhereAllocations = [.. elsewhereByEmployee[person.Key]
                        .OrderBy(a => a.Start)
                        .ThenBy(a => a.TeamName, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(a => a.RoleName, StringComparer.OrdinalIgnoreCase)
                        .Select(a => new EmployeeAllocationDto
                        {
                            Id = a.Id,
                            Team = new TeamNavigationDto
                            {
                                Id = a.TeamId,
                                Key = a.TeamKey,
                                Name = a.TeamName,
                                Code = a.TeamCode,
                                Type = a.TeamType.GetDisplayName(),
                            },
                            Role = RoleOf(a.RoleId, a.RoleKey, a.RoleName),
                            Start = a.Start,
                            End = a.End,
                            Percent = a.Percent,
                            CanManage = Manageable.Contains(a.TeamId),
                        })],
                };
            })
            .OrderBy(p => p.Employee.Name, StringComparer.OrdinalIgnoreCase)];

        List<TeamCapacityRoleDto> byRole = [.. people
            .SelectMany(p => p.Allocations)
            .GroupBy(a => a.Role?.Id)
            .Select(role => new TeamCapacityRoleDto
            {
                Role = role.First().Role,
                CountsTowardDeliveryCapacity = role.First().CountsTowardDeliveryCapacity,
                PersonDays = role.Sum(a => a.PersonDays),
            })
            .OrderBy(r => r.Role is null)
            .ThenBy(r => r.Role?.Name, StringComparer.OrdinalIgnoreCase)];

        return new TeamCapacityDto
        {
            From = From,
            To = To,
            WorkingDays = TeamDays.Count(d => !Availability.IsTeamHoliday(d)),
            AllocatedPersonDays = people.Sum(p => p.AllocatedPersonDays),
            DeliveryPersonDays = people.Sum(p => p.DeliveryPersonDays),
            AwayPersonDays = people.Sum(p => p.AwayPersonDays),
            CanManage = Manageable.Contains(TeamId),
            WorkingDayHours = WorkingDayHours,
            Roles = byRole,
            People = people,
        };
    }

    /// <summary>The team's capacity over the range day by day, person by person and role by role.</summary>
    /// <param name="timeAwayManageable">The people whose time away the caller may manage.</param>
    public TeamCapacityPlanDto ToPlan(IReadOnlySet<Guid> timeAwayManageable)
    {
        var capacity = ToCapacity();
        var hereByEmployee = Here.ToLookup(a => a.EmployeeId);
        var elsewhereByEmployee = Elsewhere.ToLookup(a => a.EmployeeId);

        return new TeamCapacityPlanDto
        {
            Capacity = capacity,
            Days = [.. TeamDays.Select(d => d.Date)],
            People = [.. capacity.People.Select(person =>
            {
                var employeeId = person.Employee.Id;

                List<CapacityAllocation> elsewhere = [.. elsewhereByEmployee[employeeId]
                    .Select(a => new CapacityAllocation(employeeId, a.TeamId, true, a.Start, a.End, a.Percent))];

                return new TeamCapacityPlanPersonDto
                {
                    EmployeeId = employeeId,
                    CanManageTimeAway = timeAwayManageable.Contains(employeeId),
                    Days = [.. TeamCapacityCalculator.DailyConditions(TeamDays, employeeId, elsewhere, Availability)
                        .Select(d => new TeamCapacityPlanPersonDayDto
                        {
                            Date = d.Date,
                            IsHoliday = d.IsHoliday,
                            PercentAway = d.PercentAway,
                            ElsewherePercent = d.ElsewherePercent,
                        })],
                    Rows = [.. hereByEmployee[employeeId]
                        .GroupBy(a => a.RoleId)
                        .Select(slot =>
                        {
                            var first = slot.First();
                            List<CapacityAllocation> allocations = [.. slot
                                .Select(a => new CapacityAllocation(employeeId, TeamId, a.CountsTowardDeliveryCapacity, a.Start, a.End, a.Percent))];

                            return new TeamCapacityPlanRowDto
                            {
                                Role = RoleOf(first.RoleId, first.RoleKey, first.RoleName),
                                CountsTowardDeliveryCapacity = first.CountsTowardDeliveryCapacity,
                                Days = [.. TeamCapacityCalculator.DailyAllocation(TeamDays, employeeId, allocations, Availability)
                                    .Select(d => new TeamCapacityPlanDayDto { Date = d.Date, Percent = d.Percent, PersonDays = d.PersonDays })],
                            };
                        })
                        .OrderBy(r => r.Role is null)
                        .ThenBy(r => r.Role?.Name, StringComparer.OrdinalIgnoreCase)],
                };
            })],
        };
    }

    /// <summary>The team member role an allocation names, or null when it names none.</summary>
    public static NavigationDto? RoleOf(Guid? id, int? key, string? name) =>
        id is { } roleId && key is { } roleKey && name is not null
            ? NavigationDto.Create(roleId, roleKey, name)
            : null;
}

/// <summary>Reads what a team's capacity over a range is worked out from.</summary>
internal static class TeamCapacityReader
{
    /// <summary>
    /// The reading for the <paramref name="teamType"/> with id <paramref name="teamId"/> from
    /// <paramref name="firstDay"/> to <paramref name="lastDay"/>, both inclusive, or null when there is no such team.
    /// </summary>
    /// <remarks>
    /// The same reads whatever the team's size: the team, its working days, the allocations to it, its people's
    /// allocations elsewhere, their availability, and which of those teams the caller may manage.
    /// </remarks>
    public static async Task<TeamCapacityReading?> Read(
        IOrganizationDbContext organizationDbContext,
        IDispatcher dispatcher,
        ICurrentPrincipal currentPrincipal,
        ISettings<SchedulingSettings> schedulingSettings,
        LocalDate today,
        Guid teamId,
        TeamType teamType,
        LocalDate firstDay,
        LocalDate lastDay,
        CancellationToken cancellationToken)
    {
        if (!await organizationDbContext.BaseTeams.AnyAsync(t => t.Id == teamId && t.Type == teamType, cancellationToken))
            return null;

        var teamDays = await TeamWorkingDays.Load(organizationDbContext, schedulingSettings, teamId, firstDay, lastDay, cancellationToken);

        List<TeamAllocationRow> here = await (
            from allocation in organizationDbContext.EmployeeTeamAllocations
            where allocation.TeamId == teamId && allocation.Start <= lastDay && (allocation.End == null || allocation.End >= firstDay)
            join employee in organizationDbContext.Employees on allocation.EmployeeId equals employee.Id
            join role in organizationDbContext.TeamMemberRoles on allocation.RoleId equals (Guid?)role.Id into roles
            from role in roles.DefaultIfEmpty()
            select new TeamAllocationRow(
                allocation.Id,
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
            .ToListAsync(cancellationToken);

        List<Guid> employeeIds = [.. here.Select(a => a.EmployeeId).Distinct()];

        List<ElsewhereAllocationRow> elsewhere = await (
            from allocation in organizationDbContext.EmployeeTeamAllocations
            where employeeIds.Contains(allocation.EmployeeId) && allocation.TeamId != teamId && allocation.Start <= lastDay && (allocation.End == null || allocation.End >= firstDay)
            join team in organizationDbContext.BaseTeams on allocation.TeamId equals team.Id
            join role in organizationDbContext.TeamMemberRoles on allocation.RoleId equals (Guid?)role.Id into roles
            from role in roles.DefaultIfEmpty()
            select new ElsewhereAllocationRow(
                allocation.Id,
                allocation.EmployeeId,
                allocation.TeamId,
                team.Key,
                team.Name,
                team.Code.Value,
                team.Type,
                allocation.RoleId,
                role == null ? (int?)null : role.Key,
                role == null ? null : role.Name,
                allocation.Start,
                allocation.End,
                allocation.Percent))
            .ToListAsync(cancellationToken);

        var availability = await AvailabilityLoader.Load(
            organizationDbContext,
            schedulingSettings,
            employeeIds,
            teamDays.Select(d => d.HolidayCalendarId).Distinct(),
            firstDay,
            lastDay,
            cancellationToken);

        List<Guid> teamIds = [teamId, .. elsewhere.Select(a => a.TeamId).Distinct()];
        var manageable = await currentPrincipal.TeamsWithManageableAllocations(dispatcher, teamIds, today, cancellationToken);

        var workingDayHours = (await schedulingSettings.Get(cancellationToken)).WorkingDayHours;

        return new TeamCapacityReading(teamId, firstDay, lastDay, teamDays, here, elsewhere, availability, manageable, workingDayHours);
    }
}
