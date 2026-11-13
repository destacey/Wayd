using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Application.PokerSessions.Queries;
using Wayd.Planning.Domain.Enums;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Planning.PokerSessions;

namespace Wayd.Web.Api.Controllers.Planning;

[Route("api/planning/poker-sessions")]
[ApiController]
[FeatureGate(FeatureFlags.Names.PlanningPoker)]
[McpTools(McpToolset.Planning, "wayd-estimation")]
public class PokerSessionsController(IDispatcher dispatcher) : ControllerBase
{
    private const string OnlyFacilitators = " Only the session's facilitators, and holders of the poker Administer permission, can do this.";

    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
    [EndpointSummary("Get a list of poker sessions.")]
    [EndpointDescription(
        "Lists the standalone sessions the caller can see: every Public session, and the Private sessions they facilitate or are invited to, directly or through a team (all of them for a poker administrator). A session hosted by another area of Wayd, such as an intake desk, is never among them; pass `hostKind` (IntakeDesk) and `hostId` together to list one host's sessions instead. Each entry has its `id`, `key`, `name`, `status` (Active or Completed), `visibility` (Public or Private), `host` (its `kind` and `id`, or null for a standalone session), `facilitators`, `participants` (everyone who has joined, voting or not), `roundCount` and `canManage` (whether the caller runs it). Pass `status` (Active or Completed) to list only those. Requires the planning-poker feature flag; returns 404 when it is off.")]
    [McpTool("PokerSessions_GetSessions", "List poker sessions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<PokerSessionListDto>>> GetList(CancellationToken cancellationToken,
        [FromQuery] PokerSessionStatus? status = null,
        [FromQuery] PokerSessionHostKind? hostKind = null,
        [FromQuery] Guid? hostId = null)
    {
        var sessions = await _dispatcher.Send(new GetPokerSessionsQuery(status, hostKind, hostId), cancellationToken);
        return Ok(sessions);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
    [EndpointSummary("Get poker session details using the Id or key.")]
    [EndpointDescription(
        "Takes the session's UUID or key; returns 404 for a Private session the caller cannot see. Returns the session with its `visibility`, `facilitators`, `invitedEmployees`, `invitedTeams`, `members` (each `employee`, `isVoting`, and `joinedOn`), `canManage` (whether the caller runs it: a facilitator, someone who manages its host, or a poker administrator), `host` (the area the session belongs to, its `kind` and `id`, or null for a standalone session), its `estimationScale` and the `estimationScaleVersion` it votes on (its `number` and ordered `values`, each with `isEstimate`, false for a card such as `?`), `focusedRoundId` (the round a facilitator has pointed the session page at, or null), and its `rounds` in order, each with its `label`, `subject` (the record it estimates, its `kind`, `id`, `displayKey`, and the `title` and `description` it had when the round was added, or null), `status` (Voting, Revealed or Accepted), `consensusEstimate`, `voteCount` and `votes` (participant and value). While a round is Voting, every vote's `value` is an empty string except the caller's own, so it shows who has voted but not what; values appear once the round is Revealed.")]
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

    [HttpGet("{id}/invited-teams/{teamId}/members")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
    [EndpointSummary("Get the members of a team invited to a poker session.")]
    [EndpointDescription(
        "Lists the active direct members of `teamId`, a team the session invites, by employee `id`, `key` and `name`. An invited team admits its current members, and the members of its current team of teams, while the session is Private. Returns 404 when the caller cannot see the session, the session is Public (its invites admit no one), or the team is not invited. Both ids are UUIDs.")]
    [McpTool("PokerSessions_GetInvitedTeamMembers", "Get poker session invited team members")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<EmployeeNavigationDto>>> GetInvitedTeamMembers(Guid id, Guid teamId, CancellationToken cancellationToken)
    {
        var members = await _dispatcher.Send(new GetPokerSessionInvitedTeamMembersQuery(id, teamId), cancellationToken);
        return members is not null
            ? Ok(members)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.PokerSessions)]
    [EndpointSummary("Create a poker session.")]
    [EndpointDescription(
        "Starts an Active session, with the caller as its first facilitator, that estimates on the scale `estimationScaleId` (a UUID; EstimationScales_GetScales lists the active scales). The scale must be active. The session is pinned to the scale's current version and stays on it when the scale later publishes a new version or is deactivated. `visibility` is `Public` (the default) or `Private`; `facilitatorIds` adds facilitators beside the caller, and `invitedEmployeeIds` and `invitedTeamIds` invite people to a private session (all employee or team UUIDs, all optional). It has no rounds yet; add them with PokerSessions_AddRound. The caller's account must be linked to an employee. Returns the new session's `id` and `key`.")]
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
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
    [EndpointSummary("Update a poker session.")]
    [EndpointDescription(
        "A whole-record overwrite of `name` and `estimationScaleId`: pass the current scale id to keep it and the version the session is on, even if that scale has since been deactivated. The scale can change only while the session has no rounds, and only to an active scale; changing it pins the session to that scale's current version. A completed session cannot be updated; visibility, facilitators and invites have their own tools. Only the session's facilitators, and holders of the poker Administer permission, can do this. Takes the session's UUID.")]
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
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
    [EndpointSummary("Delete a poker session.")]
    [EndpointDescription(
        "Permanently deletes the session with all its rounds, votes and consensus estimates, whatever its status. To close a finished session but keep its results, use PokerSessions_CompleteSession. Only the session's facilitators, and holders of the poker Administer permission, can do this. Takes the session's UUID.")]
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
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
    [EndpointSummary("Complete a poker session.")]
    [EndpointDescription(
        "Moves an Active session to Completed, deleting its empty rounds: those with no label, no description and no votes, such as the round opened automatically after an estimate is accepted. A completed session is read-only (no rounds, votes, reveals, resets, consensus or label changes) and cannot be reopened. Only the session's facilitators, and holders of the poker Administer permission, can do this. Takes the session's UUID.")]
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
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
    [EndpointSummary("Add a round to a poker session.")]
    [EndpointDescription(
        "A round is one item being estimated; `label` (optional, at most 512 characters) names it, such as a story's title or key, and `description` (optional, at most 2048 characters) carries detail for the team, such as acceptance criteria. The round opens in Voting, after the session's other rounds. The session must be Active. Only the session's facilitators, and holders of the poker Administer permission, can do this. Takes the session's UUID. Returns the new round.")]
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
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
    [EndpointSummary("Remove a round from a poker session.")]
    [EndpointDescription(
        "Deletes the round with its votes and consensus estimate. The session must be Active. Only the session's facilitators, and holders of the poker Administer permission, can do this. Both ids are UUIDs.")]
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

    [HttpPut("{id}/focused-round")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
    [EndpointSummary("Point a poker session at a round.")]
    [EndpointDescription(
        "Sets the round everyone following the facilitator is shown on the session page, returned as the session's `focusedRoundId`. `roundId` is one of the session's rounds; an omitted or null `roundId` points at no round, returning followers to the round in play. The last facilitator to point the session wins. Accepting the round's estimate, removing the round and completing the session each clear it. The session must be Active." + OnlyFacilitators + " Takes the session's UUID.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> FocusRound(Guid id, [FromBody] FocusPokerRoundRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToFocusPokerRoundCommand(id), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/rounds/{roundId}/reveal")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
    [EndpointSummary("Reveal votes for a round.")]
    [EndpointDescription(
        "Moves a round from Voting to Revealed, showing every vote; voting stays closed until the round is reset. The session must be Active. Only the session's facilitators, and holders of the poker Administer permission, can do this. Both ids are UUIDs.")]
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
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
    [EndpointSummary("Reset a round to allow re-voting.")]
    [EndpointDescription(
        "Clears every vote and any consensus estimate and moves the round back to Voting, from Voting, Revealed or Accepted. The session must be Active. Only the session's facilitators, and holders of the poker Administer permission, can do this. Both ids are UUIDs.")]
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
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
    [EndpointSummary("Set the consensus estimate for a round.")]
    [EndpointDescription(
        "Records the agreed `estimate`, which must be one of the values on the session's scale version, a card such as `?` included, and moves the round from Revealed to Accepted; the round must have been revealed. To change an accepted estimate, reset the round and vote again. The session must be Active. Only the session's facilitators, and holders of the poker Administer permission, can do this. Both ids are UUIDs.")]
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
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
    [EndpointSummary("Update the label for a round.")]
    [EndpointDescription(
        "Replaces the round's `label`; an omitted or null label clears it. The session must be Active. Only the session's facilitators, and holders of the poker Administer permission, can do this. Both ids are UUIDs.")]
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
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
    [EndpointSummary("Update the description for a round.")]
    [EndpointDescription(
        "Replaces the round's `description` (at most 2048 characters); an omitted, null or blank description clears it. The label is unchanged. The session must be Active. Only the session's facilitators, and holders of the poker Administer permission, can do this. Both ids are UUIDs.")]
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
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
    [EndpointSummary("Submit a vote for a round.")]
    [EndpointDescription(
        "Casts or replaces the caller's own vote; a vote is always the signed-in user's, never someone else's. `value` must be one of the values on the session's scale version exactly as it lists them, cards such as `?` included (read them with PokerSessions_GetSession); any other value is refused. The round must be Voting and the session Active. A caller who has not joined the session joins it, voting; a member who is not voting cannot vote until they turn voting on with PokerSessions_SetVoting. Both ids are UUIDs.")]
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
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
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

    [HttpPut("{id}/visibility")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
    [EndpointSummary("Set who can see a poker session.")]
    [EndpointDescription(
        "`visibility` is `Public`, which anyone with the poker View permission can see and join, or `Private`, which only the session's facilitators, invited employees and the members of invited teams can. Making a session private disconnects anyone it no longer admits from its live updates. Allowed on an active or a completed session." + OnlyFacilitators + " Takes the session's UUID.")]
    [McpTool("PokerSessions_SetVisibility", "Set poker session visibility", Idempotent = true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SetVisibility(Guid id, [FromBody] SetPokerSessionVisibilityRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToSetPokerSessionVisibilityCommand(id), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/facilitators")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
    [EndpointSummary("Set a poker session's facilitators.")]
    [EndpointDescription(
        "Replaces the facilitators with `employeeIds` (employee UUIDs, at least one). Facilitators run the session: rounds, reveals, resets, consensus, details, visibility, invites, completion and deletion. A facilitator can remove themselves. Allowed on an active or a completed session." + OnlyFacilitators + " Takes the session's UUID.")]
    [McpTool("PokerSessions_SetFacilitators", "Set poker session facilitators", Idempotent = true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SetFacilitators(Guid id, [FromBody] SetPokerSessionFacilitatorsRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToSetPokerSessionFacilitatorsCommand(id), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/invitees")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
    [EndpointSummary("Set who is invited to a poker session.")]
    [EndpointDescription(
        "Replaces the invites with `employeeIds` and `teamIds` (UUIDs; empty lists remove every invite). Invites admit people only while the session is Private, and a team admits its current members and the members of its current team of teams. An invite grants access only: the person still needs the poker View permission, and chooses whether to vote. Removing an invite disconnects anyone the session no longer admits from its live updates. Allowed on an active or a completed session." + OnlyFacilitators + " Takes the session's UUID.")]
    [McpTool("PokerSessions_SetInvitees", "Set poker session invitees", Idempotent = true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SetInvitees(Guid id, [FromBody] SetPokerSessionInviteesRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToSetPokerSessionInviteesCommand(id), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/join")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
    [EndpointSummary("Join a poker session.")]
    [EndpointDescription(
        "Adds the caller to the session's members, voting unless `isVoting` is false. A member who is not voting takes part in the discussion but is left out of the voted and waiting totals. Joining again changes nothing; use PokerSessions_SetVoting to change whether the caller votes. Anyone the session admits can join: on a Public session anyone with the poker View permission, on a Private one its facilitators and invitees. Facilitators join like anyone else. The session must be Active, and the caller's account must be linked to an employee. Takes the session's UUID. Returns the caller's membership.")]
    [McpTool("PokerSessions_Join", "Join poker session", Idempotent = true)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PokerSessionMemberDto>> Join(Guid id, [FromBody] JoinPokerSessionRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToJoinPokerSessionCommand(id), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/voting")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
    [EndpointSummary("Turn the caller's voting on or off in a poker session.")]
    [EndpointDescription(
        "Sets whether the caller votes (`isVoting`). Turning voting off withdraws the caller's votes in rounds not yet revealed and leaves them out of the voted and waiting totals; revealed rounds keep their votes. Turning it on lets them vote at once. The caller must have joined the session, which must be Active. Takes the session's UUID. Returns the caller's membership.")]
    [McpTool("PokerSessions_SetVoting", "Set voting in poker session", Idempotent = true)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PokerSessionMemberDto>> SetVoting(Guid id, [FromBody] SetPokerSessionVotingRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToSetPokerSessionVotingCommand(id), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
