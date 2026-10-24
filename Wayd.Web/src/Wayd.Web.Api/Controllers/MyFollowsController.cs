using Microsoft.AspNetCore.Authorization;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Following.Commands;
using Wayd.Common.Application.Following.Dtos;
using Wayd.Common.Application.Following.Queries;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Web.Api.Extensions;

namespace Wayd.Web.Api.Controllers;

/// <summary>
/// The records the caller follows. Every action acts for the caller alone — there is no way to follow on
/// someone else's behalf — and needs only a signed-in account, because following grants nothing: a record
/// can be followed only through its own read permission, and drops out of the list when that is lost.
/// </summary>
[Route("api/me/follows")]
[ApiVersionNeutral]
[ApiController]
[Authorize]
[FeatureGate(FeatureFlags.Names.Following)]
[McpTools("wayd-users")]
public class MyFollowsController(IDispatcher dispatcher) : ControllerBase
{
    private const string OwnerTypeValues =
        "`ownerType` is `ppm.project`, `ppm.program` or `planning.planning-interval-objective`";

    private const string OwnerTypes = $"{OwnerTypeValues}; `ownerId` is the record's UUID, not its key.";

    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [EndpointSummary("List the records you follow, grouped by kind.")]
    [EndpointDescription(
        $"Returns one group per kind of record you follow — projects, programs, planning interval objectives — each with its `displayName` and one page of `follows`, most recently followed first. Each follow carries the record's `id`, `key`, `name` and, for an objective, `parentKey` and `parentName` (its planning interval). A record you can no longer read is left out of its group and its total, and a kind with nothing readable is left out entirely. Pass `ownerType` to page through one kind — {OwnerTypeValues}, and any other value returns 400; without it, `page` and `pageSize` (at most 100) apply to every group. Following notifies nobody yet: it records interest. Requires the following feature flag; returns 404 when it is off.")]
    [McpTool("Me_GetFollows", "List what I follow")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<List<FollowGroupDto>>> GetFollows(
        [FromQuery] string? ownerType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.Send(new GetMyFollowsQuery(ownerType, page, pageSize), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{ownerType}/{ownerId:guid}")]
    [EndpointSummary("Get whether you follow a record.")]
    [EndpointDescription(
        $"Returns `isFollowing` and, when true, `followedOn`. {OwnerTypes} Returns 404 when the record does not exist or you cannot read it — even if you followed it before losing access — and 400 for an owner type that cannot be followed. Requires the following feature flag.")]
    [McpTool("Me_GetFollowStatus", "Check if I follow a record")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<FollowStatusDto>> GetFollowStatus(string ownerType, Guid ownerId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetFollowStatusQuery(ownerType, ownerId), cancellationToken);

        if (result.IsFailure)
            return BadRequest(result.ToBadRequestObject(HttpContext));

        return result.Value is not null
            ? Ok(result.Value)
            : NotFound();
    }

    [HttpPut("{ownerType}/{ownerId:guid}")]
    [EndpointSummary("Follow a record.")]
    [EndpointDescription(
        $"Follows the record for you. {OwnerTypes} Following a record you already follow changes nothing and still succeeds. You can follow only what you can read: returns 404 when the record does not exist or you cannot read it, and 400 for an owner type that cannot be followed. Following grants no access and notifies nobody yet. Requires your account to be linked to an employee (403 otherwise) and the following feature flag.")]
    [McpTool("Me_Follow", "Follow a record", Destructive = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Follow(string ownerType, Guid ownerId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new FollowRecordCommand(ownerType, ownerId), cancellationToken);

        if (result.IsFailure)
            return BadRequest(result.ToBadRequestObject(HttpContext));

        return result.Value == FollowRecordOutcome.NotFound
            ? NotFound()
            : NoContent();
    }

    [HttpDelete("{ownerType}/{ownerId:guid}")]
    [EndpointSummary("Stop following a record.")]
    [EndpointDescription(
        $"Removes your follow of the record. {OwnerTypes} Succeeds whether or not you followed it, and works on a record you can no longer read. Returns 400 for an owner type that cannot be followed. Requires your account to be linked to an employee (403 otherwise) and the following feature flag.")]
    [McpTool("Me_Unfollow", "Stop following a record")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Unfollow(string ownerType, Guid ownerId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new UnfollowRecordCommand(ownerType, ownerId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
