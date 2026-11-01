using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Application.PokerSessions.Queries;
using Wayd.Planning.Domain.Enums;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Planning.PokerSessions;

namespace Wayd.Web.Api.Controllers.Planning;

[Route("api/planning/poker-sessions")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(FeatureFlags.Names.PlanningPoker)]
[McpTools(McpToolset.Planning, "wayd-estimation")]
public class PokerSessionsController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
    [EndpointSummary("Get a list of poker sessions.")]
    [EndpointDescription(
        "Each entry has its `id`, `key`, `name`, `status` (Active or Completed), `facilitator` and `roundCount`. Pass `status` (Active or Completed) to list only those. Requires the planning-poker feature flag; returns 404 when it is off.")]
    [McpTool("PokerSessions_GetSessions", "List poker sessions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<PokerSessionListDto>>> GetList(CancellationToken cancellationToken, [FromQuery] PokerSessionStatus? status = null)
    {
        var sessions = await _dispatcher.Send(new GetPokerSessionsQuery(status), cancellationToken);
        return Ok(sessions);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
    [EndpointSummary("Get poker session details using the Id or key.")]
    [EndpointDescription(
        "Takes the session's UUID or key. Returns the session with its `estimationScale` and the `estimationScaleVersion` it votes on (its `number` and ordered `values`, each with `isEstimate`, false for a card such as `?`), and its `rounds` in order, each with its `label`, `status` (Voting, Revealed or Accepted), `consensusEstimate`, `voteCount` and `votes` (participant and value). While a round is Voting, every vote's `value` is an empty string except the caller's own, so it shows who has voted but not what; values appear once the round is Revealed.")]
    [McpTool("PokerSessions_GetSession", "Get poker session")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PokerSessionDetailsDto>> GetSession(string idOrKey, CancellationToken cancellationToken)
    {
        var session = await _dispatcher.Send(new GetPokerSessionQuery(idOrKey), cancellationToken);
        return session is not null
            ? Ok(session)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.PokerSessions)]
    [EndpointSummary("Create a poker session.")]
    [EndpointDescription(
        "Starts an Active session, facilitated by the caller, that estimates on the scale `estimationScaleId` (a UUID; EstimationScales_GetScales lists the active scales). The scale must be active. The session is pinned to the scale's current version and stays on it when the scale later publishes a new version or is deactivated. It has no rounds yet; add them with PokerSessions_AddRound. Returns the new session's `id` and `key`.")]
    [McpTool("PokerSessions_CreateSession", "Create poker session", Destructive = false)]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create([FromBody] CreatePokerSessionRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreatePokerSessionCommand(), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetSession), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.PokerSessions)]
    [EndpointSummary("Update a poker session.")]
    [EndpointDescription(
        "A whole-record overwrite of `name` and `estimationScaleId`: pass the current scale id to keep it and the version the session is on, even if that scale has since been deactivated. The scale can change only while the session has no rounds, and only to an active scale; changing it pins the session to that scale's current version. A completed session cannot be updated. Takes the session's UUID.")]
    [McpTool("PokerSessions_UpdateSession", "Update poker session")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdatePokerSessionRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToUpdatePokerSessionCommand(id), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.PokerSessions)]
    [EndpointSummary("Delete a poker session.")]
    [EndpointDescription(
        "Permanently deletes the session with all its rounds, votes and consensus estimates, whatever its status. To close a finished session but keep its results, use PokerSessions_CompleteSession. Takes the session's UUID.")]
    [McpTool("PokerSessions_DeleteSession", "Delete poker session")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeletePokerSessionCommand(id), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/complete")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.PokerSessions)]
    [EndpointSummary("Complete a poker session.")]
    [EndpointDescription(
        "Moves an Active session to Completed, deleting its empty rounds: those with no label, no description and no votes, such as the round opened automatically after an estimate is accepted. A completed session is read-only (no rounds, votes, reveals, resets, consensus or label changes) and cannot be reopened. Takes the session's UUID.")]
    [McpTool("PokerSessions_CompleteSession", "Complete poker session")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Complete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new CompletePokerSessionCommand(id), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/rounds")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.PokerSessions)]
    [EndpointSummary("Add a round to a poker session.")]
    [EndpointDescription(
        "A round is one item being estimated; `label` (optional, at most 512 characters) names it, such as a story's title or key, and `description` (optional, at most 2048 characters) carries detail for the team, such as acceptance criteria. The round opens in Voting, after the session's other rounds. The session must be Active. Takes the session's UUID. Returns the new round.")]
    [McpTool("PokerSessions_AddRound", "Add poker round", Destructive = false)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PokerRoundDto>> AddRound(Guid id, [FromBody] AddPokerRoundRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToAddPokerRoundCommand(id), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}/rounds/{roundId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.PokerSessions)]
    [EndpointSummary("Remove a round from a poker session.")]
    [EndpointDescription(
        "Deletes the round with its votes and consensus estimate. The session must be Active. Both ids are UUIDs.")]
    [McpTool("PokerSessions_RemoveRound", "Remove poker round")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RemoveRound(Guid id, Guid roundId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RemovePokerRoundCommand(id, roundId), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/rounds/{roundId}/reveal")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.PokerSessions)]
    [EndpointSummary("Reveal votes for a round.")]
    [EndpointDescription(
        "Moves a round from Voting to Revealed, showing every vote; voting stays closed until the round is reset. The session must be Active. Both ids are UUIDs.")]
    [McpTool("PokerSessions_RevealRound", "Reveal poker round votes")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RevealRound(Guid id, Guid roundId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RevealPokerRoundCommand(id, roundId), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/rounds/{roundId}/reset")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.PokerSessions)]
    [EndpointSummary("Reset a round to allow re-voting.")]
    [EndpointDescription(
        "Clears every vote and any consensus estimate and moves the round back to Voting, from Voting, Revealed or Accepted. The session must be Active. Both ids are UUIDs.")]
    [McpTool("PokerSessions_ResetRound", "Reset poker round")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ResetRound(Guid id, Guid roundId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ResetPokerRoundCommand(id, roundId), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/rounds/{roundId}/consensus")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.PokerSessions)]
    [EndpointSummary("Set the consensus estimate for a round.")]
    [EndpointDescription(
        "Records the agreed `estimate`, which must be one of the values on the session's scale version, a card such as `?` included, and moves the round from Revealed to Accepted; the round must have been revealed. To change an accepted estimate, reset the round and vote again. The session must be Active. Both ids are UUIDs.")]
    [McpTool("PokerSessions_SetConsensus", "Set poker round consensus")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SetConsensus(Guid id, Guid roundId, [FromBody] SetConsensusRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToSetConsensusCommand(id, roundId), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/rounds/{roundId}/label")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.PokerSessions)]
    [EndpointSummary("Update the label for a round.")]
    [EndpointDescription(
        "Replaces the round's `label`; an omitted or null label clears it. The session must be Active. Both ids are UUIDs.")]
    [McpTool("PokerSessions_UpdateRoundLabel", "Update poker round label")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateRoundLabel(Guid id, Guid roundId, [FromBody] UpdatePokerRoundLabelRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToUpdatePokerRoundLabelCommand(id, roundId), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/rounds/{roundId}/description")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.PokerSessions)]
    [EndpointSummary("Update the description for a round.")]
    [EndpointDescription(
        "Replaces the round's `description` (at most 2048 characters); an omitted, null or blank description clears it. The label is unchanged. The session must be Active. Both ids are UUIDs.")]
    [McpTool("PokerSessions_UpdateRoundDescription", "Update poker round description")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateRoundDescription(Guid id, Guid roundId, [FromBody] UpdatePokerRoundDescriptionRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToUpdatePokerRoundDescriptionCommand(id, roundId), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/rounds/{roundId}/vote")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.PokerSessions)]
    [EndpointSummary("Submit a vote for a round.")]
    [EndpointDescription(
        "Casts or replaces the caller's own vote; a vote is always the signed-in user's, never someone else's. `value` must be one of the values on the session's scale version exactly as it lists them, cards such as `?` included (read them with PokerSessions_GetSession); any other value is refused. The round must be Voting and the session Active. Both ids are UUIDs.")]
    [McpTool("PokerSessions_SubmitVote", "Submit poker vote")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SubmitVote(Guid id, Guid roundId, [FromBody] SubmitVoteRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToSubmitVoteCommand(id, roundId), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}/rounds/{roundId}/vote")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.PokerSessions)]
    [EndpointSummary("Withdraw a vote from a round.")]
    [EndpointDescription(
        "Removes the caller's own vote while the round is Voting; it fails when the caller has not voted. The session must be Active. Both ids are UUIDs.")]
    [McpTool("PokerSessions_WithdrawVote", "Withdraw poker vote")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> WithdrawVote(Guid id, Guid roundId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new WithdrawVoteCommand(id, roundId), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
