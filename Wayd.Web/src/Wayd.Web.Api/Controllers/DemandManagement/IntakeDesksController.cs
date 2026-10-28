using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.DemandManagement.Application.IntakeDesks;
using Wayd.DemandManagement.Application.IntakeDesks.Commands;
using Wayd.DemandManagement.Application.IntakeDesks.Dtos;
using Wayd.DemandManagement.Application.IntakeDesks.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.DemandManagement.IntakeDesks;

namespace Wayd.Web.Api.Controllers.DemandManagement;

/// <summary>
/// Intake desks: the review responsibilities requests are submitted to.
/// </summary>
[Route("api/demand-management/intake-desks")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(FeatureFlags.Names.DemandManagement)]
public class IntakeDesksController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [Authorize]
    [EndpointSummary("Get the intake desks, or only those a requester can submit to now.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<IntakeDeskDto>>> GetIntakeDesks([FromQuery] bool submittableOnly = false, CancellationToken cancellationToken = default)
    {
        var desks = await _dispatcher.Send(new GetIntakeDesksQuery(submittableOnly), cancellationToken);
        return Ok(desks);
    }

    [HttpGet("{idOrKey}")]
    [Authorize]
    [EndpointSummary("Get an intake desk.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IntakeDeskDto>> GetIntakeDesk(string idOrKey, CancellationToken cancellationToken)
    {
        var desk = await _dispatcher.Send(new GetIntakeDeskQuery(new IdOrKey(idOrKey)), cancellationToken);
        return desk is not null
            ? Ok(desk)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Administer, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Create an intake desk with its initial members.")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create([FromBody] CreateIntakeDeskRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateIntakeDeskCommand(), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetIntakeDesk), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [Authorize]
    [EndpointSummary("Update an intake desk's settings and members.")]
    [EndpointDescription("Needs a Desk Admin on the desk, who also chooses which company channels it offers. Changing its visibility, or naming, removing or re-roling a Desk Admin, needs the Demand Management Admin. A member removed while owning requests on the desk is unassigned from them. Send the desk's `revision` as `expectedRevision`: a desk changed since answers 409.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateIntakeDeskRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        return NoContentOrError(await _dispatcher.Send(request.ToUpdateIntakeDeskCommand(), cancellationToken));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Administer, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Delete an intake desk with no requests on it.")]
    [EndpointDescription("A desk with requests on it cannot be deleted; close it instead. A desk changed while the delete was under way answers 409.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new DeleteIntakeDeskCommand(id), cancellationToken));

    [HttpGet("visibilities")]
    [Authorize]
    [EndpointSummary("Get the desk visibility values, with their codes.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<IntakeDeskVisibilityDto>>> GetVisibilities(CancellationToken cancellationToken)
    {
        var visibilities = await _dispatcher.Send(new GetIntakeDeskVisibilitiesQuery(), cancellationToken);
        return Ok(visibilities);
    }

    [HttpGet("{idOrKey}/workflow")]
    [Authorize]
    [EndpointSummary("Get the review workflow a desk's requests follow.")]
    [EndpointDescription("Whether the desk follows the organization's default, and whether you may choose another: a Desk Admin on it or the Demand Management Admin, while no request is on it.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IntakeDeskWorkflowDto>> GetWorkflow(string idOrKey, CancellationToken cancellationToken)
    {
        var workflow = await _dispatcher.Send(new GetIntakeDeskWorkflowQuery(new IdOrKey(idOrKey)), cancellationToken);
        return workflow is not null
            ? Ok(workflow)
            : NotFound();
    }

    [HttpPut("{id}/workflow")]
    [Authorize]
    [EndpointSummary("Point a desk at another published intake request workflow.")]
    [EndpointDescription("Needs a Desk Admin on the desk or the Demand Management Admin, and is refused while any request is on the desk. The desk then follows the chosen workflow rather than the organization's default.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ChangeWorkflow(Guid id, [FromBody] ChangeIntakeDeskWorkflowRequest request, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new ChangeIntakeDeskWorkflowCommand(id, request.WorkflowId), cancellationToken));

    [HttpPut("{id}/scoring-model")]
    [Authorize]
    [EndpointSummary("Set the scoring model a desk's requests are scored with.")]
    [EndpointDescription("Needs a Desk Admin on the desk or the Demand Management Admin. The model must be active; a null `scoringModelId` stops scoring the desk's requests. Scores already recorded keep the model they were scored with. A desk changed meanwhile answers 409.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ChangeScoringModel(Guid id, [FromBody] ChangeIntakeDeskScoringModelRequest request, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new ChangeIntakeDeskScoringModelCommand(id, request.ScoringModelId), cancellationToken));

    [HttpGet("scoring-model-choices")]
    [Authorize]
    [EndpointSummary("Get the active scoring models a desk can score its requests with.")]
    [EndpointDescription("Building and editing scoring models is in Settings, under scoring model administration.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<IntakeScoringModelChoiceDto>>> GetScoringModelChoices(CancellationToken cancellationToken)
    {
        var choices = await _dispatcher.Send(new GetIntakeScoringModelChoicesQuery(), cancellationToken);
        return Ok(choices);
    }

    [HttpGet("workflow-choices")]
    [Authorize]
    [EndpointSummary("Get the published intake request workflows a desk can follow.")]
    [EndpointDescription("Building and editing workflows is in Settings, under status workflow administration.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<IntakeWorkflowChoiceDto>>> GetWorkflowChoices(CancellationToken cancellationToken)
    {
        var choices = await _dispatcher.Send(new GetIntakeWorkflowChoicesQuery(), cancellationToken);
        return Ok(choices);
    }

    [HttpGet("roles")]
    [Authorize]
    [EndpointSummary("Get the roles a desk member can hold, with their codes.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<IntakeDeskRoleDto>>> GetRoles(CancellationToken cancellationToken)
    {
        var roles = await _dispatcher.Send(new GetIntakeDeskRolesQuery(), cancellationToken);
        return Ok(roles);
    }

    private ActionResult NoContentOrError(Result result) =>
        result.IsSuccess
            ? NoContent()
            : result.Error == IntakeDeskAccess.NotFoundError
                ? NotFound()
                : result.Error == IntakeDeskAccess.StaleError
                    ? Conflict(ProblemDetailsExtensions.ForConflict(result.Error, HttpContext))
                    : BadRequest(result.ToBadRequestObject(HttpContext));
}
