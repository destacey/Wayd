using Wayd.Common.Application.Dtos;
using Wayd.Organization.Application.Teams.Models;
using NodaTime;

namespace Wayd.Organization.Application.Teams.Queries;

public sealed record GetFunctionalOrganizationChartQuery(LocalDate? AsOfDate = null) : IQuery<FunctionalOrganizationChartDto>;

public sealed class GetFunctionalOrganizationChartQueryHandler(IOrganizationDbContext organizationDbContext, ILogger<GetFunctionalOrganizationChartQueryHandler> logger, IDateTimeProvider dateTimeProvider) : IQueryHandler<GetFunctionalOrganizationChartQuery, FunctionalOrganizationChartDto>
{
    private const string RequestName = nameof(GetFunctionalOrganizationChartQuery);

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
    /// Retrieves the team hierarchy nodes as of the specified date from the database.
    /// </summary>
    /// <param name="asOfDate">The date to retrieve the team hierarchy nodes as of.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A list of team hierarchy nodes.</returns>
    private async Task<List<TeamHierarchyNode>> GetTeamHierarchyNodes(LocalDate asOfDate, CancellationToken cancellationToken)
    {
        return await _organizationDbContext.Database.SqlQuery<TeamHierarchyNode>($@"
            WITH TeamHierarchy AS (
               -- Base nodes (those with no parents as of asOfDate)
               SELECT 
                   node.Id,
                   node.[Key],
                   node.Name,
                   node.Code,
                   CASE node.[Type]
                       WHEN 'Team' THEN 0
                       WHEN 'TeamOfTeams' THEN 1
                       ELSE 0 -- Default to Team if unknown
                   END as [Type],
                   CAST(NULL as uniqueidentifier) as ParentId,
                   CAST(NULL as varchar(128)) as ParentName,
                   CAST(node.Code as varchar(2048)) as Path,
                   0 as Level
               FROM Organization.TeamNodes node
               WHERE {asOfDate} >= node.ActiveDate
               AND ({asOfDate} <= node.InactiveDate OR node.InactiveDate IS NULL)
               AND NOT EXISTS (
                   SELECT 1 
                   FROM Organization.TeamMembershipEdges edge,
                        Organization.TeamNodes parent
                   WHERE MATCH(node-(edge)->parent)
                   AND {asOfDate} >= edge.StartDate 
                   AND ({asOfDate} <= edge.EndDate OR edge.EndDate IS NULL)
               )
               UNION ALL
               -- Recursively find children
               SELECT 
                   child.Id,
                   child.[Key],
                   child.Name,
                   child.Code,
                   CASE child.[Type]
                       WHEN 'Team' THEN 0
                       WHEN 'TeamOfTeams' THEN 1
                       ELSE 0 -- Default to Team if unknown
                   END as [Type],
                   h.Id as ParentId,
                   h.Name as ParentName,
                   CAST(h.Path + ' -> ' + child.Code as varchar(2048)) as Path,
                   h.Level + 1 as Level
               FROM TeamHierarchy h
               CROSS APPLY (
                   SELECT child.Id, child.[Key], child.Name, child.Code, child.[Type]
                   FROM Organization.TeamNodes child,
                        Organization.TeamMembershipEdges edge,
                        Organization.TeamNodes parent
                   WHERE MATCH(child-(edge)->parent)
                       AND parent.Id = h.Id
                       AND {asOfDate} >= edge.StartDate 
                       AND ({asOfDate} <= edge.EndDate OR edge.EndDate IS NULL)
                       AND {asOfDate} >= child.ActiveDate
                       AND ({asOfDate} <= child.InactiveDate OR child.InactiveDate IS NULL)
                       AND {asOfDate} >= parent.ActiveDate
                       AND ({asOfDate} <= parent.InactiveDate OR parent.InactiveDate IS NULL)
               ) child
            )
            SELECT
               Id,
               [Key],
               Name,
               Code,
               [Type],
               ParentId,
               [Level],
               Path
            FROM TeamHierarchy;
            ").ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Converts a flat list of team hierarchy nodes into a hierarchical DTO structure.
    /// </summary>
    /// <param name="nodes">The flat list of team nodes</param>
    /// <returns>A list of root organizational units with their child hierarchies</returns>
    private static List<OrganizationalUnitDto> BuildOrganizationalHierarchy(List<TeamHierarchyNode> nodes)
    {
        return nodes
            .Where(n => n.ParentId == null)
            .OrderBy(n => n.Name)
            .Select(n => ToOrganizationalUnit(n, nodes))
            .ToList();
    }

    /// <summary>
    /// Converts a team hierarchy node and its children into an organizational unit DTO with a nested hierarchy structure.
    /// </summary>
    /// <param name="node">The team hierarchy node to convert.</param>
    /// <param name="allNodes">The complete list of team hierarchy nodes used to find child relationships.</param>
    /// <returns>
    /// An <see cref="OrganizationalUnitDto"/> representing the team hierarchy node and its children.
    /// The Children property will be null if the node has no children.
    /// </returns>
    private static OrganizationalUnitDto ToOrganizationalUnit(TeamHierarchyNode node, List<TeamHierarchyNode> allNodes)
    {
        var childrenByParentId = allNodes
            .Where(n => n.ParentId.HasValue)
            .ToLookup(n => n.ParentId!.Value);

        return BuildUnit(node);

        OrganizationalUnitDto BuildUnit(TeamHierarchyNode currentNode)
        {
            var children = childrenByParentId[currentNode.Id]
                .OrderBy(n => n.Name)
                .Select(BuildUnit)
                .ToList();

            return new OrganizationalUnitDto
            {
                Id = currentNode.Id,
                Key = currentNode.Key,
                Name = currentNode.Name,
                Code = currentNode.Code,
                Type = SimpleNavigationDto.FromEnum(currentNode.Type),
                Level = currentNode.Level,
                Path = currentNode.Path,
                Children = children.Count != 0 ? children : null
            };
        }
    }
}
