using Microsoft.AspNetCore.Authorization;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Notifications.Commands;
using Wayd.Common.Application.Notifications.Dtos;
using Wayd.Common.Application.Notifications.Queries;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.Notifications;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Me;

namespace Wayd.Web.Api.Controllers;

/// <summary>
/// What the caller has muted: records they hear nothing more about, and reasons they are no longer told for.
/// Every action acts for the caller alone — nobody can mute on someone else's behalf — and needs only a
/// signed-in account, because a mute grants and reveals nothing: a record can be muted only through its own
/// read permission, and muting never changes access, following or participation.
/// </summary>
[Route("api/me/notification-mutes")]
[ApiVersionNeutral]
[ApiController]
[Authorize]
[FeatureGate(FeatureFlags.Names.Following)]
[McpTools("wayd-users")]
public class MyNotificationMutesController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet("records")]
    [EndpointSummary("List the records you have muted.")]
    [EndpointDescription(
        "Returns one page of the records you have muted, most recently muted first: each with `ownerType`, `id`, `key`, `name`, `statusName`, `lifecycle`, `isFollowing` (muting never stops following) and `mutedOn`; an objective also carries `parentKey` and `parentName`, its planning interval. A muted record tells you nothing more, whatever the reason. A record you can no longer read is left out of the page and the total. `page` starts at 1; `pageSize` is at most 500. Requires the following feature flag; returns 404 when it is off.")]
    [McpTool("Me_GetMutedRecords", "List the records I have muted")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResponse<MutedRecordDto>>> GetMutedRecords(
        [FromQuery] int page = PageRequest.FirstPage,
        [FromQuery] int pageSize = PageSizes.Default,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _dispatcher.Send(new GetMyMutedRecordsQuery(new PageRequest(page, pageSize)), cancellationToken));
    }

    [HttpGet("records/{ownerType}/{ownerId:guid}")]
    [EndpointSummary("Get whether you have muted a record.")]
    [EndpointDescription(
        $"Returns `isMuted` and, when true, `mutedOn`. {OwnerTypeDocs.Parameters} Returns 404 when the record does not exist or you cannot read it, and 400 for an owner type that cannot be followed. Requires the following feature flag.")]
    [McpTool("Me_GetRecordMuteStatus", "Check if I have muted a record")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RecordMuteStatusDto>> GetRecordMuteStatus(string ownerType, Guid ownerId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetRecordMuteStatusQuery(ownerType, ownerId), cancellationToken);

        if (result.IsFailure)
            return BadRequest(result.ToBadRequestObject(HttpContext));

        return result.Value is not null
            ? Ok(result.Value)
            : NotFound();
    }

    [HttpPut("records/{ownerType}/{ownerId:guid}")]
    [EndpointSummary("Mute a record.")]
    [EndpointDescription(
        $"Mutes the record for you: you are told nothing more about it, whether you follow it, take part in it, or both, and your unread notifications about it are marked read. {OwnerTypeDocs.Parameters} Muting a record you already muted changes nothing and still succeeds; `wasAlreadyMuted` says which happened, so an undo unmutes only a mute this call made. Muting changes nothing else: you keep your access and your roles, and you still follow the record if you did. Unmuting brings back nothing that happened while it was muted. You can mute only what you can read: returns 404 when the record does not exist or you cannot read it, and 400 for an owner type that cannot be followed. Requires your account to be linked to an employee (403 otherwise) and the following feature flag.")]
    [McpTool("Me_MuteRecord", "Mute a record", Destructive = false)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MuteRecordResponse>> MuteRecord(string ownerType, Guid ownerId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new MuteRecordCommand(ownerType, ownerId), cancellationToken);

        if (result.IsFailure)
            return BadRequest(result.ToBadRequestObject(HttpContext));

        return result.Value == MuteRecordOutcome.NotFound
            ? NotFound()
            : Ok(new MuteRecordResponse { WasAlreadyMuted = result.Value == MuteRecordOutcome.AlreadyMuted });
    }

    [HttpDelete("records/{ownerType}/{ownerId:guid}")]
    [EndpointSummary("Unmute a record.")]
    [EndpointDescription(
        $"Removes your mute of the record, so its later changes notify you again. {OwnerTypeDocs.Parameters} Succeeds whether or not you had muted it, and works on a record you can no longer read. Returns 400 for an owner type that cannot be followed. Requires your account to be linked to an employee (403 otherwise) and the following feature flag.")]
    [McpTool("Me_UnmuteRecord", "Unmute a record")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UnmuteRecord(string ownerType, Guid ownerId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new UnmuteRecordCommand(ownerType, ownerId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("reasons")]
    [EndpointSummary("List the reasons you can mute, and which you have.")]
    [EndpointDescription(
        "Returns one entry per reason you can be told for: `reason` (`Following`: records you follow; `Role`: records you hold a role on, or whose team you belong to), `isMuted`, and `mutedOn` when muted. A muted reason stops only that reason, on every record: if you follow a project and also manage it, muting `Role` still tells you about it because you follow it. Requires the following feature flag; returns 404 when it is off.")]
    [McpTool("Me_GetReasonMutes", "List the reasons I have muted")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ReasonMuteDto>>> GetReasonMutes(CancellationToken cancellationToken)
    {
        return Ok(await _dispatcher.Send(new GetMyReasonMutesQuery(), cancellationToken));
    }

    [HttpPut("reasons/{reason}")]
    [EndpointSummary("Mute a reason.")]
    [EndpointDescription(
        "Stops telling you about any record for `reason` (`Following` or `Role`); you are still told for any other reason that applies. Muting a reason you already muted changes nothing and still succeeds. Muting changes no follow, role or access, and leaves the notifications you already have. Returns 422 for any other reason. Requires your account to be linked to an employee (403 otherwise) and the following feature flag.")]
    [McpTool("Me_MuteReason", "Mute a reason", Destructive = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> MuteReason(NotificationReason reason, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new MuteReasonCommand(reason), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("reasons/{reason}")]
    [EndpointSummary("Unmute a reason.")]
    [EndpointDescription(
        "Removes your mute of `reason`, so later changes tell you for it again. Succeeds whether or not you had muted it. Requires your account to be linked to an employee (403 otherwise) and the following feature flag.")]
    [McpTool("Me_UnmuteReason", "Unmute a reason")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UnmuteReason(NotificationReason reason, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new UnmuteReasonCommand(reason), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
