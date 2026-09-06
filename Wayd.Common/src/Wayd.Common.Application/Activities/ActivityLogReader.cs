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

        var items = await query
            .Include(a => a.Employee)
            .OrderByDescending(a => a.Timestamp)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return new PagedResponse<ActivityLogDto>(items.Adapt<List<ActivityLogDto>>(), totalCount, pageNumber, take);
    }
}
