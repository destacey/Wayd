using JasperFx.Core;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Admin.Messaging;
using Wolverine.Persistence.Durability;
using Wolverine.Persistence.Durability.DeadLetterManagement;

namespace Wayd.Web.Api.Controllers.Admin;

/// <summary>
/// Operational visibility into the Wolverine durable message store (outbox/inbox counts and the
/// dead letter queue). Injects <see cref="IMessageStore"/> directly rather than going through the
/// Wolverine handler pipeline: this is host-plumbing introspection (the same category as
/// <see cref="BackgroundJobsController"/> over Hangfire), and keeping it out of the pipeline keeps
/// the committed handler codegen tree untouched.
/// </summary>
[Route("api/admin/messaging")]
[ApiVersionNeutral]
[ApiController]
public class MessagingController(IMessageStore messageStore) : ControllerBase
{
    private readonly IMessageStore _messageStore = messageStore;

    [HttpGet("counts")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Messaging)]
    [OpenApiOperation("Get counts of persisted message envelopes by lifecycle state.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<MessagingCountsResponse>> GetCounts()
    {
        var counts = await _messageStore.Admin.FetchCountsAsync();
        return Ok(new MessagingCountsResponse
        {
            Incoming = counts.Incoming,
            Scheduled = counts.Scheduled,
            Outgoing = counts.Outgoing,
            Handled = counts.Handled,
            DeadLetter = counts.DeadLetter,
        });
    }

    [HttpGet("dead-letters")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Messaging)]
    [OpenApiOperation("Get a page of dead-lettered messages, optionally filtered by message type, exception type, and time range.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<DeadLetterMessagesResponse>> GetDeadLetters(
        CancellationToken cancellationToken,
        [FromQuery] string? messageType = null,
        [FromQuery] string? exceptionType = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int pageNumber = 0,
        [FromQuery] int pageSize = 100)
    {
        var query = new DeadLetterEnvelopeQuery(new TimeRange(from, to))
        {
            MessageType = messageType,
            ExceptionType = exceptionType,
            PageNumber = Math.Max(pageNumber, 0),
            PageSize = Math.Clamp(pageSize, 1, 500),
        };

        var results = await _messageStore.DeadLetters.QueryAsync(query, cancellationToken);

        return Ok(new DeadLetterMessagesResponse
        {
            TotalCount = results.TotalCount,
            PageNumber = results.PageNumber,
            PageSize = query.PageSize,
            Items = [.. results.Envelopes.Select(DeadLetterMessageResponse.From)],
        });
    }

    [HttpGet("dead-letters/{id:guid}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Messaging)]
    [OpenApiOperation("Get the full detail of a dead-lettered message, including its serialized body.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeadLetterMessageDetailsResponse>> GetDeadLetterById(Guid id)
    {
        var envelope = await _messageStore.DeadLetters.DeadLetterEnvelopeByIdAsync(id, null);
        if (envelope is null)
        {
            return NotFound();
        }

        return Ok(DeadLetterMessageDetailsResponse.From(envelope, DeadLetterMessageResponse.From(envelope)));
    }

    [HttpPost("dead-letters/replay")]
    [MustHavePermission(ApplicationAction.Run, ApplicationResource.Messaging)]
    [OpenApiOperation("Mark dead-lettered messages as replayable. The durability agent moves them back to the incoming queue on its next pass, so replay is asynchronous.", "")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReplayDeadLetters([FromBody] DeadLetterMessagesRequest request, CancellationToken cancellationToken)
    {
        if (request.Ids.Count == 0)
        {
            return BadRequest(ProblemDetailsExtensions.ForBadRequest("At least one dead letter message id is required.", HttpContext));
        }

        await _messageStore.DeadLetters.ReplayAsync(new DeadLetterEnvelopeQuery([.. request.Ids]), cancellationToken);
        return Accepted();
    }

    [HttpPost("dead-letters/discard")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.Messaging)]
    [OpenApiOperation("Permanently delete dead-lettered messages.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DiscardDeadLetters([FromBody] DeadLetterMessagesRequest request, CancellationToken cancellationToken)
    {
        if (request.Ids.Count == 0)
        {
            return BadRequest(ProblemDetailsExtensions.ForBadRequest("At least one dead letter message id is required.", HttpContext));
        }

        await _messageStore.DeadLetters.DiscardAsync(new DeadLetterEnvelopeQuery([.. request.Ids]), cancellationToken);
        return NoContent();
    }
}
