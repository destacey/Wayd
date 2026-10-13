using NodaTime;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Settings;
using Wayd.Organization.Application.EmployeeCapacities.Dtos;

namespace Wayd.Organization.Application.EmployeeCapacities.Queries;

/// <summary>
/// The capacity of a team or team of teams from <paramref name="From"/> to <paramref name="To"/>, both
/// inclusive: who is allocated to it, and their person-days over its working days. Null when there is no
/// <paramref name="TeamType"/> with that id.
/// </summary>
/// <remarks>
/// A team of teams counts only the people allocated to it directly, not those allocated to the teams beneath it.
/// </remarks>
public sealed record GetTeamCapacityQuery(Guid TeamId, TeamType TeamType, LocalDate From, LocalDate To) : IQuery<TeamCapacityDto?>;

public sealed class GetTeamCapacityQueryValidator : CustomValidator<GetTeamCapacityQuery>
{
    /// <summary>The longest range a capacity read covers, in days: two years.</summary>
    public const int MaxDays = 731;

    public GetTeamCapacityQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(q => q.TeamId)
            .NotEmpty();

        RuleFor(q => q.To)
            .GreaterThanOrEqualTo(q => q.From)
            .WithMessage("The to date cannot be before the from date.")
            .Must((q, to) => Period.DaysBetween(q.From, to) < MaxDays)
            .WithMessage($"The date range cannot be longer than {MaxDays} days.");
    }
}

public sealed class GetTeamCapacityQueryHandler(
    IOrganizationDbContext organizationDbContext,
    IDispatcher dispatcher,
    ICurrentPrincipal currentPrincipal,
    ISettings<SchedulingSettings> schedulingSettings,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetTeamCapacityQuery, TeamCapacityDto?>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<TeamCapacityDto?> Handle(GetTeamCapacityQuery request, CancellationToken cancellationToken)
    {
        var teamId = request.TeamId;
        var firstDay = request.From;
        var lastDay = request.To;

        var teamType = request.TeamType;
        if (!await _organizationDbContext.BaseTeams.AnyAsync(t => t.Id == teamId && t.Type == teamType, cancellationToken))
            return null;

        var workingDays = await TeamWorkingDays.Load(_organizationDbContext, _dispatcher, _schedulingSettings, teamId, firstDay, lastDay, cancellationToken);

        var here = await (
            from allocation in _organizationDbContext.EmployeeTeamAllocations
            where allocation.TeamId == teamId && allocation.Start <= lastDay && (allocation.End == null || allocation.End >= firstDay)
            join employee in _organizationDbContext.Employees on allocation.EmployeeId equals employee.Id
            join role in _organizationDbContext.TeamMemberRoles on allocation.RoleId equals (Guid?)role.Id into roles
            from role in roles.DefaultIfEmpty()
            select new
            {
                allocation.Id,
                allocation.EmployeeId,
                EmployeeKey = employee.Key,
                employee.Name.FirstName,
                employee.Name.LastName,
                allocation.RoleId,
                RoleKey = role == null ? (int?)null : role.Key,
                RoleName = role == null ? null : role.Name,
                RoleCounts = role == null ? (bool?)null : role.CountsTowardDeliveryCapacity,
                allocation.Start,
                allocation.End,
                allocation.Percent,
            })
            .ToListAsync(cancellationToken);

        List<Guid> employeeIds = [.. here.Select(a => a.EmployeeId).Distinct()];

        var elsewhere = await _organizationDbContext.EmployeeTeamAllocations
            .Where(a => employeeIds.Contains(a.EmployeeId) && a.TeamId != teamId && a.Start <= lastDay && (a.End == null || a.End >= firstDay))
            .Select(a => new { a.EmployeeId, a.TeamId, a.Start, a.End, a.Percent })
            .ToListAsync(cancellationToken);

        var capacityByEmployee = TeamCapacityCalculator.Calculate(
            teamId,
            workingDays,
            here.Select(a => new CapacityAllocation(a.EmployeeId, teamId, a.RoleCounts ?? true, a.Start, a.End, a.Percent))
                .Concat(elsewhere.Select(a => new CapacityAllocation(a.EmployeeId, a.TeamId, true, a.Start, a.End, a.Percent))));

        List<TeamCapacityPersonDto> people = [.. here
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
                    Allocations = [.. person
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
                        })],
                };
            })
            .OrderBy(p => p.Employee.Name, StringComparer.OrdinalIgnoreCase)];

        var manageable = await _currentPrincipal.TeamsWithManageableAllocations(_dispatcher, [teamId], _dateTimeProvider.Now.InUtc().Date, cancellationToken);

        return new TeamCapacityDto
        {
            From = firstDay,
            To = lastDay,
            WorkingDays = workingDays.Count,
            AllocatedPersonDays = people.Sum(p => p.AllocatedPersonDays),
            DeliveryPersonDays = people.Sum(p => p.DeliveryPersonDays),
            CanManage = manageable.Contains(teamId),
            People = people,
        };
    }
}
