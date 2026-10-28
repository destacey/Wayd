using Microsoft.AspNetCore.Authorization;
using CSharpFunctionalExtensions;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Scoring.ScoringModels.Dtos;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.DemandManagement.Application.IntakeDesks;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.IntakeRequests.Dtos;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;
using Wayd.DemandManagement.Application.IntakeRequestTagCategories;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.DemandManagement.IntakeRequests;

namespace Wayd.Web.Api.Controllers.DemandManagement;

/// <summary>
/// Intake requests: asks for delivery capacity that are not yet projects or work items.
/// </summary>
/// <remarks>
/// Every read and command applies the request's read tiers. A request the caller may not see answers 404,
/// exactly like one that does not exist, so a private desk's requests cannot be found by probing.
/// </remarks>
[Route("api/demand-management/intake-requests")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(FeatureFlags.Names.DemandManagement)]
public class IntakeRequestsController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [Authorize]
    [EndpointSummary("Get the intake requests you can see, as summaries.")]
    [EndpointDescription("Without View, only requests on desks you are a member of and requests that are your own. Requests on a private desk are absent for anyone outside it rather than listed as redacted rows. With `mine`, only requests you are the requester of or submitted, including those entered on someone else's behalf. With `openOnly`, only requests still under review or accepted — in the Proposed or Active status category, whatever the desk's workflow calls the status.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<IntakeRequestSummaryDto>>> GetIntakeRequests([FromQuery] Guid? deskId, [FromQuery] bool mine = false, [FromQuery] bool openOnly = false, CancellationToken cancellationToken = default)
    {
        var requests = await _dispatcher.Send(new GetIntakeRequestsQuery(deskId, mine, openOnly), cancellationToken);
        return Ok(requests);
    }

    [HttpGet("scores")]
    [Authorize]
    [EndpointSummary("Get the current scores of the intake requests you can see.")]
    [EndpointDescription("Takes the same filters as the list and answers beside it, for the requests whose score you may read: a score is substance, so a request whose substance is withheld from you is absent, as is one never scored. `isDeskModel` is false for a score from a model the request's desk no longer scores with, which does not rank with the rest.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<IntakeRequestCurrentScoreDto>>> GetIntakeRequestScores([FromQuery] Guid? deskId, [FromQuery] bool mine = false, [FromQuery] bool openOnly = false, CancellationToken cancellationToken = default)
    {
        var scores = await _dispatcher.Send(new GetIntakeRequestScoresQuery(deskId, mine, openOnly), cancellationToken);
        return Ok(scores);
    }

    [HttpGet("{idOrKey}")]
    [Authorize]
    [EndpointSummary("Get an intake request in the tiers you can read.")]
    [EndpointDescription("The summary is always returned; the substance is null when you may not read it.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IntakeRequestDetailsDto>> GetIntakeRequest(string idOrKey, CancellationToken cancellationToken)
    {
        var request = await _dispatcher.Send(new GetIntakeRequestQuery(new IdOrKey(idOrKey)), cancellationToken);
        return request is not null
            ? Ok(request)
            : NotFound();
    }

    [HttpGet("{idOrKey}/activities")]
    [Authorize]
    [EndpointSummary("Get an intake request's history, limited to the tiers you can read.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResponse<ActivityLogDto>>> GetActivities(string idOrKey, [FromQuery] int page = PageRequest.FirstPage, [FromQuery] int pageSize = PageSizes.Default, CancellationToken cancellationToken = default)
    {
        var activities = await _dispatcher.Send(new GetIntakeRequestActivitiesQuery(new IdOrKey(idOrKey), new PageRequest(page, pageSize)), cancellationToken);
        return activities is not null
            ? Ok(activities)
            : NotFound();
    }

    [HttpPost]
    [Authorize]
    [EndpointSummary("Submit an intake request to a desk.")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create([FromBody] CreateIntakeRequestRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateIntakeRequestCommand(), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetIntakeRequest), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [Authorize]
    [EndpointSummary("Update an intake request.")]
    [EndpointDescription("The requester can edit their own request while it is proposed; otherwise you must be an agent on the request's desk. You must also be able to read the request's detail.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateIntakeRequestRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        return NoContentOrError(await _dispatcher.Send(request.ToUpdateIntakeRequestCommand(), cancellationToken));
    }

    [HttpPut("{id}/assignee")]
    [Authorize]
    [EndpointSummary("Give an intake request a different assignee, or unassign it.")]
    [EndpointDescription("Needs an agent on the request's desk. The assignee must be a member of that desk.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ChangeAssignee(Guid id, [FromBody] ChangeIntakeRequestAssigneeRequest request, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new ChangeIntakeRequestAssigneeCommand(id, request.AssigneeId), cancellationToken));

    [HttpPost("{id}/reroute")]
    [Authorize]
    [EndpointSummary("Move an open intake request to another desk.")]
    [EndpointDescription("Needs an agent on the request's current desk; the destination need not be one you work, but must be open to new requests. A move to a less private desk is refused unless `confirmWiderVisibility` is set. An assignee who is not a member of the destination is unassigned.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Reroute(Guid id, [FromBody] RerouteIntakeRequestRequest request, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new RerouteIntakeRequestCommand(id, request.DeskId, request.ConfirmWiderVisibility, request.StatusId), cancellationToken));

    [HttpGet("{id}/arrival-statuses")]
    [Authorize]
    [EndpointSummary("Get the statuses an intake request can arrive in on another desk.")]
    [EndpointDescription("The destination workflow's open statuses that need no reason, in its order, and the one the request arrives in unless you choose. Empty unless you can move the request: an agent on its current desk.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IntakeRequestArrivalStatusesDto>> GetArrivalStatuses(Guid id, [FromQuery] Guid deskId, CancellationToken cancellationToken)
    {
        var arrival = await _dispatcher.Send(new GetIntakeRequestArrivalStatusesQuery(id, deskId), cancellationToken);
        return arrival is not null
            ? Ok(arrival)
            : NotFound();
    }

    [HttpPost("{id}/status")]
    [Authorize]
    [EndpointSummary("Move an intake request to another status of its desk's workflow.")]
    [EndpointDescription("Needs an agent on the request's desk. Declining, deferring, marking a duplicate and finishing need a `reason` the requester can read; marking a duplicate needs `duplicateOfId`, a request you can see that is not itself a duplicate. Withdrawn is the requester's, through withdraw.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ChangeStatus(Guid id, [FromBody] ChangeIntakeRequestStatusRequest request, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new ChangeIntakeRequestStatusCommand(id, request.StatusId, request.Reason, request.DuplicateOfId), cancellationToken));

    [HttpPost("{id}/withdraw")]
    [Authorize]
    [EndpointSummary("Withdraw your own open intake request.")]
    [EndpointDescription("Only the request's requester; the submitter cannot withdraw a request entered on someone's behalf.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Withdraw(Guid id, [FromBody] WithdrawIntakeRequestRequest request, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new WithdrawIntakeRequestCommand(id, request.Note), cancellationToken));

    [HttpGet("{id}/status-options")]
    [Authorize]
    [EndpointSummary("Get the statuses you can move an intake request to, with what each move needs.")]
    [EndpointDescription("In its desk workflow's order, without the status it holds or Withdrawn. Empty unless you are an agent on the request's desk.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<IntakeRequestStatusOptionDto>>> GetStatusOptions(Guid id, CancellationToken cancellationToken)
    {
        var options = await _dispatcher.Send(new GetIntakeRequestStatusOptionsQuery(id), cancellationToken);
        return options is not null
            ? Ok(options)
            : NotFound();
    }

    [HttpGet("{idOrKey}/status-history")]
    [Authorize]
    [EndpointSummary("Get an intake request's status changes, newest first, in the tiers you can read.")]
    [EndpointDescription("Each change reports the status names as they were at the time. Without the request's substance, each change comes without its reason or who made it.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<StatusTransitionDto>>> GetStatusHistory(string idOrKey, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetIntakeRequestStatusHistoryQuery(new IdOrKey(idOrKey)), cancellationToken);

        if (result.IsFailure)
            return BadRequest(result.ToBadRequestObject(HttpContext));

        return result.Value is not null
            ? Ok(result.Value)
            : NotFound();
    }

    [HttpGet("{idOrKey}/status-workflow")]
    [Authorize]
    [EndpointSummary("Get the status workflow an intake request moves through.")]
    [EndpointDescription("Readable by anyone who can see the request, so its status history can be drawn on the workflow without workflow administration permissions.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StatusWorkflowDetailsDto>> GetStatusWorkflow(string idOrKey, CancellationToken cancellationToken)
    {
        var workflow = await _dispatcher.Send(new GetIntakeRequestStatusWorkflowQuery(new IdOrKey(idOrKey)), cancellationToken);
        return workflow is not null
            ? Ok(workflow)
            : NotFound();
    }

    [HttpPost("{id}/participants")]
    [Authorize]
    [EndpointSummary("Give an employee access to an intake request as a participant.")]
    [EndpointDescription("An agent on the request's desk can always add someone; the requester and submitter can too, except on a private desk that does not allow it.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> AddParticipant(Guid id, [FromBody] AddIntakeRequestParticipantRequest request, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new AddIntakeRequestParticipantCommand(id, request.EmployeeId), cancellationToken));

    [HttpDelete("{id}/participants/{employeeId}")]
    [Authorize]
    [EndpointSummary("Remove a participant's access to an intake request.")]
    [EndpointDescription("Needs an agent on the request's desk.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RemoveParticipant(Guid id, Guid employeeId, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new RemoveIntakeRequestParticipantCommand(id, employeeId), cancellationToken));

    [HttpPut("{id}/tags/{tagId}")]
    [Authorize]
    [EndpointSummary("File an intake request under a tag.")]
    [EndpointDescription("Needs an agent on the request's desk. The tag must be active, on an active axis. On an axis that takes one tag, this replaces the tag the request held there. Applying a tag the request already carries changes nothing.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Tag(Guid id, Guid tagId, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new TagIntakeRequestCommand(id, tagId), cancellationToken));

    [HttpDelete("{id}/tags/{tagId}")]
    [Authorize]
    [EndpointSummary("Take a tag off an intake request.")]
    [EndpointDescription("Needs an agent on the request's desk. Works for an inactive tag too. Removing a tag the request does not carry changes nothing.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Untag(Guid id, Guid tagId, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new UntagIntakeRequestCommand(id, tagId), cancellationToken));

    [HttpDelete("{id}")]
    [Authorize]
    [EndpointSummary("Delete an intake request.")]
    [EndpointDescription("Needs a Desk Admin on the request's desk. Deleting is a last resort: withdrawing or declining keeps a request on record.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new DeleteIntakeRequestCommand(id), cancellationToken));

    [HttpGet("need-by-bases")]
    [Authorize]
    [EndpointSummary("Get the need-by basis values, with their codes and families.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<NeedByBasisDto>>> GetNeedByBases(CancellationToken cancellationToken)
    {
        var bases = await _dispatcher.Send(new GetNeedByBasesQuery(), cancellationToken);
        return Ok(bases);
    }

    [HttpGet("need-by-basis-families")]
    [Authorize]
    [EndpointSummary("Get the need-by basis families, with their codes and names.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<NeedByBasisFamilyDto>>> GetNeedByBasisFamilies(CancellationToken cancellationToken)
    {
        var families = await _dispatcher.Send(new GetNeedByBasisFamiliesQuery(), cancellationToken);
        return Ok(families);
    }

    [HttpGet("{id}/scoring")]
    [Authorize]
    [EndpointSummary("Get an intake request's scoring: its desk's model, its score history and its rank on the desk.")]
    [EndpointDescription("Answers 404 when you may not read the request's substance, the tier its score belongs to. `scoringModel` is null when the desk does not score its requests. `deskRank` places the request among the open requests on its desk scored with the desk's current model whose scores you may read.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IntakeRequestScoringDto>> GetScoring(Guid id, CancellationToken cancellationToken)
    {
        var scoring = await _dispatcher.Send(new GetIntakeRequestScoringQuery(id), cancellationToken);
        return scoring is not null
            ? Ok(scoring)
            : NotFound();
    }

    [HttpPost("{id}/scores")]
    [Authorize]
    [EndpointSummary("Score an intake request with its desk's scoring model.")]
    [EndpointDescription("Needs an agent on the request's desk, an open request, and a desk with a scoring model. Rate every criterion: choose a level for one rated on a scale, a number for one without. Adds a score to the request's history and makes it current; earlier scores are kept. Answers the new score's id.")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<Guid>> Score(Guid id, [FromBody] ScoreIntakeRequestRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RecordIntakeRequestScoreCommand(id, request.ToInputs()), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetScoring), new { id }, result.Value)
            : ErrorResult(result);
    }

    [HttpPost("{id}/scores/preview")]
    [Authorize]
    [EndpointSummary("Preview what ratings would score an intake request, without recording it.")]
    [EndpointDescription("Needs an agent on the request's desk. Calculates every output of the desk's scoring model from the ratings and records nothing.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ScoringModelEvaluationDto>> PreviewScore(Guid id, [FromBody] ScoreIntakeRequestRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new PreviewIntakeRequestScoreQuery(id, request.ToInputs()), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : ErrorResult(result);
    }

    private ActionResult ErrorResult(Result result) =>
        result.Error is IntakeRequestAuthorization.NotFoundError
            ? NotFound()
            : BadRequest(result.ToBadRequestObject(HttpContext));

    private ActionResult NoContentOrError(Result result) =>
        result.IsSuccess
            ? NoContent()
            : result.Error is IntakeRequestAuthorization.NotFoundError or IntakeDeskAccess.NotFoundError
                or IntakeRequestTagCategoryErrors.TagNotFoundError
                ? NotFound()
                : BadRequest(result.ToBadRequestObject(HttpContext));
}
