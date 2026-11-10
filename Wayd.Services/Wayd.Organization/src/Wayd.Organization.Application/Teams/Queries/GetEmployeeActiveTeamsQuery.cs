using Wayd.Common.Domain.Enums.Organization;
using Wayd.Organization.Application.Models;

namespace Wayd.Organization.Application.Teams.Queries;

/// <summary>
/// The active teams an employee is a member of, by name; teams of teams are left out. Empty for an employee on
/// none, or one that does not exist.
/// </summary>
public sealed record GetEmployeeActiveTeamsQuery(Guid EmployeeId) : IQuery<IReadOnlyList<TeamNavigationDto>>;

/// <summary>Handles <see cref="GetEmployeeActiveTeamsQuery"/>.</summary>
public sealed class GetEmployeeActiveTeamsQueryHandler(IOrganizationDbContext organizationDbContext)
    : IQueryHandler<GetEmployeeActiveTeamsQuery, IReadOnlyList<TeamNavigationDto>>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;

    /// <inheritdoc />
    public async Task<IReadOnlyList<TeamNavigationDto>> Handle(GetEmployeeActiveTeamsQuery request, CancellationToken cancellationToken)
    {
        // One membership row per role held, so a member with two roles would list the team twice.
        var teams = await _organizationDbContext.TeamMembers
            .Where(m => m.EmployeeId == request.EmployeeId && m.Team.IsActive && m.Team.Type == TeamType.Team)
            .Select(m => new { m.Team.Id, m.Team.Key, m.Team.Name, m.Team.Code, m.Team.Type })
            .Distinct()
            .ToListAsync(cancellationToken);

        return [.. teams
            .Select(t => new TeamNavigationDto { Id = t.Id, Key = t.Key, Name = t.Name, Code = t.Code.Value, Type = t.Type.GetDisplayName() })
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Id)];
    }
}
