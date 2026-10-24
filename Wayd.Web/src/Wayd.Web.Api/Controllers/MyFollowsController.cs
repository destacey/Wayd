using Microsoft.AspNetCore.Authorization;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Following;
using Wayd.Common.Application.Following.Commands;
using Wayd.Common.Application.Following.Dtos;
using Wayd.Common.Application.Following.Queries;
using Wayd.Common.Application.Models;
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
    [EndpointSummary("List the records you follow.")]
    [EndpointDescription(
        $"Returns one page of the records you follow — projects, programs and planning interval objectives together — with each record's `ownerType`, `id`, `key`, `name`, `statusName`, `lifecycle` (`NotStarted`, `Active`, `Completed`, `Canceled`), `health` (`Healthy`, `AtRisk`, `Unhealthy`, or null when it has none), when you followed it and when it `lastChangedOn`; an objective also carries `parentKey` and `parentName`, its planning interval. A record you can no longer read is left out of the page and the total. Narrow with `ownerType` ({OwnerTypeValues}; any other value returns 400), `view` (`Unhealthy`, `ChangedRecently` for the last seven days, `Closed` for records that have finished, whatever the outcome: completed, canceled, or a missed objective) and `search` (matched against the name, or the key exactly). `sort` is `LastChanged` (default) or `Followed`, and `direction` is `Descending` (default, most recent first) or `Ascending`; a record with no activity counts as the least recently changed, so it comes last when descending and first when ascending. `page` starts at 1; `pageSize` is at most 100. Following notifies nobody yet: it records interest. Requires the following feature flag; returns 404 when it is off.")]
    [McpTool("Me_GetFollows", "List what I follow")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResponse<FollowedRecordDto>>> GetFollows(
        [FromQuery] string? ownerType,
        [FromQuery] FollowView? view,
        [FromQuery] string? search,
        [FromQuery] FollowSort sort = FollowSort.LastChanged,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] SortDirection direction = SortDirection.Descending,
        CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.Send(new GetMyFollowsQuery(ownerType, view, search, sort, page, pageSize, direction), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("counts")]
    [EndpointSummary("Count the records you follow, by kind and by what needs a look.")]
    [EndpointDescription(
        "Returns the `total` you follow and can still read, a count per kind of record you can read (`types`, each with `ownerType`, `displayName` and `count`, including kinds you follow none of), and a count per view (`views`: `Unhealthy`, `ChangedRecently`, `Closed`). Records you can no longer read are not counted. Requires the following feature flag; returns 404 when it is off.")]
    [McpTool("Me_GetFollowCounts", "Count what I follow")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<FollowCountsDto>> GetFollowCounts(CancellationToken cancellationToken)
    {
        return Ok(await _dispatcher.Send(new GetMyFollowCountsQuery(), cancellationToken));
    }

    [HttpGet("{ownerType}/{ownerId:guid}/record")]
    [EndpointSummary("Get a record you follow, with what has happened since you followed it.")]
    [EndpointDescription(
        $"Returns the record as `Me_GetFollows` lists it (`record`), plus `progress` (0–100, objectives only), `people` (each with a `role`, a `name` and the `employeeId` of a person, null for a team: a project's or program's owners then managers, an objective's team), `start` and `end`, and `changesSinceFollowed`: its activity since you followed it, newest first, at most 20 — `hasMoreChanges` says whether there is more. {OwnerTypes} Returns 404 when you do not follow the record or can no longer read it, and 400 for an owner type that cannot be followed. Requires the following feature flag.")]
    [McpTool("Me_GetFollowedRecord", "Get a record I follow")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<FollowedRecordDetailDto>> GetFollowedRecord(string ownerType, Guid ownerId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetFollowedRecordQuery(ownerType, ownerId), cancellationToken);

        if (result.IsFailure)
            return BadRequest(result.ToBadRequestObject(HttpContext));

        return result.Value is not null
            ? Ok(result.Value)
            : NotFound();
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
