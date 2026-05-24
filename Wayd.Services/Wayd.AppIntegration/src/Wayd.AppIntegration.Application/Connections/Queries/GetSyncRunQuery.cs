using System.Text.Json;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Wayd.AppIntegration.Application.Connections.Queries;

public sealed record GetSyncRunQuery(Guid SyncRunId) : IQuery<SyncRunDetailsDto?>;

internal sealed class GetSyncRunQueryHandler(IAppIntegrationDbContext db, ILogger<GetSyncRunQueryHandler> logger) : IQueryHandler<GetSyncRunQuery, SyncRunDetailsDto?>
{
    private readonly IAppIntegrationDbContext _db = db;
    private readonly ILogger<GetSyncRunQueryHandler> _logger = logger;

    public async Task<SyncRunDetailsDto?> Handle(GetSyncRunQuery request, CancellationToken cancellationToken)
    {
        var run = await _db.SyncRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.SyncRunId, cancellationToken);

        if (run is null)
            return null;

        var dto = run.Adapt<SyncRunDetailsDto>();

        if (run.DetailsJson is not null)
        {
            try
            {
                dto = dto with
                {
                    Details = JsonSerializer.Deserialize<List<WorkspaceSyncDetail>>(run.DetailsJson)
                              ?? []
                };
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize DetailsJson for SyncRun {SyncRunId}.", run.Id);
            }
        }

        return dto;
    }
}
