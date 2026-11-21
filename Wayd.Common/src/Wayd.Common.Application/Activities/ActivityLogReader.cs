using Mapster;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Activities;

/// <inheritdoc cref="IActivityLogReader"/>
public sealed class ActivityLogReader(IActivityLogDbContext dbContext, IKpisDbContext kpisDbContext) : IActivityLogReader
{
    /// <summary>The aggregate type every KPI event is filed under.</summary>
    private const string KpiAggregateType = "Kpi";

    private readonly IActivityLogDbContext _dbContext = dbContext;
    private readonly IKpisDbContext _kpisDbContext = kpisDbContext;

    public Task<PagedResponse<ActivityLogDto>> Read(
        Guid aggregateId,
        string? aggregateType = null,
        PageRequest? paging = null,
        CancellationToken cancellationToken = default) =>
        Read(aggregateId, aggregateType, eventTypes: null, paging ?? new(), cancellationToken);

    public Task<PagedResponse<ActivityLogDto>> ReadEventTypes(
        Guid aggregateId,
        string aggregateType,
        IReadOnlyCollection<string> eventTypes,
        PageRequest? paging = null,
        CancellationToken cancellationToken = default) =>
        Read(aggregateId, aggregateType, eventTypes, paging ?? new(), cancellationToken);

    private async Task<PagedResponse<ActivityLogDto>> Read(
        Guid aggregateId,
        string? aggregateType,
        IReadOnlyCollection<string>? eventTypes,
        PageRequest paging,
        CancellationToken cancellationToken)
    {
        var hasType = !string.IsNullOrWhiteSpace(aggregateType);

        var own = _dbContext.ActivityLogs.Where(a => a.AggregateId == aggregateId);

        var related = _dbContext.ActivityLogs
            .SelectMany(a => a.RelatedAggregates, (a, r) => new { a.Id, r.AggregateId, r.AggregateType })
            .Where(r => r.AggregateId == aggregateId);

        // Filtered here rather than inside the predicates as "no type or this type": that form reaches SQL as
        // an OR on a parameter, which keeps the optimizer from seeking the index on it.
        if (hasType)
        {
            own = own.Where(a => a.AggregateType == aggregateType);
            related = related.Where(r => r.AggregateType == aggregateType);
        }

        // Two id sets unioned rather than one predicate ORing the entry's columns with an EXISTS over its
        // related rows. Each half can seek its own (AggregateId, AggregateType) index; the OR spans two
        // tables, so no single index answers it.
        var query = _dbContext.ActivityLogs
            .AsNoTracking()
            .Where(a => own.Select(o => o.Id).Concat(related.Select(r => r.Id)).Contains(a.Id));

        if (eventTypes is not null)
        {
            var types = eventTypes.ToArray();
            query = query.Where(a => types.Contains(a.EventType));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // The tiebreaks are not a display preference. The events of one command land microseconds apart at
        // best and on the same Timestamp where the caller reads the clock once for a batch, so ordering on it
        // alone leaves rows tied, and Skip/Take over a sort the database is free to break differently per
        // query can return one entry on two pages or on neither.
        // Ordinal settles entries from the same unit of work in the order they were raised; Id settles the
        // rest, arbitrarily but identically on every read, which is all paging needs.
        var items = await query
            .Include(a => a.Employee)
            .OrderByDescending(a => a.Timestamp)
            .ThenByDescending(a => a.Ordinal)
            .ThenByDescending(a => a.Id)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items
            .Select(entry => entry.Adapt<ActivityLogDto>() with
            {
                // Ignoring case to agree with the query, which matched the type under the database's case-insensitive
                // collation; an ordinal compare marks a record's own entries related when the caller's casing differs.
                IsRelated = entry.AggregateId != aggregateId
                    || (hasType && !string.Equals(entry.AggregateType, aggregateType, StringComparison.OrdinalIgnoreCase)),
            })
            .ToList();

        return new PagedResponse<ActivityLogDto>(await NameKpis(dtos, cancellationToken), totalCount, paging.Page, paging.PageSize);
    }

    /// <summary>
    /// Names the KPI each related KPI entry was raised on.
    /// </summary>
    /// <remarks>
    /// Done here rather than by each module reading its record's log: KPIs are a Common model that any module's
    /// record carries, so every record's Activity lists them, and no module knows what a KPI is. A KPI since removed
    /// is left unnamed.
    /// </remarks>
    private async Task<List<ActivityLogDto>> NameKpis(List<ActivityLogDto> dtos, CancellationToken cancellationToken)
    {
        static bool RaisedOnAKpi(ActivityLogDto d) =>
            d.IsRelated && d.RaisedOn is null && string.Equals(d.AggregateType, KpiAggregateType, StringComparison.OrdinalIgnoreCase);

        var kpiIds = dtos
            .Where(RaisedOnAKpi)
            .Select(d => d.AggregateId)
            .Distinct()
            .ToList();

        if (kpiIds.Count == 0)
            return dtos;

        var kpis = await _kpisDbContext.Kpis
            .Where(k => kpiIds.Contains(k.Id))
            .Select(k => new { k.Id, k.Key, k.Name })
            .ToDictionaryAsync(k => k.Id, cancellationToken);

        return [.. dtos.Select(d =>
            RaisedOnAKpi(d) && kpis.TryGetValue(d.AggregateId, out var kpi)
                ? d with { RaisedOn = NavigationDto.Create(kpi.Id, kpi.Key, kpi.Name) }
                : d)];
    }
}
