using CsvHelper;
using CSharpFunctionalExtensions;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Commands;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Imports;
using Wayd.ProjectPortfolioManagement.Application.Projects.Deliverables.Queries;
using Wayd.ProjectPortfolioManagement.Application.Projects.Models;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Ppm.Projects;

namespace Wayd.Web.Api.Controllers.Ppm;

[Route("api/ppm/projects")]
[ApiVersionNeutral]
[ApiController]
[McpTools(McpToolset.Ppm, "wayd-ppm")]
public class ProjectDeliverablesController(IDispatcher dispatcher, ICsvService csvService) : ControllerBase
{
    private const string ManageDescription =
        "Caller must be the project, parent portfolio, or parent program owner or manager, and the project must not be completed or canceled. " +
        "Send the project's `revision` as `expectedRevision`: a project changed since answers 409.";

    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICsvService _csvService = csvService;

    [HttpGet("{idOrKey}/deliverables")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [EndpointSummary("Get a project's deliverables, in their order.")]
    [EndpointDescription("Deliverables are what the project has committed to produce, each with acceptance criteria and a status. `isOpen` is true while one still has to be accepted or descoped; a project cannot complete while any is open.")]
    [McpTool("Projects_GetDeliverables", "List project deliverables")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ProjectDeliverableDto>>> GetDeliverables(string idOrKey, CancellationToken cancellationToken)
    {
        var deliverables = await _dispatcher.Send(new GetProjectDeliverablesQuery(new ProjectIdOrKey(idOrKey)), cancellationToken);
        return deliverables is not null
            ? Ok(deliverables)
            : NotFound();
    }

    [HttpGet("{idOrKey}/deliverables/{deliverableId}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [EndpointSummary("Get one of a project's deliverables.")]
    [McpTool("Projects_GetDeliverable", "Get project deliverable")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDeliverableDto>> GetDeliverable(string idOrKey, Guid deliverableId, CancellationToken cancellationToken)
    {
        var deliverable = await _dispatcher.Send(new GetProjectDeliverableQuery(new ProjectIdOrKey(idOrKey), deliverableId), cancellationToken);
        return deliverable is not null
            ? Ok(deliverable)
            : NotFound();
    }

    [HttpPost("{id}/deliverables")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [EndpointSummary("Add a deliverable to a project. It starts Not Started, last in the project's order.")]
    [EndpointDescription(ManageDescription)]
    [McpTool("Projects_CreateDeliverable", "Add project deliverable", Destructive = false)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<Guid>> CreateDeliverable(Guid id, [FromBody] CreateProjectDeliverableRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new CreateProjectDeliverableCommand(id, request.Name, request.Description,
            request.AcceptanceCriteria, request.OwnerId, request.TargetDate, request.ExpectedRevision), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetDeliverable), new { idOrKey = id.ToString(), deliverableId = result.Value }, result.Value)
            : Failure(result.Error);
    }

    [HttpPut("{id}/deliverables/{deliverableId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [EndpointSummary("Change a deliverable's name, description, acceptance criteria, owner or target date.")]
    [EndpointDescription("Every field is overwritten from the request body, so read the deliverable first and echo back anything that should stay the same. Status changes through Projects_ChangeDeliverableStatus. " + ManageDescription)]
    [McpTool("Projects_UpdateDeliverable", "Update project deliverable", Idempotent = true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateDeliverable(Guid id, Guid deliverableId, [FromBody] UpdateProjectDeliverableRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new UpdateProjectDeliverableCommand(id, deliverableId, request.Name, request.Description,
            request.AcceptanceCriteria, request.OwnerId, request.TargetDate, request.ExpectedRevision), cancellationToken);

        return NoContentOr(result);
    }

    [HttpGet("{id}/deliverables/{deliverableId}/status-options")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [EndpointSummary("Get the statuses a deliverable can be moved to, with what each move needs.")]
    [EndpointDescription("In the workflow's order, without the status it holds. A `RequiresReason` condition means the move must carry a reason, as descoping does.")]
    [McpTool("Projects_GetDeliverableStatusOptions", "List deliverable status options")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<StatusOptionDto>>> GetStatusOptions(Guid id, Guid deliverableId, CancellationToken cancellationToken)
    {
        var options = await _dispatcher.Send(new GetProjectDeliverableStatusOptionsQuery(id, deliverableId), cancellationToken);
        return options is not null
            ? Ok(options)
            : NotFound();
    }

    [HttpPost("{id}/deliverables/{deliverableId}/status")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [EndpointSummary("Move a deliverable to another status: start, accept, descope or reopen it.")]
    [EndpointDescription(
        "Pick the status from Projects_GetDeliverableStatusOptions. Accepting records who accepted it and when; descoping, or moving to any other Removed status, needs a `reason`. " +
        "Accept a deliverable only after the user confirms it meets its acceptance criteria. " + ManageDescription)]
    [McpTool("Projects_ChangeDeliverableStatus", "Change project deliverable status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ChangeStatus(Guid id, Guid deliverableId, [FromBody] ChangeProjectDeliverableStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ChangeProjectDeliverableStatusCommand(id, deliverableId, request.StatusId,
            request.Reason, request.ExpectedRevision), cancellationToken);

        return NoContentOr(result);
    }

    [HttpGet("{idOrKey}/deliverables/{deliverableId}/status-history")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [EndpointSummary("Get a deliverable's status changes, newest first.")]
    [EndpointDescription("Each change reports the status names as they were at the time, with who made it and why.")]
    [McpTool("Projects_GetDeliverableStatusHistory", "Get deliverable status history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<StatusTransitionDto>>> GetStatusHistory(string idOrKey, Guid deliverableId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetProjectDeliverableStatusHistoryQuery(new ProjectIdOrKey(idOrKey), deliverableId), cancellationToken);

        if (result.IsFailure)
            return BadRequest(result.ToBadRequestObject(HttpContext));

        return result.Value is not null
            ? Ok(result.Value)
            : NotFound();
    }

    [HttpPut("{id}/deliverables/order")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [EndpointSummary("Put a project's deliverables in a new order.")]
    [EndpointDescription("`deliverableIds` names every deliverable of the project exactly once, first to last. " + ManageDescription)]
    [McpTool("Projects_ReorderDeliverables", "Reorder project deliverables", Idempotent = true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ReorderDeliverables(Guid id, [FromBody] ReorderProjectDeliverablesRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ReorderProjectDeliverablesCommand(id, request.DeliverableIds, request.ExpectedRevision), cancellationToken);

        return NoContentOr(result);
    }

    [HttpDelete("{id}/deliverables/{deliverableId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [EndpointSummary("Delete a deliverable that was added by mistake.")]
    [EndpointDescription(
        "Only a deliverable not yet started, on a project that is still Proposed or Approved. Later, descope it with Projects_ChangeDeliverableStatus instead, which keeps it and the reason. " + ManageDescription)]
    [McpTool("Projects_DeleteDeliverable", "Delete project deliverable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> DeleteDeliverable(Guid id, Guid deliverableId, [FromQuery] int? expectedRevision, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteProjectDeliverableCommand(id, deliverableId, expectedRevision), cancellationToken);

        return NoContentOr(result);
    }

    [HttpPost("deliverables/import")]
    [MustHavePermission(ApplicationAction.Import, ApplicationResource.Projects)]
    [EndpointSummary("Submit a csv file of project deliverables to import. Returns the run — 200 once it has finished, 202 while it is still queued or running.")]
    [EndpointDescription("Each row names its project by key, so one file can cover many projects. A project's rows are added in file order, after any deliverables it already has.")]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [CsvImport(ProjectDeliverableImportDefinition.ImportKey)]
    public async Task<ActionResult> ImportDeliverables([FromForm, CsvRows(typeof(ImportProjectDeliverableRequest))] IFormFile file, [FromQuery] Guid? submissionGroupId, [FromQuery] bool validateOnly, [FromServices] ImportSubmissionResponder responder, CancellationToken cancellationToken)
    {
        try
        {
            var imported = _csvService.ReadCsv<ImportProjectDeliverableRequest>(file.OpenReadStream());

            List<SubmittedImportRow<ImportProjectDeliverableDto>> rows = [];
            var validator = new ImportProjectDeliverableRequestValidator();
            foreach (var deliverable in imported)
            {
                var validationResults = await validator.ValidateAsync(deliverable, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        error.ErrorMessage = $"{error.ErrorMessage} (Project: {deliverable.ProjectKey}, Deliverable: {deliverable.Name})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                }

                rows.Add(new SubmittedImportRow<ImportProjectDeliverableDto>(deliverable.ImportId, deliverable.ToImportProjectDeliverableDto()));
            }

            var result = await _dispatcher.Send(new ImportProjectDeliverablesCommand(rows, submissionGroupId, validateOnly), cancellationToken);

            return result.IsSuccess
                ? await responder.Respond(this, result.Value, cancellationToken)
                : BadRequest(result.ToBadRequestObject(HttpContext));
        }
        catch (CsvHelperException ex)
        {
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(ex.Message, HttpContext));
        }
    }

    private ActionResult NoContentOr(Result result) =>
        result.IsSuccess ? NoContent() : Failure(result.Error);

    /// <summary>A stale revision is a conflict and a missing project is not found; anything else is a bad request.</summary>
    private ActionResult Failure(string error) => error switch
    {
        ProjectDeliverableCommandSupport.StaleError => Conflict(ProblemDetailsExtensions.ForConflict(error, HttpContext)),
        ProjectDeliverableCommandSupport.ProjectNotFoundError => NotFound(ProblemDetailsExtensions.ForNotFound(error, HttpContext)),
        _ => BadRequest(ProblemDetailsExtensions.ForBadRequest(error, HttpContext)),
    };
}
