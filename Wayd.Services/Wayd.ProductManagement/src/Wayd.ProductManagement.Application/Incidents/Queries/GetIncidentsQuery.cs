using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.ProductManagement.Application.Incidents.Dtos;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Incidents.Queries;

public sealed record GetIncidentsQuery(PageRequest Paging) : IQuery<PagedResponse<IncidentSummaryDto>>, IPagedQuery;

public sealed class GetIncidentsQueryValidator : PagedQueryValidator<GetIncidentsQuery>;

public sealed class GetIncidentsQueryHandler(IProductManagementDbContext context)
    : IQueryHandler<GetIncidentsQuery, PagedResponse<IncidentSummaryDto>>
{
    private readonly IProductManagementDbContext _context = context;

    public async Task<PagedResponse<IncidentSummaryDto>> Handle(
        GetIncidentsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.Incidents.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);

        if (totalCount == 0)
        {
            return new PagedResponse<IncidentSummaryDto>([], 0, request.Paging.Page, request.Paging.PageSize);
        }

        var page = await (
            from incident in query
            join commander in _context.Employees on incident.CommanderEmployeeId equals commander.Id
            join level in _context.IncidentSeverityLevels on incident.SeverityLevelId equals level.Id
            orderby incident.Key descending
            select new { incident, commander, level })
            .Skip(request.Paging.Skip)
            .Take(request.Paging.PageSize)
            .ToListAsync(cancellationToken);

        var items = page
            .Select(row => IncidentProjections.Summary(row.incident, row.commander, row.level))
            .ToList();

        return new PagedResponse<IncidentSummaryDto>(
            items,
            totalCount,
            request.Paging.Page,
            request.Paging.PageSize);
    }
}

public sealed record GetIncidentQuery : IQuery<IncidentDto?>
{
    public GetIncidentQuery(IdOrKey idOrKey)
    {
        IdOrKeyFilter = idOrKey.CreateFilter<Incident>();
    }

    public Expression<Func<Incident, bool>> IdOrKeyFilter { get; }
}

public sealed class GetIncidentQueryHandler(IProductManagementDbContext context)
    : IQueryHandler<GetIncidentQuery, IncidentDto?>
{
    private readonly IProductManagementDbContext _context = context;

    public async Task<IncidentDto?> Handle(GetIncidentQuery query, CancellationToken cancellationToken)
    {
        var incident = await _context.Incidents
            .AsNoTracking()
            .Include(i => i.ProductLinks)
            .Where(query.IdOrKeyFilter)
            .FirstOrDefaultAsync(cancellationToken);

        if (incident is null)
        {
            return null;
        }

        var commander = await _context.Employees
            .AsNoTracking()
            .FirstAsync(e => e.Id == incident.CommanderEmployeeId, cancellationToken);

        var level = await _context.IncidentSeverityLevels
            .AsNoTracking()
            .FirstAsync(l => l.Id == incident.SeverityLevelId, cancellationToken);

        var productIds = incident.ProductLinks.Select(l => l.ProductId).ToList();
        var products = productIds.Count == 0
            ? []
            : await _context.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToListAsync(cancellationToken);

        return IncidentProjections.Details(incident, commander, level, products);
    }
}
