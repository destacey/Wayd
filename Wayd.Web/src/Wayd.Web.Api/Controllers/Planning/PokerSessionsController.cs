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
public class PokerSessionsController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
    [OpenApiOperation("Get a list of poker sessions.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<PokerSessionListDto>>> GetList(CancellationToken cancellationToken, [FromQuery] PokerSessionStatus? status = null)
    {
        var sessions = await _dispatcher.Send(new GetPokerSessionsQuery(status), cancellationToken);
        return Ok(sessions);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
    [OpenApiOperation("Get poker session details using the Id or key.", "")]
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
    [OpenApiOperation("Create a poker session.", "")]
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
    [OpenApiOperation("Update a poker session.", "")]
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
    [OpenApiOperation("Delete a poker session.", "")]
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
    [OpenApiOperation("Complete a poker session.", "")]
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
    [OpenApiOperation("Add a round to a poker session.", "")]
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
    [OpenApiOperation("Remove a round from a poker session.", "")]
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
    [OpenApiOperation("Reveal votes for a round.", "")]
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
    [OpenApiOperation("Reset a round to allow re-voting.", "")]
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
    [OpenApiOperation("Set the consensus estimate for a round.", "")]
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
    [OpenApiOperation("Update the label for a round.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateRoundLabel(Guid id, Guid roundId, [FromBody] UpdatePokerRoundLabelRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToUpdatePokerRoundLabelCommand(id, roundId), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/rounds/{roundId}/vote")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.PokerSessions)]
    [OpenApiOperation("Submit a vote for a round.", "")]
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
    [OpenApiOperation("Withdraw a vote from a round.", "")]
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
