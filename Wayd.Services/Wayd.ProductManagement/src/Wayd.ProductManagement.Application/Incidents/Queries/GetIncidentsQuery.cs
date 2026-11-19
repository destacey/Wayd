using System.Linq.Expressions;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Requests.AppIntegration;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.ProductManagement.Application.Incidents.Dtos;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Incidents.Queries;

/// <summary>
/// A page of incidents matching the state and severity filters, filtered and sorted by <see cref="Fields"/>,
/// highest key first by default.
/// </summary>
public sealed record GetIncidentsQuery(
    PageRequest Paging,
    IReadOnlyCollection<ProductStatusAlias>? Statuses = null,
    IReadOnlyCollection<Guid>? SeverityLevelIds = null,
    string? Filter = null,
    string? Sort = null) : IQuery<PagedResponse<IncidentSummaryDto>>, IFilteredPagedQuery
{
    /// <summary>
    /// What a caller may filter and sort incidents by, and the fields whose values
    /// <see cref="GetIncidentFilterOptionsQuery"/> lists. Each is named as the incidents grid's column id. People,
    /// products, releases and severity are matched by id; people sort by name and severity by its rank.
    /// </summary>
    public static ListFieldMap<Incident> Fields { get; } =
        ListFieldMap<Incident>.Create("key desc", i => i.Id)
            .Map("key", i => i.Key)
            .Map("title", i => i.Title)
            .Map("severity", i => i.SeverityLevelId, i => i.SeverityLevel!.SortOrder)
            .Map("state", i => i.StatusName)
            .MapMany("products", i => i.ProductLinks.Select(l => l.ProductId))
            .MapMany("releases", i => i.ReleaseLinks.Select(l => l.ReleaseId))
            .Map("reportedBy", i => i.ReportedByEmployeeId, i => i.ReportedBy!.Name.FirstName + " " + i.ReportedBy.Name.LastName)
            .Map(
                "commander",
                i => i.CommanderEmployeeId,
                i => i.Commander == null ? null : i.Commander.Name.FirstName + " " + i.Commander.Name.LastName)
            .Map("noticedAt", i => i.NoticedAt)
            .Options("severity", incidents => incidents.Select(i => new ListFilterOptionRow<Guid>
            {
                Value = i.SeverityLevelId,
                Label = i.SeverityLevel!.Name,
                Rank = i.SeverityLevel.SortOrder,
            }))
            .Options("state", incidents => incidents.Select(i => new ListFilterOptionRow<string>
            {
                Value = i.StatusName,
                Label = i.StatusName,
                Rank = 0,
            }))
            .Options("products", incidents => incidents
                .SelectMany(i => i.ProductLinks)
                .Select(l => new ListFilterOptionRow<Guid> { Value = l.ProductId, Label = l.Product!.Name, Rank = 0 }))
            .Options("releases", incidents => incidents
                .SelectMany(i => i.ReleaseLinks)
                .Select(l => new ListFilterOptionRow<Guid>
                {
                    Value = l.ReleaseId,
                    Label = string.IsNullOrWhiteSpace(l.Release!.Name) ? l.Release.Version : l.Release.Name,
                    Rank = 0,
                }))
            .Options("reportedBy", incidents => incidents.Select(i => new ListFilterOptionRow<Guid>
            {
                Value = i.ReportedByEmployeeId,
                Label = i.ReportedBy!.Name.FirstName + " " + i.ReportedBy.Name.LastName,
                Rank = 0,
            }))
            .Options("commander", incidents => incidents
                .Where(i => i.Commander != null)
                .Select(i => new ListFilterOptionRow<Guid>
                {
                    Value = i.Commander!.Id,
                    Label = i.Commander.Name.FirstName + " " + i.Commander.Name.LastName,
                    Rank = 0,
                }));
}

/// <summary>Bounds the page a caller may ask for, and refuses a filter or sort incidents cannot take.</summary>
public sealed class GetIncidentsQueryValidator()
    : FilteredPagedQueryValidator<GetIncidentsQuery, Incident>(GetIncidentsQuery.Fields);

public sealed class GetIncidentsQueryHandler(IProductManagementDbContext context)
    : IQueryHandler<GetIncidentsQuery, PagedResponse<IncidentSummaryDto>>
{
    private readonly IProductManagementDbContext _context = context;

    public async Task<PagedResponse<IncidentSummaryDto>> Handle(
        GetIncidentsQuery request,
        CancellationToken cancellationToken)
    {
        var query = GetIncidentsQuery.Fields.ApplyFilter(
            IncidentQuery.Matching(_context.Incidents.AsNoTracking(), request.Statuses, request.SeverityLevelIds),
            request.Filter);

        var totalCount = await query.CountAsync(cancellationToken);

        if (totalCount == 0)
        {
            return new PagedResponse<IncidentSummaryDto>([], 0, request.Paging.Page, request.Paging.PageSize);
        }

        var page = await GetIncidentsQuery.Fields.ApplySort(query, request.Sort)
            .Include(i => i.SubmittedBy)
            .Include(i => i.ReportedBy)
            .Include(i => i.Commander)
            .Include(i => i.SeverityLevel)
            .Include(i => i.ProductLinks).ThenInclude(l => l.Product)
            .Include(i => i.ReleaseLinks).ThenInclude(l => l.Release)
            .AsSplitQuery()
            .Skip(request.Paging.Skip)
            .Take(request.Paging.PageSize)
            .ToListAsync(cancellationToken);

        var items = page
            .Select(incident => IncidentProjections.Summary(
                incident,
                incident.SeverityLevel ?? throw new InvalidOperationException(
                    "The incident query must include SeverityLevel before projecting it."),
                [.. incident.ProductLinks.Where(l => l.Product is not null).Select(l => l.Product!)],
                [.. incident.ReleaseLinks.Where(l => l.Release is not null).Select(l => l.Release!)]))
            .ToList();

        return new PagedResponse<IncidentSummaryDto>(items, totalCount, request.Paging.Page, request.Paging.PageSize);
    }
}

/// <summary>
/// The values one incidents grid column holds across every incident, for its set filter: at most
/// <see cref="Limit"/>, whose labels contain <see cref="Search"/> when one is given.
/// </summary>
public sealed record GetIncidentFilterOptionsQuery(string Field, string? Search = null, int Limit = 100)
    : IQuery<ListFilterOptionsDto>, IListFilterOptionsQuery;

/// <summary>Refuses a field whose values incidents do not list, and a limit or search out of bounds.</summary>
public sealed class GetIncidentFilterOptionsQueryValidator()
    : ListFilterOptionsQueryValidator<GetIncidentFilterOptionsQuery, Incident>(GetIncidentsQuery.Fields);

public sealed class GetIncidentFilterOptionsQueryHandler(IProductManagementDbContext context)
    : IQueryHandler<GetIncidentFilterOptionsQuery, ListFilterOptionsDto>
{
    private readonly IProductManagementDbContext _context = context;

    public Task<ListFilterOptionsDto> Handle(GetIncidentFilterOptionsQuery request, CancellationToken cancellationToken) =>
        GetIncidentsQuery.Fields.ReadOptions(
            _context.Incidents.AsNoTracking(),
            request.Field,
            request.Search,
            request.Limit,
            cancellationToken);
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
            .Include(i => i.SubmittedBy)
            .Include(i => i.ReportedBy)
            .Include(i => i.Commander)
            .Include(i => i.ProductLinks)
            .Include(i => i.ReleaseLinks)
            .Where(query.IdOrKeyFilter)
            .FirstOrDefaultAsync(cancellationToken);

        if (incident is null)
        {
            return null;
        }

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
            level,
            products,
            releases,
            durations,
            teamsConnectorConfigured);
    }
}
