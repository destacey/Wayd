using Microsoft.AspNetCore.Authorization;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Notifications.Commands;
using Wayd.Common.Application.Notifications.Dtos;
using Wayd.Common.Application.Notifications.Queries;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Web.Api.Extensions;

namespace Wayd.Web.Api.Controllers;

/// <summary>
/// The caller's notifications: what changed on the records they follow or take part in, and why they were
/// told. Every action acts for the caller alone and needs only a signed-in account — a notification about a
/// record is shown only while the caller can read that record, so it grants nothing.
/// </summary>
[Route("api/me/notifications")]
[ApiVersionNeutral]
[ApiController]
[Authorize]
[FeatureGate(FeatureFlags.Names.Following)]
[McpTools("wayd-users")]
public class MyNotificationsController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [EndpointSummary("List your notifications.")]
    [EndpointDescription(
        "Returns one page of your notifications, one entry per record, newest change first. Each entry has the `record` (`ownerType`, `id`, `key`, `name`, `statusName`; an objective also carries `parentKey` and `parentName`, its planning interval), the newest change — `event` (`Created`, `StatusChanged`, `LifecycleChanged`, `HealthReported`, `TimelineChanged`, `RolesChanged`, `Reparented`, `ScoreRecorded`), `eventId`, `occurredOn`, `actorKind` and `actor` (null when no employee is linked to whoever made it) — the `reasons` you were told (`Following`, `Participant`, and later `Requester`, `Assigned`, `Mentioned`), `unreadCount`, `count`, `isUnread` and `latestNotificationId`. You are never told about your own changes, or about imports. A record you can no longer read is left out of the page and the total. `unreadOnly` keeps only entries with something unread. `page` starts at 1; `pageSize` is at most 100. Requires the following feature flag; returns 404 when it is off.")]
    [McpTool("Me_GetNotifications", "List my notifications")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResponse<NotificationDto>>> GetNotifications(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _dispatcher.Send(new GetMyNotificationsQuery(unreadOnly, page, pageSize), cancellationToken));
    }

    [HttpGet("unread-count")]
    [EndpointSummary("Count the records you have unread notifications about.")]
    [EndpointDescription(
        "Returns `count`, the number of records with unread notifications you can still read, and `isCapped`, true when there are more than 99 — `count` is then 99, shown as \"99+\". Requires the following feature flag; returns 404 when it is off.")]
    [McpTool("Me_GetUnreadNotificationCount", "Count my unread notifications")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadNotificationCountDto>> GetUnreadCount(CancellationToken cancellationToken)
    {
        return Ok(await _dispatcher.Send(new GetMyUnreadNotificationCountQuery(), cancellationToken));
    }

    [HttpPost("{id:long}/read")]
    [EndpointSummary("Mark one notification read.")]
    [EndpointDescription(
        "Marks the notification with this `id` — an entry's `latestNotificationId` — read. Marking one already read changes nothing. Returns 404 when you have no notification with that id. Requires your account to be linked to an employee (403 otherwise) and the following feature flag.")]
    [McpTool("Me_MarkNotificationRead", "Mark a notification read", Destructive = false, Idempotent = true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> MarkRead(long id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new MarkNotificationReadCommand(id), cancellationToken);

        return result.Value ? NoContent() : NotFound();
    }

    [HttpPost("{ownerType}/{ownerId:guid}/read")]
    [EndpointSummary("Mark your notifications about one record read.")]
    [EndpointDescription(
        $"Marks every notification you have about the record read — the whole entry the list shows for it. {OwnerTypeDocs.Parameters} Succeeds whether or not anything was unread. Returns 400 for an owner type that cannot be followed. Requires your account to be linked to an employee (403 otherwise) and the following feature flag.")]
    [McpTool("Me_MarkRecordNotificationsRead", "Mark a record's notifications read", Destructive = false, Idempotent = true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> MarkRecordRead(string ownerType, Guid ownerId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new MarkRecordNotificationsReadCommand(ownerType, ownerId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("read")]
    [EndpointSummary("Mark all your notifications read.")]
    [EndpointDescription(
        "Marks every notification you have read. Succeeds whether or not anything was unread. Requires your account to be linked to an employee (403 otherwise) and the following feature flag.")]
    [McpTool("Me_MarkAllNotificationsRead", "Mark all my notifications read", Destructive = false, Idempotent = true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new MarkAllNotificationsReadCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
