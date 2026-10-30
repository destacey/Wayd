using Microsoft.AspNetCore.Authorization;
using Wayd.Common.Application.Discussions;
using Wayd.Common.Application.Discussions.Commands;
using Wayd.Common.Application.Discussions.Dtos;
using Wayd.Common.Application.Discussions.Queries;
using Wayd.Common.Application.Models;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Discussions;

namespace Wayd.Web.Api.Controllers;

/// <summary>
/// The conversation on a record: an intake request, a project, and every kind of record that registers a channel type.
/// </summary>
/// <remarks>
/// Needs only a signed-in account: every action asks the record's own channel type what the caller may read and
/// write, from the record as it is now. A record the caller cannot read, and a message they may not see, are 404,
/// never 403, so the conversation never confirms that a hidden note exists.
/// </remarks>
[Route("api/discussions/{channelType}/{ownerId:guid}")]
[ApiVersionNeutral]
[ApiController]
[Authorize]
public class DiscussionsController(IDispatcher dispatcher) : ControllerBase
{
    private const string ChannelTypeDocs =
        "`channelType` names the kind of record, such as `demand.intake-request` or `ppm.project`, and `ownerId` is the record's id; a `channelType` that cannot be discussed returns 400.";

    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [EndpointSummary("Get what you may do in a record's conversation.")]
    [EndpointDescription(
        $"{ChannelTypeDocs} Returns `canPost`, `canPostInternal` (false means you are offered no internal notes at all), `canModerate` (lock the conversation and delete anyone's message) and whether the conversation `isLocked`. Read its messages from the entries endpoint. A record nobody has posted on is an empty, unlocked conversation. Returns 404 when the record does not exist or you cannot read it.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DiscussionDto>> GetDiscussion(string channelType, Guid ownerId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetDiscussionQuery(channelType, ownerId), cancellationToken);

        if (result.IsFailure)
            return BadRequest(result.ToBadRequestObject(HttpContext));

        return result.Value is not null ? Ok(result.Value) : NotFound();
    }

    [HttpGet("entries")]
    [EndpointSummary("Get one page of a record's conversation.")]
    [EndpointDescription(
        $"{ChannelTypeDocs} Returns one page of entries: top-level messages and, unless `messagesOnly` is true, the record's activity between them, as its Activity section shows it to you (activity older than the record's latest 2,000 entries is left out). Each entry has a `type` (`Message` or `Activity`), a `timestamp`, and its `message` or `activity`. A thread's replies are not in the page; each message carries `replyCount`, `lastReplyOn` and `recentRepliers`, and the replies come from the replies endpoint. Internal notes appear only if you may read them. `order` is `OldestFirst` (default) or `NewestFirst`; pass the previous page's `nextCursor` as `before` for the next page; `pageSize` counts top-level messages and is at most 500. Returns 404 when the record does not exist or you cannot read it.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CursorPage<DiscussionEntryDto>>> GetEntries(
        string channelType,
        Guid ownerId,
        [FromQuery] string? before = null,
        [FromQuery] int pageSize = PageSizes.Default,
        [FromQuery] DiscussionOrder order = DiscussionOrder.OldestFirst,
        [FromQuery] bool messagesOnly = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.Send(
            new GetDiscussionEntriesQuery(channelType, ownerId, new CursorRequest(before, pageSize), order, messagesOnly), cancellationToken);

        if (result.IsFailure)
            return BadRequest(result.ToBadRequestObject(HttpContext));

        return result.Value is not null ? Ok(result.Value) : NotFound();
    }

    [HttpPost("messages")]
    [EndpointSummary("Post a message that starts a thread in a record's conversation.")]
    [EndpointDescription(
        $"{ChannelTypeDocs} Posts a top-level message, opening the conversation if nobody has posted on the record yet, and returns its id. `kind` is `Public` (everyone who can read the conversation) or `InternalNote` (its internal audience only); `subject` is optional, at most 200 characters; `body` is markdown, at most 10,000 characters, with mentions written `@[Name](employee:{{employeeId}})`. Mentions you may not make are dropped. Returns 403 when you may not post that kind of message, 404 when the record does not exist or you cannot read it, 400 when the conversation is locked, and 409 when someone else opened the conversation at the same moment (post again). Requires your account to be linked to an employee.")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<Guid>> PostMessage(string channelType, Guid ownerId, [FromBody] PostDiscussionMessageRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new PostDiscussionMessageCommand(channelType, ownerId, request.Kind, request.Subject, request.Body), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetDiscussion), new { channelType, ownerId }, result.Value)
            : ErrorResult(result.Error);
    }

    [HttpGet("messages/{messageId:guid}/replies")]
    [EndpointSummary("Get the replies in a thread.")]
    [EndpointDescription(
        $"{ChannelTypeDocs} Returns every reply in the thread the top-level message `messageId` starts, oldest first, each with its author, mentions, and whether you may edit or delete it. Returns 404 when the record, the message or its thread does not exist or you cannot read it.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<List<DiscussionMessageDto>>> GetReplies(string channelType, Guid ownerId, Guid messageId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetDiscussionRepliesQuery(channelType, ownerId, messageId), cancellationToken);

        if (result.IsFailure)
            return BadRequest(result.ToBadRequestObject(HttpContext));

        return result.Value is not null ? Ok(result.Value) : NotFound();
    }

    [HttpPost("messages/{messageId:guid}/replies")]
    [EndpointSummary("Reply in a thread.")]
    [EndpointDescription(
        $"{ChannelTypeDocs} Replies in the thread the top-level message `messageId` starts and returns the reply's id. The reply takes the thread's kind — a reply under an internal note is an internal note — and has no subject; replies go one level deep, so `messageId` must start a thread. `body` is markdown, at most 10,000 characters, with mentions written `@[Name](employee:{{employeeId}})`. Returns 403 when you may not write in that thread, 404 when the record or message does not exist or you cannot read it, and 400 when the conversation is locked or `messageId` is itself a reply. Requires your account to be linked to an employee.")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<Guid>> Reply(string channelType, Guid ownerId, Guid messageId, [FromBody] ReplyToDiscussionMessageRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ReplyToDiscussionMessageCommand(channelType, ownerId, messageId, request.Body), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetReplies), new { channelType, ownerId, messageId }, result.Value)
            : ErrorResult(result.Error);
    }

    [HttpPut("messages/{messageId:guid}")]
    [EndpointSummary("Edit your own message.")]
    [EndpointDescription(
        $"{ChannelTypeDocs} Replaces the subject and text of a message you wrote, keeping the version it replaces as a revision; nothing is recorded when nothing changed. `subject` must be blank on a reply in a thread. Returns 403 when you are not the author or may no longer write that kind of message, 404 when the record or message does not exist or you cannot read it, 400 when the message was deleted or the conversation is locked, and 409 when the message was edited or deleted at the same moment. Requires your account to be linked to an employee.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> EditMessage(string channelType, Guid ownerId, Guid messageId, [FromBody] EditDiscussionMessageRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new EditDiscussionMessageCommand(channelType, ownerId, messageId, request.Subject, request.Body), cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }

    [HttpDelete("messages/{messageId:guid}")]
    [EndpointSummary("Delete a message, leaving a tombstone.")]
    [EndpointDescription(
        $"{ChannelTypeDocs} Deletes a message: its subject, text, earlier versions and mentions are removed, and a tombstone naming who deleted it and when stays in the conversation, so a thread is never quietly rewritten. You may delete your own message; a moderator may delete anyone's. Returns 403 when you may not delete it, 404 when the record or message does not exist or you cannot read it, 400 when it was already deleted, and 409 when it was edited at the same moment. Requires your account to be linked to an employee.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> DeleteMessage(string channelType, Guid ownerId, Guid messageId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteDiscussionMessageCommand(channelType, ownerId, messageId), cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }

    [HttpGet("messages/{messageId:guid}/revisions")]
    [EndpointSummary("Get the earlier versions of a message.")]
    [EndpointDescription(
        $"{ChannelTypeDocs} Returns every version of the message its edits replaced, newest first, each with its `subject`, `body` and when it was `replacedOn`. Empty for a message never edited, or deleted. Returns 404 when the record or message does not exist or you cannot read it.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<List<DiscussionMessageRevisionDto>>> GetRevisions(string channelType, Guid ownerId, Guid messageId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetDiscussionMessageRevisionsQuery(channelType, ownerId, messageId), cancellationToken);

        if (result.IsFailure)
            return BadRequest(result.ToBadRequestObject(HttpContext));

        return result.Value is not null ? Ok(result.Value) : NotFound();
    }

    [HttpPut("lock")]
    [EndpointSummary("Lock a record's conversation.")]
    [EndpointDescription(
        $"{ChannelTypeDocs} Locks the conversation: nothing can be posted, replied or edited until it is unlocked. Locking a record nobody has posted on opens its conversation locked. Moderators only: returns 403 otherwise, 404 when the record does not exist or you cannot read it, 400 when it is already locked, and 409 when the conversation changed at the same moment. Requires your account to be linked to an employee.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Lock(string channelType, Guid ownerId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new SetDiscussionLockCommand(channelType, ownerId, Locked: true), cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }

    [HttpDelete("lock")]
    [EndpointSummary("Unlock a record's conversation.")]
    [EndpointDescription(
        $"{ChannelTypeDocs} Unlocks the conversation so people can post again. Moderators only: returns 403 otherwise, 404 when the record does not exist or you cannot read it, 400 when it is not locked, and 409 when the conversation changed at the same moment. Requires your account to be linked to an employee.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Unlock(string channelType, Guid ownerId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new SetDiscussionLockCommand(channelType, ownerId, Locked: false), cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }

    private ActionResult ErrorResult(string error)
    {
        if (DiscussionAccessErrors.IsNotFound(error))
            return NotFound();

        if (DiscussionAccessErrors.IsForbidden(error))
            return Problem(detail: error, statusCode: StatusCodes.Status403Forbidden);

        if (DiscussionAccessErrors.IsConflict(error))
            return Problem(detail: error, statusCode: StatusCodes.Status409Conflict);

        return BadRequest(ProblemDetailsExtensions.ForBadRequest(error, HttpContext));
    }
}
