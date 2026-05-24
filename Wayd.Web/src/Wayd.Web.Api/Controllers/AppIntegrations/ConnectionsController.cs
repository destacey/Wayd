using CSharpFunctionalExtensions;
using Wayd.AppIntegration.Application.Connections.Commands.AzureDevOps;
using Wayd.AppIntegration.Application.Connections.Commands.AzureOpenAI;
using Wayd.AppIntegration.Application.Connections.Dtos.AzureDevOps;
using Wayd.AppIntegration.Application.Connections.Dtos.AzureOpenAI;
using Wayd.AppIntegration.Domain.Models;
using Wayd.Common.Application.BackgroundJobs;
using Wayd.Common.Application.Enums;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Interfaces;
using Wayd.Web.Api.Models.AppIntegrations.Connections;

namespace Wayd.Web.Api.Controllers.AppIntegrations;

[Route("api/app-integrations/connections")]
[ApiVersionNeutral]
[ApiController]
public class ConnectionsController(ISender sender) : ControllerBase
{
    private readonly ISender _sender = sender;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Connections)]
    [OpenApiOperation("Get list of all connections.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ConnectionListDto>>> GetConnections(
        CancellationToken cancellationToken,
        bool includeDisabled = false)
    {
        var connections = await _sender.Send(new GetConnectionsQuery(includeDisabled), cancellationToken);
        return Ok(connections);
    }

    [HttpGet("connectors")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Connectors)]
    [OpenApiOperation("Get list of all connector types.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ConnectorListDto>>> GetConnectors(CancellationToken cancellationToken)
    {
        var connectors = await _sender.Send(new GetConnectorsQuery(), cancellationToken);
        return Ok(connectors.OrderBy(c => c.Name));
    }

    [HttpGet("{id}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Connections)]
    [OpenApiOperation("Get connection details by ID.", "Returns polymorphic response based on connector type.")]
    [ProducesResponseType(typeof(ConnectionDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConnectionDetailsDto>> GetConnection(Guid id, CancellationToken cancellationToken)
    {
        var connection = await _sender.Send(new GetConnectionQuery(id), cancellationToken);

        if (connection is null)
            return NotFound();

        // Mask sensitive fields
        if (connection is AzureDevOpsConnectionDetailsDto azdo)
        {
            azdo.Configuration.MaskPersonalAccessToken();
        }
        else if (connection is AzureOpenAIConnectionDetailsDto aoai)
        {
            aoai.Configuration.ApiKey = "***MASKED***";
        }

        return this.OkPolymorphic(connection);
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Connections)]
    [OpenApiOperation("Create a new connection.", "Accepts polymorphic request based on connector type.")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Guid))]
    public async Task<ActionResult> CreateConnection(
        [FromBody] CreateConnectionRequest request,
        CancellationToken cancellationToken)
    {
        Result<Guid> result = request switch
        {
            CreateAzureDevOpsConnectionRequest azdo =>
                await _sender.Send(azdo.ToCommand(), cancellationToken),
            CreateAzureOpenAIConnectionRequest aoai =>
                await _sender.Send(aoai.ToCommand(), cancellationToken),
            _ => Result.Failure<Guid>($"Connector type not supported")
        };

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetConnection), new { id = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Connections)]
    [OpenApiOperation("Update a connection.", "Accepts polymorphic request based on connector type.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateConnection(
        Guid id,
        [FromBody] UpdateConnectionRequest request,
        CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        Result result = request switch
        {
            UpdateAzureDevOpsConnectionRequest azdo =>
                await _sender.Send(azdo.ToCommand(), cancellationToken),
            UpdateAzureOpenAIConnectionRequest aoai =>
                await _sender.Send(aoai.ToCommand(), cancellationToken),
            _ => Result.Failure($"Connector type not supported")
        };

        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/run")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Connections)]
    [OpenApiOperation("Trigger a sync for a connection.",
        "Enqueues a background job that runs the sync pipeline for this connection only. Defaults to a differential sync; pass 'syncType=Full' for a full sync.")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public ActionResult RunSync(
        Guid id,
        [FromServices] IJobService jobService,
        [FromServices] IJobManager jobManager,
        CancellationToken cancellationToken,
        SyncType syncType = SyncType.Differential)
    {
        jobService.Enqueue(() => jobManager.RunWorkSync(syncType, SyncTriggerSource.Manual, id, cancellationToken));
        return Accepted();
    }

    [HttpGet("{id}/sync-runs")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Connections)]
    [OpenApiOperation("Get sync run history for a connection.",
        "Returns sync runs for the specified connection, ordered by start time descending. Filtered by 'since' (UTC, defaults to the last 24 hours when omitted).")]
    [ProducesResponseType(typeof(IEnumerable<SyncRunListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SyncRunListDto>>> GetSyncRuns(
        Guid id,
        CancellationToken cancellationToken,
        [FromQuery] DateTime? since = null)
    {
        Instant? sinceInstant = null;
        if (since.HasValue)
        {
            var s = since.Value;
            s = s.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(s, DateTimeKind.Utc) : s.ToUniversalTime();
            sinceInstant = Instant.FromDateTimeUtc(s);
        }

        var runs = await _sender.Send(new GetSyncRunsQuery(id, sinceInstant), cancellationToken);
        return Ok(runs);
    }

    [HttpGet("sync-runs/{syncRunId}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Connections)]
    [OpenApiOperation("Get sync run details.", "Returns full details including per-workspace breakdown for a single sync run.")]
    [ProducesResponseType(typeof(SyncRunDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SyncRunDetailsDto>> GetSyncRun(Guid syncRunId, CancellationToken cancellationToken)
    {
        var run = await _sender.Send(new GetSyncRunQuery(syncRunId), cancellationToken);

        if (run is null)
            return NotFound();

        return Ok(run);
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.Connections)]
    [OpenApiOperation("Delete a connection.", "Works for all connector types.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteConnection(Guid id, CancellationToken cancellationToken)
    {
        // Determine connection type first to dispatch correct command
        var connection = await _sender.Send(new GetConnectionQuery(id), cancellationToken);
        if (connection is null)
            return NotFound();

        Result result = connection switch
        {
            AzureDevOpsConnectionDetailsDto =>
                await _sender.Send(new DeleteAzureDevOpsConnectionCommand(id), cancellationToken),
            AzureOpenAIConnectionDetailsDto =>
                await _sender.Send(new DeleteAzureOpenAIConnectionCommand(id), cancellationToken),
            _ => Result.Failure($"Connector type not supported")
        };

        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
