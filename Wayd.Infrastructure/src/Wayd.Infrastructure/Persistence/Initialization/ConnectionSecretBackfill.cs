using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;
using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.AppIntegration.Domain.Models.OpenAI;

namespace Wayd.Infrastructure.Persistence.Initialization;

/// <summary>
/// One-shot backfill that re-saves any connection whose secret fields are still
/// plaintext after the data-protection feature shipped.
///
/// Idempotent: detects already-encrypted rows by scanning the raw Configuration
/// column for the protector's version tag, so re-running this on every boot is
/// a no-op once every row is encrypted.
/// </summary>
internal sealed class ConnectionSecretBackfill
{
    // Must match AesGcmSecretProtector's version tag. Kept as a literal so the
    // backfill can SQL-LIKE the raw column without taking a dependency on the
    // protector's internals.
    private const string ProtectedMarker = "wayd1:";

    private readonly WaydDbContext _dbContext;
    private readonly ILogger<ConnectionSecretBackfill> _logger;

    public ConnectionSecretBackfill(
        WaydDbContext dbContext,
        ILogger<ConnectionSecretBackfill> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Run(CancellationToken cancellationToken)
    {
        var legacyIds = await FindLegacyConnectionIds(cancellationToken);
        if (legacyIds.Count == 0) return;

        _logger.LogInformation(
            "ConnectionSecretBackfill: found {Count} connection row(s) with plaintext secrets; encrypting now.",
            legacyIds.Count);

        await BackfillType<AzureDevOpsBoardsConnection>(legacyIds, cancellationToken);
        await BackfillType<AzureOpenAIConnection>(legacyIds, cancellationToken);
        await BackfillType<OpenAIConnection>(legacyIds, cancellationToken);
        await BackfillType<EntraConnection>(legacyIds, cancellationToken);
    }

    private async Task<HashSet<Guid>> FindLegacyConnectionIds(CancellationToken cancellationToken)
    {
        var result = new HashSet<Guid>();
        var connection = _dbContext.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                "SELECT Id FROM [AppIntegrations].[Connections] " +
                "WHERE [Configuration] IS NOT NULL AND [Configuration] NOT LIKE @marker";
            var p = cmd.CreateParameter();
            p.ParameterName = "@marker";
            p.Value = $"%{ProtectedMarker}%";
            cmd.Parameters.Add(p);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(reader.GetGuid(0));
            }
        }
        catch (SqlException ex) when (ex.Number == 208 /* Invalid object name */
                                       || ex.Number == 207 /* Invalid column name */)
        {
            // Schema not yet present (first migration is creating it now). Nothing to backfill.
            _logger.LogDebug("ConnectionSecretBackfill: Connections table not present yet, skipping.");
        }
        finally
        {
            if (wasClosed) await connection.CloseAsync();
        }
        return result;
    }

    private async Task BackfillType<TConnection>(
        IReadOnlyCollection<Guid> legacyIds,
        CancellationToken cancellationToken)
        where TConnection : Connection
    {
        var rows = await _dbContext.Set<TConnection>()
            .IgnoreQueryFilters()
            .Where(c => legacyIds.Contains(c.Id))
            .ToListAsync(cancellationToken);
        if (rows.Count == 0) return;

        foreach (var row in rows)
        {
            // Mark Configuration modified so EF re-runs the encrypting converter on save.
            _dbContext.Entry(row).Property("Configuration").IsModified = true;
        }

        var saved = await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "ConnectionSecretBackfill: encrypted secrets for {Count} {Type} row(s).",
            saved, typeof(TConnection).Name);
    }
}
