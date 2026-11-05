using Wayd.AppIntegration.Application.Connections.Commands.AzureDevOps;
using Wayd.AppIntegration.Application.Connections.Dtos.AzureDevOps;
using Wayd.AppIntegration.Application.Connections.Queries.AzureDevOps;
using Wayd.AppIntegration.Application.Interfaces;
using Wayd.AppIntegration.Domain.Models;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Organization.Application.BaseTeams.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.AppIntegrations.Connections;

namespace Wayd.Web.Api.Controllers.AppIntegrations;

[Route("api/app-integrations/connections/azure-devops")]
[ApiVersionNeutral]
[ApiController]
public class AzureDevOpsConnectionsController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpPost("{id}/sync-organization")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Connections)]
    [EndpointSummary("Sync Azure DevOps organization processes and projects.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SyncOrganizationConfiguration(
        Guid id,
        [FromServices] IAzureDevOpsInitManager azureDevOpsInitManager,
        CancellationToken cancellationToken)
    {
        var result = await azureDevOpsInitManager.SyncOrganizationConfiguration(id, cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/init-workspace")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Connections)]
    [EndpointSummary("Set up an Azure DevOps project for syncing.")]
    [EndpointDescription("Creates the project's Wayd workspace. The project's work process is set up first when Wayd does not have it yet.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> InitWorkspaceIntegration(
        Guid id,
        InitWorkspaceIntegrationRequest request,
        [FromServices] IAzureDevOpsInitManager azureDevOpsInitManager,
        CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await azureDevOpsInitManager.InitWorkspaceIntegration(
            request.Id, request.ExternalId, request.WorkspaceKey, request.WorkspaceName,
            request.ExternalViewWorkItemUrlTemplate, cancellationToken);

        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/workspaces/{workspaceExternalId}/sync")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Connections)]
    [EndpointSummary("Turn syncing on or off for an Azure DevOps project.")]
    [EndpointDescription("The project must already be set up. Its Wayd workspace and work items are kept either way.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SetWorkspaceSync(
        Guid id,
        Guid workspaceExternalId,
        SetAzureDevOpsWorkspaceSyncRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new SetAzureDevOpsWorkspaceSyncCommand(id, workspaceExternalId, request.IsEnabled), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{id}/teams")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Connections)]
    [EndpointSummary("Get Azure DevOps connection teams.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<AzureDevOpsWorkspaceTeamDto>>> GetConnectionTeams(
        Guid id, Guid? workspaceId, CancellationToken cancellationToken)
    {
        var teams = await _dispatcher.Send(new GetAzureDevOpsConnectionTeamsQuery(id, workspaceId), cancellationToken);
        return Ok(teams);
    }

    [HttpPost("{id}/teams")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Connections)]
    [EndpointSummary("Update Azure DevOps team mappings.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> MapConnectionTeams(
        Guid id,
        [FromBody] AzdoConnectionTeamMappingsRequest request,
        CancellationToken cancellationToken)
    {
        if (id != request.ConnectionId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(id), nameof(request.ConnectionId), HttpContext));

        var teamIds = await _dispatcher.Send(new GetValidBaseTeamIdsQuery(), cancellationToken);
        var result = await _dispatcher.Send(request.ToUpdateAzureDevOpsConnectionTeamMappingsCommand(teamIds), cancellationToken);

        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("test")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Connections)]
    [EndpointSummary("Test Azure DevOps connection configuration.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> TestConfig(
        TestAzureDevOpsConnectionRequest request,
        [FromServices] IAzureDevOpsService azureDevOpsService)
    {
        if (string.IsNullOrWhiteSpace(request.Organization) || string.IsNullOrWhiteSpace(request.PersonalAccessToken))
            return BadRequest(ProblemDetailsExtensions.ForBadRequest("Organization and PAT required.", HttpContext));

        var config = new AzureDevOpsBoardsConnectionConfiguration(request.Organization, request.PersonalAccessToken);
        var result = await azureDevOpsService.TestConnection(new AzureDevOpsConnectionContext(config.Organization, config.PersonalAccessToken));

        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
