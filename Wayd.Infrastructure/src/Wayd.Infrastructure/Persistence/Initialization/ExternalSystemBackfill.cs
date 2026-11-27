using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wayd.AppIntegration.Application.Connections.ExternalSystems;
using Wayd.AppIntegration.Domain.Interfaces;

namespace Wayd.Infrastructure.Persistence.Initialization;

/// <summary>
/// Ties every syncable connection that knows its system id, deleted ones included, to its
/// <see cref="ExternalSystem"/>, creating the systems it needs. Runs on every boot and finds nothing once every
/// connection is linked.
/// </summary>
/// <remarks>
/// Done in code rather than in a migration because Entra and Workday system ids live in the encrypted
/// <c>Configuration</c> column, which SQL cannot read.
/// </remarks>
internal sealed class ExternalSystemBackfill
{
    private readonly WaydDbContext _dbContext;
    private readonly IExternalSystemLinker _linker;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<ExternalSystemBackfill> _logger;

    public ExternalSystemBackfill(
        WaydDbContext dbContext,
        IExternalSystemLinker linker,
        IDateTimeProvider dateTimeProvider,
        ILogger<ExternalSystemBackfill> logger)
    {
        _dbContext = dbContext;
        _linker = linker;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    /// <summary>Links the connections that are not yet linked and saves them together.</summary>
    public async Task Run(CancellationToken cancellationToken)
    {
        var unlinked = await _dbContext.Connections
            .IgnoreQueryFilters()
            .Where(c => c.ExternalSystemId == null)
            .ToListAsync(cancellationToken);

        var candidates = unlinked
            .Where(c => c is ISyncableConnection { SystemId: not null and not "" })
            .ToList();
        if (candidates.Count == 0)
            return;

        var timestamp = _dateTimeProvider.Now;
        var linked = 0;
        foreach (var connection in candidates)
        {
            var result = await _linker.Link(connection, EventActor.System, timestamp, cancellationToken);
            if (result.IsFailure)
            {
                _logger.LogWarning("ExternalSystemBackfill: unable to link connection {ConnectionId} to its outside system. {Error}", connection.Id, result.Error);
                continue;
            }

            linked++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("ExternalSystemBackfill: linked {Count} connection(s) to their outside systems.", linked);
    }
}
