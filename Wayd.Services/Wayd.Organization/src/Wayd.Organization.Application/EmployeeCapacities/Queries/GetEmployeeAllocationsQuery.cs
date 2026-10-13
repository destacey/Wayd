using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Organization.Application.EmployeeCapacities.Dtos;
using Wayd.Organization.Application.Models;

namespace Wayd.Organization.Application.EmployeeCapacities.Queries;

/// <summary>
/// Every allocation of an employee's time, across all teams, in date order. Null when the employee does not exist.
/// </summary>
public sealed record GetEmployeeAllocationsQuery(Guid EmployeeId) : IQuery<IReadOnlyList<EmployeeAllocationDto>?>;

public sealed class GetEmployeeAllocationsQueryHandler(
    IOrganizationDbContext organizationDbContext,
    IDispatcher dispatcher,
    ICurrentPrincipal currentPrincipal,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetEmployeeAllocationsQuery, IReadOnlyList<EmployeeAllocationDto>?>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<IReadOnlyList<EmployeeAllocationDto>?> Handle(GetEmployeeAllocationsQuery request, CancellationToken cancellationToken)
    {
        if (!await _organizationDbContext.Employees.AnyAsync(e => e.Id == request.EmployeeId, cancellationToken))
            return null;

        var rows = await (
            from allocation in _organizationDbContext.EmployeeTeamAllocations
            where allocation.EmployeeId == request.EmployeeId
            join team in _organizationDbContext.BaseTeams on allocation.TeamId equals team.Id
            join role in _organizationDbContext.TeamMemberRoles on allocation.RoleId equals (Guid?)role.Id into roles
            from role in roles.DefaultIfEmpty()
            select new
            {
                allocation.Id,
                allocation.TeamId,
                TeamKey = team.Key,
                TeamName = team.Name,
                TeamCode = team.Code.Value,
                TeamType = team.Type,
                allocation.RoleId,
                RoleKey = role == null ? (int?)null : role.Key,
                RoleName = role == null ? null : role.Name,
                allocation.Start,
                allocation.End,
                allocation.Percent,
            })
            .ToListAsync(cancellationToken);

        List<Guid> teamIds = [.. rows.Select(r => r.TeamId).Distinct()];
        var manageable = await _currentPrincipal.TeamsWithManageableAllocations(_dispatcher, teamIds, _dateTimeProvider.Now.InUtc().Date, cancellationToken);

        return [.. rows
            .OrderBy(r => r.Start)
            .ThenBy(r => r.TeamName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.RoleName, StringComparer.OrdinalIgnoreCase)
            .Select(r => new EmployeeAllocationDto
            {
                Id = r.Id,
                Team = new TeamNavigationDto
                {
                    Id = r.TeamId,
                    Key = r.TeamKey,
                    Name = r.TeamName,
                    Code = r.TeamCode,
                    Type = r.TeamType.GetDisplayName(),
                },
                Role = r.RoleId is { } roleId && r.RoleKey is { } roleKey && r.RoleName is not null
                    ? NavigationDto.Create(roleId, roleKey, r.RoleName)
                    : null,
                Start = r.Start,
                End = r.End,
                Percent = r.Percent,
                CanManage = manageable.Contains(r.TeamId),
            })];
    }
}
