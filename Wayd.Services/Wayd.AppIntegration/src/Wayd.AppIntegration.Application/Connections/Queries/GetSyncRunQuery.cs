using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Wayd.AppIntegration.Application.Connections.Queries;

public sealed record GetSyncRunQuery(Guid SyncRunId) : IQuery<SyncRunDetailsDto?>;

internal sealed class GetSyncRunQueryHandler(IAppIntegrationDbContext db) : IQueryHandler<GetSyncRunQuery, SyncRunDetailsDto?>
{
    private readonly IAppIntegrationDbContext _db = db;

    public async Task<SyncRunDetailsDto?> Handle(GetSyncRunQuery request, CancellationToken cancellationToken)
    {
        var run = await _db.SyncRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.SyncRunId, cancellationToken);

        // DetailsJson flows through as a string; the consumer (a per-connector frontend view)
        // is responsible for parsing it against the schema it knows.
        return run?.Adapt<SyncRunDetailsDto>();
    }
}
