using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Queries;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Ppm.ProjectLifecycles;
using Wayd.Web.Api.Models.Ppm.Projects;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Queries;

namespace Wayd.Web.Api.Controllers.Ppm;

[Route("api/ppm/[controller]")]
[ApiVersionNeutral]
[ApiController]
public class ProjectsController(ILogger<ProjectsController> logger, IDispatcher dispatcher) : ControllerBase
{
    private readonly ILogger<ProjectsController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [OpenApiOperation("Get a list of projects.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ProjectListDto>>> GetProjects([FromQuery] int[]? status, [FromQuery] Guid? portfolioId, [FromQuery] int[]? role, CancellationToken cancellationToken)
    {
        var filter = ParseStatusFilter(status);
        var roleFilter = ParseRoleFilter(role);

        IdOrKey? portfolioIdOrKey = portfolioId.HasValue
            ? new IdOrKey(portfolioId.Value)
            : null;

        var projects = await _dispatcher.Send(new GetProjectsQuery(StatusFilter: filter, PortfolioIdOrKey: portfolioIdOrKey, RoleFilter: roleFilter), cancellationToken);

        return projects is not null
            ? Ok(projects)
            : NotFound();
    }

    [HttpGet("my-summary")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [OpenApiOperation("Get a summary of the current user's project involvement.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<MyProjectsSummaryDto>> GetMyProjectsSummary([FromQuery] int[]? status, CancellationToken cancellationToken)
    {
        var statusFilter = ParseStatusFilter(status);

        var summary = await _dispatcher.Send(new GetMyProjectsSummaryQuery(StatusFilter: statusFilter), cancellationToken);

        return summary is not null
            ? Ok(summary)
            : Ok(new MyProjectsSummaryDto());
    }

    [HttpGet("my-task-metrics")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [OpenApiOperation("Get aggregated task metrics across the current user's projects.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<MyProjectsTaskMetricsDto>> GetMyProjectsTaskMetrics([FromQuery] int[]? status, [FromQuery] int[]? role, CancellationToken cancellationToken)
    {
        var statusFilter = ParseStatusFilter(status);
        var roleFilter = ParseRoleFilter(role);

        return Ok(await _dispatcher.Send(new GetMyProjectsTaskMetricsQuery(StatusFilter: statusFilter, RoleFilter: roleFilter), cancellationToken));
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [OpenApiOperation("Get project details.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDetailsDto>> GetProject(string idOrKey, CancellationToken cancellationToken)
    {
        var project = await _dispatcher.Send(new GetProjectQuery(idOrKey), cancellationToken);

        return project is not null
            ? Ok(project)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Projects)]
    [OpenApiOperation("Create a project.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create([FromBody] CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateProjectCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetProject), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [OpenApiOperation("Update a project.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateProjectCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }


    [HttpPut("{id}/program")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [OpenApiOperation("Change a project's program.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ChangeProgram(Guid id, [FromBody] ChangeProjectProgramRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToChangeProjectProgramCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/key")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [OpenApiOperation("Change a project's key.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ChangeKey(Guid id, [FromBody] ChangeProjectKeyRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToChangeProjectKeyCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/approve")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [OpenApiOperation("Approve a project.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ApproveProjectCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/activate")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [OpenApiOperation("Activate a project.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ActivateProjectCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/complete")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [OpenApiOperation("Complete a project.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Complete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new CompleteProjectCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/cancel")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [OpenApiOperation("Cancel a project.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new CancelProjectCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.Projects)]
    [OpenApiOperation("Delete a project.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteProjectCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("statuses")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [OpenApiOperation("Get a list of all project statuses.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ProjectStatusDto>>> GetProjectStatuses(CancellationToken cancellationToken)
    {
        var items = await _dispatcher.Send(new GetProjectStatusesQuery(), cancellationToken);
        return Ok(items.OrderBy(c => c.Order));
    }

    [HttpGet("{idOrKey}/team")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [OpenApiOperation("Get the team members for a project.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ProjectTeamMemberDto>>> GetProjectTeam(string idOrKey, CancellationToken cancellationToken)
    {
        var team = await _dispatcher.Send(new GetProjectTeamQuery(idOrKey), cancellationToken);

        return team is not null
            ? Ok(team)
            : NotFound();
    }

    [HttpGet("{id}/work-items")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [OpenApiOperation("Get work items for a project.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<WorkItemListDto>>> GetProjectWorkItems(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetProjectWorkItemsQuery(id), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value.OrderBy(w => w.StackRank))
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/lifecycle")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [OpenApiOperation("Assign a lifecycle to a project.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> AssignLifecycle(Guid id, [FromBody] AssignProjectLifecycleRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/lifecycle/change")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [OpenApiOperation("Change a project's lifecycle, remapping tasks between phases.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ChangeProjectLifecycle(
        Guid id,
        [FromBody] ChangeProjectLifecycleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{id}/phases")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [OpenApiOperation("Get phases for a project.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ProjectPhaseListDto>>> GetProjectPhases(Guid id, CancellationToken cancellationToken)
    {
        var phases = await _dispatcher.Send(new GetProjectPhasesQuery(id), cancellationToken);

        return Ok(phases);
    }

    [HttpGet("{idOrKey}/plan-tree")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [OpenApiOperation("Get a unified plan tree with phases as top-level nodes and tasks nested within.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ProjectPlanNodeDto>>> GetProjectPlanTree(string idOrKey, CancellationToken cancellationToken)
    {
        var nodes = await _dispatcher.Send(new GetProjectPlanTreeQuery(idOrKey), cancellationToken);

        return Ok(nodes);
    }

    [HttpGet("{idOrKey}/plan-summary")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [OpenApiOperation("Get summary metrics for a project's plan, computed from leaf tasks.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProjectPlanSummaryDto>> GetProjectPlanSummary(string idOrKey, [FromQuery] Guid? employeeId, CancellationToken cancellationToken)
    {
        var summary = await _dispatcher.Send(new GetProjectPlanSummaryQuery(idOrKey, employeeId), cancellationToken);

        return Ok(summary);
    }

    [HttpGet("plan-summaries")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [OpenApiOperation("Get plan summary metrics for multiple projects in a single request.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<Dictionary<Guid, ProjectPlanSummaryDto>>> GetProjectsPlanSummaries([FromQuery] Guid[] projectId, [FromQuery] int[]? role, CancellationToken cancellationToken)
    {
        var roleFilter = ParseRoleFilter(role);

        var summaries = await _dispatcher.Send(new GetProjectsPlanSummariesQuery(projectId, roleFilter), cancellationToken);

        return Ok(summaries);
    }

    [HttpGet("{id}/phases/{phaseId}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [OpenApiOperation("Get project phase details.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectPhaseDetailsDto>> GetProjectPhase(Guid id, Guid phaseId, CancellationToken cancellationToken)
    {
        var phase = await _dispatcher.Send(new GetProjectPhaseQuery(id, phaseId), cancellationToken);

        return phase is not null
            ? Ok(phase)
            : NotFound();
    }

    [HttpPut("{id}/phases/{phaseId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [OpenApiOperation("Update a project phase.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateProjectPhase(Guid id, Guid phaseId, [FromBody] UpdateProjectPhaseRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(id, phaseId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPatch("{id}/phases/{phaseId}")]
    [Consumes("application/json", "application/json-patch+json")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [OpenApiOperation("Partially update a project phase using JSON Patch (RFC 6902).", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> PatchProjectPhase(
        Guid id,
        Guid phaseId,
        [FromBody] JsonPatchDocument<UpdateProjectPhaseRequest> patchDocument,
        CancellationToken cancellationToken)
    {
        if (patchDocument == null)
            return BadRequest("Patch document cannot be null.");

        var phaseDto = await _dispatcher.Send(new GetProjectPhaseQuery(id, phaseId), cancellationToken);
        if (phaseDto is null)
            return NotFound($"Project phase with ID '{phaseId}' not found.");

        var updateRequest = UpdateProjectPhaseRequest.FromDto(phaseDto);

        patchDocument.ApplyTo(updateRequest, error =>
        {
            ModelState.AddModelError(error.AffectedObject.GetType().Name, error.ErrorMessage);
        });

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (!TryValidateModel(updateRequest))
            return ValidationProblem(ModelState);

        var result = await _dispatcher.Send(updateRequest.ToCommand(id, phaseId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    private static ProjectStatus[]? ParseStatusFilter(int[]? values)
    {
        if (values is not { Length: > 0 }) return null;

        var parsed = new ProjectStatus[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            if (!Enum.IsDefined(typeof(ProjectStatus), values[i]))
                return null;
            parsed[i] = (ProjectStatus)values[i];
        }
        return parsed;
    }

    private static ProjectMemberRole[]? ParseRoleFilter(int[]? values)
    {
        if (values is not { Length: > 0 }) return null;

        var parsed = new ProjectMemberRole[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            if (!Enum.IsDefined(typeof(ProjectMemberRole), values[i]))
                return null;
            parsed[i] = (ProjectMemberRole)values[i];
        }
        return parsed;
    }
}
