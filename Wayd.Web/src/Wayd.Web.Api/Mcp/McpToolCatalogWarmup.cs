using Wayd.Infrastructure;

namespace Wayd.Web.Api.Mcp;

/// <summary>
/// Builds the <see cref="McpToolCatalog"/> in the background as the host starts, so the first MCP connection
/// does not pay for generating the OpenAPI document.
/// </summary>
/// <remarks>
/// Building it takes seconds, and a client's first request on connecting (<c>server/discover</c>) waits for it;
/// a client gives up on that request after a few seconds, and an SDK client that is told 500 then fails to
/// connect at all. A host that only boots to be introspected (<see cref="HostIntrospection"/>) skips the work.
/// </remarks>
public sealed partial class McpToolCatalogWarmup(
    McpToolCatalog catalog,
    IConfiguration configuration,
    ILogger<McpToolCatalogWarmup> logger) : BackgroundService
{
    /// <summary>Builds the catalog once, logging rather than throwing if it cannot be built.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (HostIntrospection.SkipsDatabaseInitialization(configuration))
            return;

        try
        {
            var tools = await catalog.GetToolsets().WaitAsync(stoppingToken);
            LogBuilt(tools.Count);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            // The first MCP request rebuilds nothing: it awaits the same faulted build and reports the error there.
            LogFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Built the MCP tool catalog: {Count} tools.")]
    private partial void LogBuilt(int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not build the MCP tool catalog; MCP connections will fail until this is fixed.")]
    private partial void LogFailed(Exception ex);
}
