using Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Commands;
using Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Dtos;
using Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Queries;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Ppm.ProjectLifecycles;

namespace Wayd.Web.Api.Controllers.Ppm;

[Route("api/ppm/project-lifecycles")]
[ApiVersionNeutral]
[ApiController]
[McpTools(McpToolset.Ppm, "wayd-ppm")]
public class ProjectLifecyclesController(ILogger<ProjectLifecyclesController> logger, IDispatcher dispatcher) : ControllerBase
{
    private readonly ILogger<ProjectLifecyclesController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ProjectLifecycles)]
    [EndpointSummary("Get a list of project lifecycles.")]
    [McpTool("ProjectLifecycles_GetProjectLifecycles", "List project lifecycles")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ProjectLifecycleListDto>>> GetProjectLifecycles([FromQuery] ProjectLifecycleState? state, CancellationToken cancellationToken)
    {
        var lifecycles = await _dispatcher.Send(new GetProjectLifecyclesQuery(state), cancellationToken);

        return Ok(lifecycles);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ProjectLifecycles)]
    [EndpointSummary("Get project lifecycle details.")]
    [McpTool("ProjectLifecycles_GetProjectLifecycle", "Get project lifecycle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectLifecycleDetailsDto>> GetProjectLifecycle(string idOrKey, CancellationToken cancellationToken)
    {
        var lifecycle = await _dispatcher.Send(new GetProjectLifecycleQuery(idOrKey), cancellationToken);

        return lifecycle is not null
            ? Ok(lifecycle)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.ProjectLifecycles)]
    [EndpointSummary("Create a project lifecycle — a reusable template of ordered stages that projects are planned in.")]
    [EndpointDescription(
        "It starts in Proposed state, where its name, description and stages can still change; stages may be supplied here in order or added later with ProjectLifecycles_AddStage. It can be assigned to projects only after ProjectLifecycles_Activate, which needs at least one stage and freezes the template. Returns the new lifecycle's id. Confirm with the user before calling.")]
    [McpTool("ProjectLifecycles_Create", "Create project lifecycle", Destructive = false)]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Guid))]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateProjectLifecycleRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateProjectLifecycleCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetProjectLifecycle), new { idOrKey = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProjectLifecycles)]
    [EndpointSummary("Update a project lifecycle's name and description.")]
    [EndpointDescription(
        "This is a whole-record update, not a patch: both fields are overwritten from the request body, so send the current value of any you are not changing. Only a Proposed lifecycle can be updated; an Active or Archived one is fixed. Stages are changed with the stage tools. Confirm with the user before calling. Takes a UUID only, not a key.")]
    [McpTool("ProjectLifecycles_Update", "Update project lifecycle")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateProjectLifecycleRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToUpdateProjectLifecycleCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.ProjectLifecycles)]
    [EndpointSummary("Delete a project lifecycle.")]
    [EndpointDescription(
        "Permanent, and only for a lifecycle still in Proposed state, which no project can be using. An Active lifecycle cannot be deleted; archive it instead (ProjectLifecycles_Archive). Confirm with the user before calling. Takes a UUID only, not a key.")]
    [McpTool("ProjectLifecycles_Delete", "Delete project lifecycle")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteProjectLifecycleCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/activate")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProjectLifecycles)]
    [EndpointSummary("Activate a proposed project lifecycle, making it available to assign to projects.")]
    [EndpointDescription(
        "The lifecycle needs at least one stage. **Activation freezes the template**: its name, description and stages can no longer be changed, added, removed or reordered, and it cannot be deleted. Confirm with the user before calling. Takes a UUID only, not a key.")]
    [McpTool("ProjectLifecycles_Activate", "Activate project lifecycle")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ActivateProjectLifecycleCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/archive")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProjectLifecycles)]
    [EndpointSummary("Archive an active project lifecycle so it can no longer be assigned to projects.")]
    [EndpointDescription(
        "Projects already using it keep it and their stages. Only an Active lifecycle can be archived, and archiving cannot be undone. Confirm with the user before calling. Takes a UUID only, not a key.")]
    [McpTool("ProjectLifecycles_Archive", "Archive project lifecycle")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ArchiveProjectLifecycleCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/stages")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProjectLifecycles)]
    [EndpointSummary("Add a stage to a project lifecycle.")]
    [EndpointDescription(
        "The stage is appended after the existing stages; use ProjectLifecycles_ReorderStages to move it. Only a Proposed lifecycle can change its stages. Returns the new stage's id. Confirm with the user before calling. Takes the lifecycle's UUID only, not a key.")]
    [McpTool("ProjectLifecycles_AddStage", "Add a lifecycle stage", Destructive = false)]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Guid))]
    public async Task<ActionResult<Guid>> AddStage(Guid id, [FromBody] ProjectLifecycleStageRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToAddCommand(id), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetProjectLifecycle), new { idOrKey = id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/stages/{stageId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProjectLifecycles)]
    [EndpointSummary("Update a lifecycle stage's name and description.")]
    [EndpointDescription(
        "This is a whole-record update, not a patch: both fields are overwritten from the request body, so send the current value of any you are not changing. Only a Proposed lifecycle can change its stages. Confirm with the user before calling. Takes the lifecycle's and the stage's UUIDs, not keys.")]
    [McpTool("ProjectLifecycles_UpdateStage", "Update a lifecycle stage")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateStage(Guid id, Guid stageId, [FromBody] ProjectLifecycleStageRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToUpdateCommand(id, stageId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}/stages/{stageId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProjectLifecycles)]
    [EndpointSummary("Remove a stage from a project lifecycle.")]
    [EndpointDescription(
        "The remaining stages close up their order. Only a Proposed lifecycle can change its stages. Confirm with the user before calling. Takes the lifecycle's and the stage's UUIDs, not keys.")]
    [McpTool("ProjectLifecycles_RemoveStage", "Remove a lifecycle stage")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RemoveStage(Guid id, Guid stageId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RemoveProjectLifecycleStageCommand(id, stageId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/stages/reorder")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ProjectLifecycles)]
    [EndpointSummary("Reorder stages in a project lifecycle.")]
    [EndpointDescription(
        "`orderedStageIds` lists **every** stage of the lifecycle exactly once, in the new order; a partial list is rejected. Only a Proposed lifecycle can change its stages. Confirm with the user before calling. Takes the lifecycle's UUID only, not a key.")]
    [McpTool("ProjectLifecycles_ReorderStages", "Reorder lifecycle stages")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ReorderStages(Guid id, [FromBody] ReorderProjectLifecycleStagesRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToReorderProjectLifecycleStagesCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
