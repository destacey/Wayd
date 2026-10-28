using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.DemandManagement.Application.IntakeChannels;
using Wayd.DemandManagement.Application.IntakeChannels.Commands;
using Wayd.DemandManagement.Application.IntakeChannels.Dtos;
using Wayd.DemandManagement.Application.IntakeChannels.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.DemandManagement.IntakeChannels;

namespace Wayd.Web.Api.Controllers.DemandManagement;

/// <summary>
/// Intake channels: the company's list of where requests come from, managed by the Demand Management Admin.
/// </summary>
[Route("api/demand-management/intake-channels")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(FeatureFlags.Names.DemandManagement)]
[McpTools(McpToolset.Intake, "wayd-intake")]
public class IntakeChannelsController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [Authorize]
    [McpTool("IntakeChannels_GetList", "List intake channels")]
    [EndpointSummary("Get the intake channels in the order requesters see them.")]
    [EndpointDescription("With `deskId`, only the active channels that desk offers, which may be none; `includeArchived` is then ignored.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<IntakeChannelDto>>> GetIntakeChannels([FromQuery] bool includeArchived = false, [FromQuery] Guid? deskId = null, CancellationToken cancellationToken = default)
    {
        var channels = await _dispatcher.Send(new GetIntakeChannelsQuery(includeArchived, deskId), cancellationToken);
        return Ok(channels);
    }

    [HttpPost]
    [McpTool("IntakeChannels_Create", "Create intake channel", Destructive = false)]
    [MustHavePermission(ApplicationAction.Administer, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Add an intake channel to the end of the list.")]
    [EndpointDescription("Desks that offer every channel offer it at once; desks that chose their channels do not until a Desk Admin adds it.")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Guid))]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateIntakeChannelRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateIntakeChannelCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetIntakeChannels), null, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [McpTool("IntakeChannels_Update", "Update intake channel")]
    [MustHavePermission(ApplicationAction.Administer, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Rename an intake channel or change its description.")]
    [EndpointDescription("Every request carrying the channel reads the new name.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateIntakeChannelRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        return NoContentOrError(await _dispatcher.Send(request.ToUpdateIntakeChannelCommand(), cancellationToken));
    }

    [HttpPost("{id}/archive")]
    [McpTool("IntakeChannels_Archive", "Archive intake channel")]
    [MustHavePermission(ApplicationAction.Administer, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Archive an intake channel.")]
    [EndpointDescription("No new request can be given an archived channel and no desk offers it; requests already carrying it keep it. Channels are archived, never deleted.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Archive(Guid id, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new ArchiveIntakeChannelCommand(id), cancellationToken));

    [HttpPost("{id}/restore")]
    [McpTool("IntakeChannels_Restore", "Restore intake channel")]
    [MustHavePermission(ApplicationAction.Administer, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Restore an archived intake channel.")]
    [EndpointDescription("Desks that offer every channel offer it again, as do desks that had chosen it before it was archived.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Restore(Guid id, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new RestoreIntakeChannelCommand(id), cancellationToken));

    [HttpPut("order")]
    [McpTool("IntakeChannels_Reorder", "Reorder intake channels")]
    [MustHavePermission(ApplicationAction.Administer, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Put the intake channels in the order requesters see them.")]
    [EndpointDescription("The list must name every channel, archived ones included, exactly once; a partial list is refused.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Reorder([FromBody] ReorderIntakeChannelsRequest request, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(request.ToReorderIntakeChannelsCommand(), cancellationToken));

    private ActionResult NoContentOrError(Result result) =>
        result.IsSuccess
            ? NoContent()
            : result.Error == IntakeChannelErrors.NotFoundError
                ? NotFound()
                : BadRequest(result.ToBadRequestObject(HttpContext));
}
