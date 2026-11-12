using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows.Dtos;
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
[ApiController]
[FeatureGate(FeatureFlags.Names.DemandManagement)]
[McpTools(McpToolset.Intake, "wayd-intake")]
public class IntakeDesksController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [Authorize]
    [McpTool("IntakeDesks_GetList", "List intake desks")]
    [EndpointSummary("Get the intake desks, or only those a requester can submit to now.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<IntakeDeskDto>>> GetIntakeDesks([FromQuery] bool submittableOnly = false, CancellationToken cancellationToken = default)
    {
        var desks = await _dispatcher.Send(new GetIntakeDesksQuery(submittableOnly), cancellationToken);
        return Ok(desks);
    }

    [HttpGet("{idOrKey}")]
    [McpTool("IntakeDesks_GetIntakeDesk", "Get intake desk")]
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
    [McpTool("IntakeDesks_Create", "Create intake desk", Destructive = false)]
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
    [McpTool("IntakeDesks_Update", "Update intake desk")]
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
    [McpTool("IntakeDesks_Delete", "Delete intake desk")]
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
    [McpTool("IntakeDesks_GetVisibilities", "List intake desk visibilities")]
    [Authorize]
    [EndpointSummary("Get the desk visibility values, with their codes.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<IntakeDeskVisibilityDto>>> GetVisibilities(CancellationToken cancellationToken)
    {
        var visibilities = await _dispatcher.Send(new GetIntakeDeskVisibilitiesQuery(), cancellationToken);
        return Ok(visibilities);
    }

    [HttpGet("{idOrKey}/workflow")]
    [McpTool("IntakeDesks_GetWorkflow", "Get intake desk workflow")]
    [Authorize]
    [EndpointSummary("Get the review workflow a desk's requests follow.")]
    [EndpointDescription("Whether the desk follows the organization's default, and whether you may choose another: a Desk Admin on it or the Demand Management Admin.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IntakeDeskWorkflowDto>> GetWorkflow(string idOrKey, CancellationToken cancellationToken)
    {
        var workflow = await _dispatcher.Send(new GetIntakeDeskWorkflowQuery(new IdOrKey(idOrKey)), cancellationToken);
        return workflow is not null
            ? Ok(workflow)
            : NotFound();
    }

    [HttpGet("{id}/workflow/change-preview")]
    [McpTool("IntakeDesks_PreviewWorkflowChange", "Preview intake desk workflow change")]
    [Authorize]
    [EndpointSummary("Preview moving a desk and its requests onto another published intake request workflow.")]
    [EndpointDescription("Where each status of the desk's workflow would land in the target, how it was matched, how many of the desk's requests are in it, and the target's statuses an unplaced one can be sent to. The workflow must be published and not the one the desk follows. Needs a Desk Admin on the desk or the Demand Management Admin. Nothing changes.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScopedWorkflowChangePreviewDto>> PreviewWorkflowChange(Guid id, [FromQuery] Guid workflowId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetIntakeDeskWorkflowChangeQuery(id, workflowId), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : ErrorFor(result.Error);
    }

    [HttpPut("{id}/workflow")]
    [McpTool("IntakeDesks_ChangeWorkflow", "Change intake desk workflow", Destructive = true)]
    [Authorize]
    [EndpointSummary("Point a desk at another published intake request workflow, moving its requests onto it.")]
    [EndpointDescription("Needs a Desk Admin on the desk or the Demand Management Admin. Every request on the desk moves to the status the automatic mapping or `decisions` send its own to, recorded in its status history; preview the change first, and decide each status it leaves unplaced. A request lands in a status meaning Duplicate only while it names the request it duplicates, and in a closing status needing a reason only while it has one; a refusal names every request that cannot, and nothing moves. Choosing the organization's default makes the desk follow it again; any other gives the desk its own. Choosing the workflow the desk already follows moves only requests still on another one, such as a request created while an earlier change ran. Returns how many requests moved. Another change to the desk's workflow saved meanwhile answers 409.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<int>> ChangeWorkflow(Guid id, [FromBody] ChangeIntakeDeskWorkflowRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToChangeIntakeDeskWorkflowCommand(id), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : ErrorFor(result.Error);
    }

    [HttpPut("{id}/scoring-model")]
    [Authorize]
    [McpTool("IntakeDesks_ChangeScoringModel", "Change intake desk scoring model")]
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
    [McpTool("IntakeDesks_GetScoringModelChoices", "List intake desk scoring model choices")]
    [EndpointSummary("Get the active scoring models a desk can score its requests with.")]
    [EndpointDescription("Building and editing scoring models is in Settings, under scoring model administration.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<IntakeScoringModelChoiceDto>>> GetScoringModelChoices(CancellationToken cancellationToken)
    {
        var choices = await _dispatcher.Send(new GetIntakeScoringModelChoicesQuery(), cancellationToken);
        return Ok(choices);
    }

    [HttpPut("{id}/estimation-scale")]
    [Authorize]
    [McpTool("IntakeDesks_ChangeEstimationScale", "Change intake desk estimation scale")]
    [EndpointSummary("Set the estimation scale a desk's requests are estimated on.")]
    [EndpointDescription("Needs a Desk Admin on the desk or the Demand Management Admin. The scale must be active, and the desk is pinned to its current version; choosing the scale the desk already uses moves it to that version. A null `estimationScaleId` stops estimating the desk's requests. Refused while an open request on the desk has an estimate on the version the desk pins now; estimates on an earlier version, and on closed requests, keep their version and do not block it. A desk changed meanwhile answers 409.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ChangeEstimationScale(Guid id, [FromBody] ChangeIntakeDeskEstimationScaleRequest request, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new ChangeIntakeDeskEstimationScaleCommand(id, request.EstimationScaleId), cancellationToken));

    [HttpGet("estimation-scale-choices")]
    [Authorize]
    [McpTool("IntakeDesks_GetEstimationScaleChoices", "List intake desk estimation scale choices")]
    [EndpointSummary("Get the active estimation scales a desk can estimate its requests on.")]
    [EndpointDescription("Each with the estimate values of its current version, which a desk choosing it is pinned to; cards are left out. Building and editing scales is in Settings, under estimation scales.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<IntakeEstimationScaleChoiceDto>>> GetEstimationScaleChoices(CancellationToken cancellationToken)
    {
        var choices = await _dispatcher.Send(new GetIntakeEstimationScaleChoicesQuery(), cancellationToken);
        return Ok(choices);
    }

    [HttpGet("workflow-choices")]
    [McpTool("IntakeDesks_GetWorkflowChoices", "List intake desk workflow choices")]
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
    [McpTool("IntakeDesks_GetRoles", "List intake desk roles")]
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
            : ErrorFor(result.Error);

    private ActionResult ErrorFor(string error) =>
        error == IntakeDeskAccess.NotFoundError
            ? NotFound()
            : error == IntakeDeskAccess.StaleError
                ? Conflict(ProblemDetailsExtensions.ForConflict(error, HttpContext))
                : BadRequest(Result.Failure(error).ToBadRequestObject(HttpContext));
}
