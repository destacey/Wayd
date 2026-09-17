using Wayd.Common.Application.Dtos;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Organization.Application.Teams.Models;
using NodaTime;

namespace Wayd.Organization.Application.Teams.Queries;

public sealed record GetFunctionalOrganizationChartQuery(LocalDate? AsOfDate = null) : IQuery<FunctionalOrganizationChartDto>;

public sealed class GetFunctionalOrganizationChartQueryHandler(IOrganizationDbContext organizationDbContext, ILogger<GetFunctionalOrganizationChartQueryHandler> logger, IDateTimeProvider dateTimeProvider) : IQueryHandler<GetFunctionalOrganizationChartQuery, FunctionalOrganizationChartDto>
{
    private const string RequestName = nameof(GetFunctionalOrganizationChartQuery);
    private const string PathSeparator = " -> ";

    private readonly IOrganizationDbContext _organizationDbContext = organizationDbContext;
    private readonly ILogger<GetFunctionalOrganizationChartQueryHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<FunctionalOrganizationChartDto> Handle(GetFunctionalOrganizationChartQuery request, CancellationToken cancellationToken)
    {
        var asOfDate = request.AsOfDate ?? _dateTimeProvider.Today;

        _logger.LogInformation("{RequestName}: Retrieving functional organization chart as of {AsOfDate}", RequestName, asOfDate);

        var nodes = await GetTeamHierarchyNodes(asOfDate, cancellationToken);

        var maxDepth = nodes.Count > 0 ? nodes.Max(n => n.Level) : 0;
        _logger.LogDebug("{RequestName}: Retrieved {NodeCount} team hierarchy nodes with a max depth of {MaxDepth}", RequestName, nodes.Count, maxDepth);

        if (nodes.Count == 0)
        {
            _logger.LogWarning("{RequestName}: No team hierarchy nodes found", RequestName);
            return new FunctionalOrganizationChartDto
            {
                AsOfDate = asOfDate,
                Total = 0,
                MaxDepth = 0
            };
        }

        // Convert flat list to hierarchical DTOs
        var rootUnits = BuildOrganizationalHierarchy(nodes);

        _logger.LogDebug("{RequestName}: Built functional organization chart with {RootCount} root organizational units and {TotalCount} overall units", RequestName, rootUnits.Count, nodes.Count);

        return new FunctionalOrganizationChartDto
        {
            AsOfDate = asOfDate,
            Organization = rootUnits,
            Total = nodes.Count,
            MaxDepth = maxDepth
        };
    }

    /// <summary>
    /// Every team active on the date, placed under the parent its membership names on that date. The walk is
    /// done in memory: teams number in the hundreds, and a recursive query is provider-specific SQL for no gain.
    /// </summary>
    /// <remarks>
    /// Every active team is placed. One whose only membership points at a team not itself active on the date
    /// is a root; one that the walk from the roots never reaches — a membership cycle, which only bad data can
    /// produce — is placed as a root too and logged, rather than silently missing from the chart.
    /// </remarks>
    private async Task<List<TeamHierarchyNode>> GetTeamHierarchyNodes(LocalDate asOfDate, CancellationToken cancellationToken)
    {
        var teams = await _organizationDbContext.BaseTeams
            .AsNoTracking()
            .Where(t => !t.IsDeleted
                && t.ActiveDate <= asOfDate
                && (t.InactiveDate == null || asOfDate <= t.InactiveDate))
            .Select(t => new
            {
                t.Id,
                t.Key,
                t.Name,
                t.Code,
                t.Type,
                ParentIds = t.ParentMemberships
                    .Where(m => !m.IsDeleted
                        && m.DateRange.Start <= asOfDate
                        && (m.DateRange.End == null || asOfDate <= m.DateRange.End))
                    .Select(m => m.TargetId)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var activeIds = teams.Select(t => t.Id).ToHashSet();
        var childrenByParentId = teams
            .SelectMany(t => t.ParentIds.Where(activeIds.Contains).Select(parentId => (ParentId: parentId, Child: t)))
            .ToLookup(x => x.ParentId, x => x.Child);

        var nodes = new List<TeamHierarchyNode>(teams.Count);
        var visited = new HashSet<Guid>();
        var pending = new Stack<(Guid Id, int Key, string Name, string Code, TeamType Type, Guid? ParentId, string Path, int Level)>();

        // Pre-order from every root. A team is placed once: the domain allows one active parent at a time
        // and forbids cycles, so a second visit is stale data and is skipped rather than looped over.
        foreach (var root in teams.Where(t => !t.ParentIds.Any(activeIds.Contains)))
        {
            pending.Push((root.Id, root.Key, root.Name, root.Code.Value, root.Type, null, root.Code.Value, 0));
        }
        Drain();

        // A cycle has no root, so nothing above reaches it. Each member gets placed as a root of its own —
        // the tree is wrong for them, but a team that exists is on the chart.
        var unplaced = teams.Where(t => !visited.Contains(t.Id)).ToList();
        if (unplaced.Count > 0)
        {
            _logger.LogWarning("{RequestName}: {Count} teams are in a membership cycle as of {AsOfDate} and are shown as roots: {Codes}",
                RequestName, unplaced.Count, asOfDate, unplaced.Select(t => t.Code.Value));
            foreach (var team in unplaced)
            {
                pending.Push((team.Id, team.Key, team.Name, team.Code.Value, team.Type, null, team.Code.Value, 0));
                Drain();
            }
        }

        return nodes;

        void Drain()
        {
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (!visited.Add(current.Id))
                    continue;

                nodes.Add(new TeamHierarchyNode
                {
                    Id = current.Id,
                    Key = current.Key,
                    Name = current.Name,
                    Code = current.Code,
                    Type = current.Type,
                    ParentId = current.ParentId,
                    Path = current.Path,
                    Level = current.Level
                });

                foreach (var child in childrenByParentId[current.Id])
                {
                    pending.Push((child.Id, child.Key, child.Name, child.Code.Value, child.Type, current.Id, current.Path + PathSeparator + child.Code.Value, current.Level + 1));
                }
            }
        }
    }

    /// <summary>
    /// Converts a flat list of team hierarchy nodes into a hierarchical DTO structure.
    /// </summary>
    /// <param name="nodes">The flat list of team nodes</param>
    /// <returns>A list of root organizational units with their child hierarchies</returns>
    private static List<OrganizationalUnitDto> BuildOrganizationalHierarchy(List<TeamHierarchyNode> nodes)
    {
        var childrenByParentId = nodes
            .Where(n => n.ParentId.HasValue)
            .ToLookup(n => n.ParentId!.Value);

        return nodes
            .Where(n => n.ParentId == null)
            .OrderBy(n => n.Name)
            .Select(n => ToOrganizationalUnit(n, childrenByParentId))
            .ToList();
    }

    /// <summary>
    /// Converts a team hierarchy node and its children into an organizational unit DTO with a nested hierarchy structure.
    /// The Children property is null when the node has no children.
    /// </summary>
    private static OrganizationalUnitDto ToOrganizationalUnit(TeamHierarchyNode node, ILookup<Guid, TeamHierarchyNode> childrenByParentId)
    {
        var children = childrenByParentId[node.Id]
            .OrderBy(n => n.Name)
            .Select(n => ToOrganizationalUnit(n, childrenByParentId))
            .ToList();

        return new OrganizationalUnitDto
        {
            Id = node.Id,
            Key = node.Key,
            Name = node.Name,
            Code = node.Code,
            Type = SimpleNavigationDto.FromEnum(node.Type),
            Level = node.Level,
            Path = node.Path,
            Children = children.Count != 0 ? children : null
        };
    }
}
