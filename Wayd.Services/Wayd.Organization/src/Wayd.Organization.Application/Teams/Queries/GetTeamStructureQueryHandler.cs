using Wayd.Common.Application.Requests.Organization;
using Wayd.Organization.Domain.Enums;

namespace Wayd.Organization.Application.Teams.Queries;

public sealed class GetTeamStructureQueryHandler(IOrganizationDbContext organizationDbContext)
    : IQueryHandler<GetTeamStructureQuery, TeamStructure?>
{
    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;

    public async Task<TeamStructure?> Handle(GetTeamStructureQuery request, CancellationToken cancellationToken)
    {
        var rootExists = await _organizationDbContext.BaseTeams
            .AnyAsync(t => t.Id == request.TeamId, cancellationToken);

        if (!rootExists)
            return null;

        // Every edge in the window, then walked in memory: teams number in the hundreds, and a recursive
        // query is provider-specific SQL for no gain.
        var memberships = (await _organizationDbContext.BaseTeams
                .SelectMany(t => t.ParentMemberships)
                .Where(m => !m.IsDeleted
                    && m.DateRange.Start <= request.To
                    && (m.DateRange.End == null || request.From <= m.DateRange.End))
                .Select(m => new { m.SourceId, m.TargetId, m.DateRange.Start, m.DateRange.End })
                .ToListAsync(cancellationToken))
            .Select(m => new TeamStructureMembership(m.SourceId, m.TargetId, m.Start, m.End))
            .ToList();

        var childEdgesByParentId = memberships.ToLookup(m => m.ParentId);

        var teamIds = new HashSet<Guid> { request.TeamId };
        var subtreeEdges = new List<TeamStructureMembership>();
        var pending = new Queue<Guid>([request.TeamId]);

        // A team reached twice (it moved between two parents in the window) is walked once; only bad data
        // can produce a cycle, and the visited set stops that too.
        while (pending.TryDequeue(out var parentId))
        {
            foreach (var edge in childEdgesByParentId[parentId])
            {
                subtreeEdges.Add(edge);
                if (teamIds.Add(edge.ChildId))
                    pending.Enqueue(edge.ChildId);
            }
        }

        var teams = (await _organizationDbContext.BaseTeams
                .Where(t => teamIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Key, t.Code, t.Name, t.Type })
                .ToListAsync(cancellationToken))
            .Select(t => new TeamStructureTeam(t.Id, t.Key, t.Code.Value, t.Name, t.Type))
            .ToList();

        var sizingPeriods = (await _organizationDbContext.Teams
                .Where(t => teamIds.Contains(t.Id))
                .SelectMany(t => t.OperatingModels, (team, model) => new
                {
                    TeamId = team.Id,
                    model.DateRange.Start,
                    model.DateRange.End,
                    model.SizingMethod,
                })
                .Where(x => x.Start <= request.To && (x.End == null || request.From <= x.End))
                .ToListAsync(cancellationToken))
            .Select(x => new TeamSizingPeriod(x.TeamId, x.Start, x.End, x.SizingMethod == SizingMethod.StoryPoints))
            .ToList();

        return new TeamStructure(request.TeamId, teams, subtreeEdges, sizingPeriods);
    }
}
