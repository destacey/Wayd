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

        var query = _dbContext.ActivityLogs
            .AsNoTracking()
            .Where(a => a.AggregateId == aggregateId);

        if (!string.IsNullOrWhiteSpace(aggregateType))
        {
            query = query.Where(a => a.AggregateType == aggregateType);
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
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return new PagedResponse<ActivityLogDto>(items.Adapt<List<ActivityLogDto>>(), totalCount, pageNumber, take);
    }
}
