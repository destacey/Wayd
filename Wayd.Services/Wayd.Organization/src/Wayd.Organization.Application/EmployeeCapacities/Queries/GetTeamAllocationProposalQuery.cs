using NodaTime;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Organization.Application.EmployeeCapacities.Dtos;

namespace Wayd.Organization.Application.EmployeeCapacities.Queries;

/// <summary>
/// Allocations a team or team of teams could start from on <paramref name="From"/>: each active member, one
/// row per active role they hold, sharing what is left of their time. It reads only; the planner saves what
/// they confirm through the allocation commands. Null when there is no <paramref name="TeamType"/> with that id.
/// </summary>
/// <remarks>
/// A member with nothing left is still returned, at 0%, so the planner sees who could not be proposed. Saving
/// a proposed row replaces what the team has on its days, so a row never covers time the team already has: it
/// stops the day before the member's next allocation to the team, and a member with time on the team on
/// <paramref name="From"/> is left out.
/// </remarks>
public sealed record GetTeamAllocationProposalQuery(Guid TeamId, TeamType TeamType, LocalDate From) : IQuery<TeamAllocationProposalDto?>;

public sealed class GetTeamAllocationProposalQueryValidator : CustomValidator<GetTeamAllocationProposalQuery>
{
    public GetTeamAllocationProposalQueryValidator()
    {
        RuleFor(q => q.TeamId)
            .NotEmpty();
    }
}

public sealed class GetTeamAllocationProposalQueryHandler(IOrganizationDbContext organizationDbContext)
    : IQueryHandler<GetTeamAllocationProposalQuery, TeamAllocationProposalDto?>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;

    public async Task<TeamAllocationProposalDto?> Handle(GetTeamAllocationProposalQuery request, CancellationToken cancellationToken)
    {
        var teamId = request.TeamId;
        var teamType = request.TeamType;
        var from = request.From;

        if (!await _organizationDbContext.BaseTeams.AnyAsync(t => t.Id == teamId && t.Type == teamType, cancellationToken))
            return null;

        var members = await _organizationDbContext.TeamMembers
            .Where(m => m.TeamId == teamId && m.Employee.IsActive && m.Role.IsActive)
            .Select(m => new
            {
                m.EmployeeId,
                EmployeeKey = m.Employee.Key,
                m.Employee.Name.FirstName,
                m.Employee.Name.LastName,
                m.RoleId,
                RoleKey = m.Role.Key,
                RoleName = m.Role.Name,
                RoleCounts = m.Role.CountsTowardDeliveryCapacity,
            })
            .ToListAsync(cancellationToken);

        List<Guid> employeeIds = [.. members.Select(m => m.EmployeeId).Distinct()];

        var allocations = await _organizationDbContext.EmployeeTeamAllocations
            .Where(a => employeeIds.Contains(a.EmployeeId) && (a.End == null || a.End >= from))
            .Select(a => new { a.EmployeeId, a.TeamId, a.Start, a.End, a.Percent })
            .ToListAsync(cancellationToken);

        var allocationsByEmployee = allocations.ToLookup(a => a.EmployeeId, a => (a.Start, a.End, a.Percent));

        // Each member's first day already on the team, from the first day of the proposal on.
        var firstDayOnTeam = allocations
            .Where(a => a.TeamId == teamId)
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(a => a.Key, a => a.Min(x => x.Start));

        List<TeamAllocationProposalRowDto> rows = [.. members
            .GroupBy(m => m.EmployeeId)
            .Where(member => !firstDayOnTeam.TryGetValue(member.Key, out var first) || first > from)
            .Select(member =>
            {
                LocalDate? end = firstDayOnTeam.TryGetValue(member.Key, out var first) ? first.PlusDays(-1) : null;

                return new
                {
                    Name = $"{member.First().FirstName} {member.First().LastName}",
                    Roles = member.OrderBy(m => m.RoleName, StringComparer.OrdinalIgnoreCase).ToList(),
                    End = end,
                    Remaining = AllocationProposal.RemainingPercent([.. allocationsByEmployee[member.Key]], from, end),
                };
            })
            .OrderBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
            .SelectMany(member =>
            {
                var shares = AllocationProposal.SplitEvenly(member.Remaining, member.Roles.Count);

                return member.Roles.Select((role, index) => new TeamAllocationProposalRowDto
                {
                    Employee = NavigationDto.Create(role.EmployeeId, role.EmployeeKey, member.Name),
                    Role = NavigationDto.Create(role.RoleId, role.RoleKey, role.RoleName),
                    CountsTowardDeliveryCapacity = role.RoleCounts,
                    End = member.End,
                    RemainingPercent = member.Remaining,
                    Percent = shares[index],
                });
            })];

        return new TeamAllocationProposalDto
        {
            From = from,
            Rows = rows,
        };
    }
}
