using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Requests.AppIntegration;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.Incidents.Dtos;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Incidents.Queries;

public sealed record GetIncidentsQuery(
    PageRequest Paging,
    IReadOnlyCollection<ProductStatusAlias>? Statuses = null,
    IReadOnlyCollection<Guid>? SeverityLevelIds = null) : IQuery<PagedResponse<IncidentSummaryDto>>, IPagedQuery;

public sealed class GetIncidentsQueryValidator : PagedQueryValidator<GetIncidentsQuery>;

public sealed class GetIncidentsQueryHandler(IProductManagementDbContext context)
    : IQueryHandler<GetIncidentsQuery, PagedResponse<IncidentSummaryDto>>
{
    private readonly IProductManagementDbContext _context = context;

    public async Task<PagedResponse<IncidentSummaryDto>> Handle(
        GetIncidentsQuery request,
        CancellationToken cancellationToken)
    {
        var query = IncidentQuery.Matching(
            _context.Incidents.AsNoTracking(),
            request.Statuses,
            request.SeverityLevelIds);

        var totalCount = await query.CountAsync(cancellationToken);

        if (totalCount == 0)
        {
            return new PagedResponse<IncidentSummaryDto>([], 0, request.Paging.Page, request.Paging.PageSize);
        }

        var page = await (
            from incident in query
            join submittedBy in _context.Employees on incident.SubmittedByEmployeeId equals submittedBy.Id
            join reportedBy in _context.Employees on incident.ReportedByEmployeeId equals reportedBy.Id
            join commander in _context.Employees on incident.CommanderEmployeeId equals (Guid?)commander.Id into commanders
            from commander in commanders.DefaultIfEmpty()
            join level in _context.IncidentSeverityLevels on incident.SeverityLevelId equals level.Id
            orderby incident.Key descending
            select new { incident, submittedBy, reportedBy, commander, level })
            .Skip(request.Paging.Skip)
            .Take(request.Paging.PageSize)
            .ToListAsync(cancellationToken);

        var incidentIds = page.Select(row => row.incident.Id).ToList();
        var incidentsWithLinks = await _context.Incidents
            .AsNoTracking()
            .Include(i => i.ProductLinks)
            .Include(i => i.ReleaseLinks)
            .Where(i => incidentIds.Contains(i.Id))
            .ToListAsync(cancellationToken);
        var byId = incidentsWithLinks.ToDictionary(i => i.Id);
        var productIds = incidentsWithLinks
            .SelectMany(i => i.ProductLinks.Select(link => link.ProductId))
            .Distinct()
            .ToList();
        var productsById = productIds.Count == 0
            ? new Dictionary<Guid, Product>()
            : await _context.Products
                .AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken);
        var releaseIds = incidentsWithLinks
            .SelectMany(i => i.ReleaseLinks.Select(link => link.ReleaseId))
            .Distinct()
            .ToList();
        var releasesById = releaseIds.Count == 0
            ? new Dictionary<Guid, Release>()
            : await _context.Releases
                .AsNoTracking()
                .Where(r => releaseIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, cancellationToken);

        var items = page
            .Select(row =>
            {
                var incident = byId.GetValueOrDefault(row.incident.Id);
                var products = incident?.ProductLinks.Select(l => productsById[l.ProductId]).ToList() ?? [];
                var releases = incident?.ReleaseLinks.Select(l => releasesById[l.ReleaseId]).ToList() ?? [];
                return IncidentProjections.Summary(
                    row.incident,
                    row.submittedBy,
                    row.reportedBy,
                    row.commander,
                    row.level,
                    products,
                    releases);
            })
            .ToList();

        return new PagedResponse<IncidentSummaryDto>(items, totalCount, request.Paging.Page, request.Paging.PageSize);
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

public sealed class GetIncidentQueryHandler(
    IProductManagementDbContext context,
    IDispatcher dispatcher,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetIncidentQuery, IncidentDto?>
{
    private readonly IProductManagementDbContext _context = context;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<IncidentDto?> Handle(GetIncidentQuery query, CancellationToken cancellationToken)
    {
        var incident = await _context.Incidents
            .AsNoTracking()
            .Include(i => i.ProductLinks)
            .Include(i => i.ReleaseLinks)
            .Where(query.IdOrKeyFilter)
            .FirstOrDefaultAsync(cancellationToken);

        if (incident is null)
        {
            return null;
        }

        var peopleIds = new[]
            {
                incident.SubmittedByEmployeeId,
                incident.ReportedByEmployeeId,
                incident.CommanderEmployeeId,
            }
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var people = await _context.Employees
            .AsNoTracking()
            .Where(e => peopleIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, cancellationToken);

        var level = await _context.IncidentSeverityLevels
            .AsNoTracking()
            .FirstAsync(l => l.Id == incident.SeverityLevelId, cancellationToken);

        var productIds = incident.ProductLinks.Select(l => l.ProductId).ToList();
        var products = productIds.Count == 0
            ? []
            : await _context.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToListAsync(cancellationToken);

        var releaseIds = incident.ReleaseLinks.Select(l => l.ReleaseId).ToList();
        var releases = releaseIds.Count == 0
            ? []
            : await _context.Releases.AsNoTracking().Where(r => releaseIds.Contains(r.Id)).ToListAsync(cancellationToken);

        var connections = await _dispatcher.Send(new GetActiveCommunicationConnectionsQuery(), cancellationToken);
        var teamsConnectorConfigured = connections.Any(connection =>
            connection.Connector == Connector.Teams
            && connection.IsActive
            && connection.IsValidConfiguration);

        var durations = IncidentDurations.Measure(
            new IncidentDurations.Row(
                incident.Key,
                incident.Title,
                incident.NoticedAt,
                incident.StartedAt,
                incident.RecoveredAt,
                incident.StatusAlias,
                incident.StatusCategory),
            _dateTimeProvider.Now);

        return IncidentProjections.Details(
            incident,
            people[incident.SubmittedByEmployeeId],
            people[incident.ReportedByEmployeeId],
            incident.CommanderEmployeeId is Guid commanderId ? people[commanderId] : null,
            level,
            products,
            releases,
            durations,
            teamsConnectorConfigured);
    }
}
