using Microsoft.AspNetCore.Authorization;
using Wayd.Common.Application.Discussions;
using Wayd.Common.Application.Discussions.Commands;
using Wayd.Common.Application.Discussions.Dtos;
using Wayd.Common.Application.Discussions.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Mcp;
using Wayd.Web.Api.Models.Discussions;

namespace Wayd.Web.Api.Controllers;

/// <summary>
/// How conversations behave on a kind of record: per scope, such as an intake desk, and by default for every scope.
/// </summary>
/// <remarks>
/// Needs only a signed-in account: the kind of record's own channel type decides who may see and change each scope's
/// settings, as it decides who may read a conversation. A scope that does not exist, or that the caller cannot see,
/// is 404. Its tools join the discussions toolset, beside the conversations they shape.
/// </remarks>
[Route("api/discussion-settings/{channelType}")]
[ApiVersionNeutral]
[ApiController]
[Authorize]
[McpTools(McpToolset.Discussions, "wayd-discussions")]
public class DiscussionSettingsController(IDispatcher dispatcher) : ControllerBase
{
    private const string ScopeDocs =
        "`channelType` names the kind of record — `demand.intake-request` for intake requests, whose scope is a desk. `scopeId` is the scope's id, such as the desk's; leave it out for the default every scope without settings of its own follows. A `channelType` whose conversations have no settings returns 404.";

    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [EndpointSummary("Get how conversations behave in a scope.")]
    [EndpointDescription(
        $"{ScopeDocs} Returns the settings in effect — the scope's own, else the default — whether they are inherited, which settings the kind of record supports, and whether you may change them (`canManage`). `mentionsGrantAccess` false means only the people who already have a record can be mentioned in its conversation. Returns 404 when the scope does not exist or you cannot see it.")]
    [McpTool("Discussions_GetSettings", "Get how conversations behave in a scope")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DiscussionSettingsDto>> GetSettings(string channelType, [FromQuery] Guid? scopeId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetDiscussionSettingsQuery(channelType, scopeId), cancellationToken);
        if (result.IsFailure)
            return BadRequest(result.ToBadRequestObject(HttpContext));

        return result.Value is not null ? Ok(result.Value) : NotFound();
    }

    [HttpPut]
    [EndpointSummary("Save how conversations behave in a scope.")]
    [EndpointDescription(
        $"{ScopeDocs} Saves the settings and records each change, with its previous value, in the Activity log; nothing is recorded when nothing changed. Saving the values a scope would inherit leaves it following the default, removing settings of its own if it had them, so later changes to the default reach it. Turning internal notes off stops new notes only: existing notes stay readable to their audience. Returns 403 when you may not change them — a desk's are changed by its Desk Admins and the Demand Management Admin, the default by the Demand Management Admin — 404 when the scope does not exist or you cannot see it, 400 when a setting is one the kind of record does not support, and 409 when someone saved at the same moment.")]
    [McpTool("Discussions_SaveSettings", "Change how conversations behave in a scope")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> SaveSettings(string channelType, [FromQuery] Guid? scopeId, [FromBody] SaveDiscussionSettingsRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new SaveDiscussionSettingsCommand(
            channelType, scopeId, request.InternalNotesEnabled, request.MentionsGrantAccess), cancellationToken);

        if (result.IsSuccess)
            return NoContent();

        if (DiscussionAccessErrors.IsNotFound(result.Error))
            return NotFound();

        if (DiscussionAccessErrors.IsForbidden(result.Error))
            return Problem(detail: result.Error, statusCode: StatusCodes.Status403Forbidden);

        if (DiscussionAccessErrors.IsConflict(result.Error))
            return Problem(detail: result.Error, statusCode: StatusCodes.Status409Conflict);

        return BadRequest(ProblemDetailsExtensions.ForBadRequest(result.Error, HttpContext));
    }
}
