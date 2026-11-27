using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Incidents.Dtos;

namespace Wayd.ProductManagement.Application.Incidents.Queries;

/// <summary>
/// The active incidents on the Proposed or Active products the teams are linked to, most severe first, then the
/// longest active.
/// </summary>
/// <remarks>
/// Active is <see cref="IncidentQuery.Active"/>, the rule the products and releases grids' Incident column uses.
/// The products are those <c>GetTeamsProductCountQuery</c> counts — direct links, Proposed or Active — so the home
/// page, which shows its incidents widget only while that count is above zero, never hides an incident this
/// returns. The caller supplies the teams, since Product Management holds no memberships.
/// </remarks>
/// <param name="TeamIds">The ids of the teams and teams of teams.</param>
public sealed record GetTeamsActiveIncidentsQuery(IReadOnlyCollection<Guid> TeamIds)
    : IQuery<IReadOnlyList<TeamProductIncidentDto>>;

/// <summary>Reads the active incidents on the teams' products, with the teams' products each one is on.</summary>
public sealed class GetTeamsActiveIncidentsQueryHandler(IProductManagementDbContext context)
    : IQueryHandler<GetTeamsActiveIncidentsQuery, IReadOnlyList<TeamProductIncidentDto>>
{
    private readonly IProductManagementDbContext _context = context;

    public async Task<IReadOnlyList<TeamProductIncidentDto>> Handle(
        GetTeamsActiveIncidentsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.TeamIds.Count == 0)
        {
            return [];
        }

        var teamIds = query.TeamIds;
        var productIds = await _context.Products
            .AsNoTracking()
            .Where(p => p.StatusCategory == StatusCategory.Proposed || p.StatusCategory == StatusCategory.Active)
            .Where(p => p.TeamLinks.Any(l => teamIds.Contains(l.TeamId)))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (productIds.Count == 0)
        {
            return [];
        }

        var incidents = await IncidentQuery.Active(_context.Incidents.AsNoTracking())
            .Where(i => i.ProductLinks.Any(l => productIds.Contains(l.ProductId)))
            .Include(i => i.SeverityLevel)
            .Include(i => i.ProductLinks).ThenInclude(l => l.Product)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return [.. incidents
            .Select(incident => new TeamProductIncidentDto
            {
                Id = incident.Id,
                Key = incident.Key,
                Title = incident.Title,
                NoticedAt = incident.NoticedAt,
                SeverityLevel = IncidentProjections.SeverityLevel(
                    incident.SeverityLevel ?? throw new InvalidOperationException(
                        "The incident query must include SeverityLevel before projecting it.")),
                Status = IncidentProjections.Status(incident),
                Products = [.. incident.ProductLinks
                    .Where(l => l.Product is not null && productIds.Contains(l.ProductId))
                    .Select(l => IncidentProjections.Product(l.Product!))
                    .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => p.Key)],
            })
            .OrderBy(i => i.SeverityLevel.SortOrder)
            .ThenBy(i => i.NoticedAt)
            .ThenBy(i => i.Key)];
    }
}
