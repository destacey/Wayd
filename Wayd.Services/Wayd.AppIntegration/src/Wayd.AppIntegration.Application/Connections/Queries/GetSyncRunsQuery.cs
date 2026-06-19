using Mapster;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Wayd.AppIntegration.Application.Connections.Queries;

public sealed record GetSyncRunsQuery(Guid ConnectionId, Instant? Since = null) : IQuery<IReadOnlyList<SyncRunListDto>>;

internal sealed class GetSyncRunsQueryHandler(IAppIntegrationDbContext db, IDateTimeProvider clock) : IQueryHandler<GetSyncRunsQuery, IReadOnlyList<SyncRunListDto>>
{
    private readonly IAppIntegrationDbContext _db = db;
    private readonly IDateTimeProvider _clock = clock;

    public async Task<IReadOnlyList<SyncRunListDto>> Handle(GetSyncRunsQuery request, CancellationToken cancellationToken)
    {
        var since = request.Since ?? _clock.Now.Minus(Duration.FromHours(24));

        return await _db.SyncRuns
            .AsNoTracking()
            .Where(r => r.ConnectionId == request.ConnectionId && r.StartedAt >= since)
            .OrderByDescending(r => r.StartedAt)
            .ProjectToType<SyncRunListDto>()
            .ToListAsync(cancellationToken);
    }
}
