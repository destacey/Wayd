using Mapster;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Activities;

/// <inheritdoc cref="IActivityLogReader"/>
public sealed class ActivityLogReader(IActivityLogDbContext dbContext) : IActivityLogReader
{
    private readonly IActivityLogDbContext _dbContext = dbContext;

    public async Task<PagedResponse<ActivityLogDto>> Read(
        Guid aggregateId,
        string? aggregateType = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = Math.Max(1, page);
        var take = Math.Clamp(pageSize, 1, 100);
        var skip = (pageNumber - 1) * take;

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
            .Skip(skip)
            .Take(take)
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

        return new PagedResponse<ActivityLogDto>(dtos, totalCount, pageNumber, take);
    }
}
