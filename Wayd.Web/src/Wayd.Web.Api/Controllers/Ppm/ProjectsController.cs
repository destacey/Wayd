using CsvHelper;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Domain.FeatureManagement;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Application.Projects.Commands;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Application.Projects.Imports;
using Wayd.ProjectPortfolioManagement.Application.Projects.Models;
using Wayd.ProjectPortfolioManagement.Application.Projects.Queries;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Commands;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Dtos;
using Wayd.ProjectPortfolioManagement.Application.ProjectTasks.Imports;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Web.Api.Dtos.Ppm;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Ppm.ProjectLifecycles;
using Wayd.Web.Api.Models.Ppm.Projects;
using Wayd.Web.Api.Models.Ppm.ProjectTasks;
using Wayd.Work.Application.WorkItems.Dtos;
using Wayd.Work.Application.WorkItems.Forecasting;
using Wayd.Work.Application.WorkItems.Queries;

namespace Wayd.Web.Api.Controllers.Ppm;

[Route("api/ppm/[controller]")]
[ApiVersionNeutral]
[ApiController]
[McpTools(McpToolset.Ppm, "wayd-ppm")]
public class ProjectsController(ILogger<ProjectsController> logger, IDispatcher dispatcher, ICsvService csvService) : ControllerBase
{
    private readonly ILogger<ProjectsController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICsvService _csvService = csvService;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [EndpointSummary("Get a list of projects.")]
    [EndpointDescription(
        "A role filter narrows the list to projects where an employee holds one of the roles: the caller's linked employee by default, or the employee named by employeeId. An employeeId on its own lists every project that employee is involved in. For the caller's own projects (\"where am I the project manager?\"), pass the role and leave employeeId out: there is no need to look yourself up first.")]
    [McpTool("Projects_GetProjects", "List projects")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ProjectListDto>>> GetProjects([FromQuery] ProjectStatus[]? status, [FromQuery] Guid? portfolioId, [FromQuery] ProjectMemberRole[]? role, [FromQuery] Guid? employeeId, CancellationToken cancellationToken)
    {
        IdOrKey? portfolioIdOrKey = portfolioId.HasValue
            ? new IdOrKey(portfolioId.Value)
            : null;

        var projects = await _dispatcher.Send(new GetProjectsQuery(StatusFilter: status, PortfolioIdOrKey: portfolioIdOrKey, RoleFilter: role, EmployeeId: employeeId), cancellationToken);

        return projects is not null
            ? Ok(projects)
            : NotFound();
    }

    [HttpGet("my-summary")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [EndpointSummary(
        "Get a summary of the current user's project involvement, as counts per role (total, sponsor, owner, manager, member, assignee).")]
    [EndpointDescription("Scoped to the caller — no user parameter.")]
    [McpTool("Projects_GetMyProjectsSummary", "Get my project involvement summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<MyProjectsSummaryDto>> GetMyProjectsSummary([FromQuery] ProjectStatus[]? status, CancellationToken cancellationToken)
    {
        var summary = await _dispatcher.Send(new GetMyProjectsSummaryQuery(StatusFilter: status), cancellationToken);

        return summary is not null
            ? Ok(summary)
            : Ok(new MyProjectsSummaryDto());
    }

    [HttpGet("my-task-metrics")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [EndpointSummary(
        "Get aggregated open-task counts across the current user's projects: overdue, due this week (through Saturday), and upcoming (next Sunday through Saturday).")]
    [EndpointDescription("Scoped to the caller — no user parameter.")]
    [McpTool("Projects_GetMyProjectsTaskMetrics", "Get my project task metrics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProjectsTaskMetricsDto>> GetMyProjectsTaskMetrics([FromQuery] ProjectStatus[]? status, [FromQuery] ProjectMemberRole[]? role, CancellationToken cancellationToken)
    {
        return Ok(await _dispatcher.Send(new GetProjectsTaskMetricsQuery(StatusFilter: status, RoleFilter: role), cancellationToken));
    }

    [HttpGet("task-metrics")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [EndpointSummary(
        "Get aggregated open-task counts across the projects an employee is involved in: overdue, due this week (through Saturday), and upcoming (next Sunday through Saturday).")]
    [EndpointDescription(
        "Pass employeeId for another person; omit it for the caller. On a project where the employee holds a leadership role (Sponsor, Owner or Manager) every task counts, otherwise only the tasks assigned to them.")]
    [McpTool("Projects_GetTaskMetrics", "Get an employee's project task metrics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProjectsTaskMetricsDto>> GetProjectsTaskMetrics([FromQuery] ProjectStatus[]? status, [FromQuery] ProjectMemberRole[]? role, [FromQuery] Guid? employeeId, CancellationToken cancellationToken)
    {
        return Ok(await _dispatcher.Send(new GetProjectsTaskMetricsQuery(StatusFilter: status, RoleFilter: role, EmployeeId: employeeId), cancellationToken));
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [EndpointSummary("Get project details.")]
    [McpTool("Projects_GetProject", "Get project")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDetailsDto>> GetProject(string idOrKey, CancellationToken cancellationToken)
    {
        var project = await _dispatcher.Send(new GetProjectQuery(idOrKey), cancellationToken);

        return project is not null
            ? Ok(project)
            : NotFound();
    }

    [HttpGet("{id}/status-history")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [EndpointSummary("Get the project's status change history.")]
    [EndpointDescription(
        "Each entry records the status moved out of (null for the project's initial state), the status moved into, who made the change, when, and an optional reason. Entries are flagged as recorded live or reconstructed from the audit trail.")]
    [McpTool("Projects_GetStatusHistory", "Get project status history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProjectStatusHistoryDto>>> GetStatusHistory(Guid id, CancellationToken cancellationToken)
    {
        var history = await _dispatcher.Send(new GetProjectStatusHistoryQuery(id), cancellationToken);

        return Ok(history);
    }

    [HttpGet("{idOrKey}/activities")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [EndpointSummary(
        "Get a project's activity history, newest first: every change recorded on the project itself — details, key, program, lifecycle, timeline, roles, strategic themes, status, health checks and scores.")]
    [EndpointDescription(
        "Tasks and stages are not included. Prefer this over `Projects_GetStatusHistory` when asking what changed beyond status. Each entry has a `category` (Created, Updated, ScheduleChanged, StatusChanged, StateChanged, Health, Removed, Baseline), an `actorKind` (User, System, Import, Sync, Anonymous) with the acting `employee` when there is one, a `timestamp`, a one-line `summary`, and a `payload`: the event's fields as a JSON string. A change carries both ends, the value before and after. People in a payload are employee ids, not user ids. A Baseline entry marks where tracking began for a record that already existed, holding what it looked like then; nothing before it was recorded. An entry with `isRelated: true` was raised on another record and is listed here because it concerns this one; `raisedOn` names that record, or is null where it could not be resolved (typically removed since). Paged: the response carries `totalCount` and `hasNextPage`.")]
    [McpTool("Projects_GetActivities", "Get project activity history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<ActivityLogDto>>> GetActivities(string idOrKey, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.Send(new GetProjectActivitiesQuery(new ProjectIdOrKey(idOrKey), page, pageSize), cancellationToken);

        return result.Value is not null
            ? Ok(result.Value)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Projects)]
    [EndpointSummary("Create a project in a portfolio, optionally inside a program.")]
    [EndpointDescription(
        "It starts in Proposed status. Approving it later requires an assigned lifecycle, and activating it requires a start and end date. `expenditureCategoryId` must be an Active category; ExpenditureCategories_GetOptions lists them. Role lists REPLACE the existing assignments for that role — they do not add to them. An omitted or empty list REMOVES everyone currently holding that role. Always read the current record first and pass back the full membership you intend to keep, including people you are not changing. Changes a record other people rely on, so confirm with the user before calling.")]
    [McpTool("Projects_Create", "Create project", Destructive = false)]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create([FromBody] CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateProjectCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetProject), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("import")]
    [MustHavePermission(ApplicationAction.Import, ApplicationResource.Projects)]
    [EndpointSummary("Submit a csv file of projects to import. Returns the run — 200 once it has finished, 202 while it is still queued or running.")]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [CsvImport(ProjectImportDefinition.ImportKey)]
    public async Task<ActionResult> Import([FromForm, CsvRows(typeof(ImportProjectRequest))] IFormFile file, [FromQuery] Guid? submissionGroupId, [FromQuery] bool validateOnly, [FromServices] ImportSubmissionResponder responder, CancellationToken cancellationToken)
    {
        try
        {
            var importedProjects = _csvService.ReadCsv<ImportProjectRequest>(file.OpenReadStream());

            List<SubmittedImportRow<ImportProjectDto>> rows = [];
            var validator = new ImportProjectRequestValidator();
            foreach (var project in importedProjects)
            {
                var validationResults = await validator.ValidateAsync(project, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        error.ErrorMessage = $"{error.ErrorMessage} (Key: {project.Key})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                }

                rows.Add(new SubmittedImportRow<ImportProjectDto>(project.ImportId, project.ToImportProjectDto()));
            }
            var result = await _dispatcher.Send(new ImportProjectsCommand(rows, submissionGroupId, validateOnly), cancellationToken);

            return result.IsSuccess
                ? await responder.Respond(this, result.Value, cancellationToken)
                : BadRequest(result.ToBadRequestObject(HttpContext));
        }
        catch (CsvHelperException ex)
        {
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(ex.Message, HttpContext));
        }
    }

    /// <summary>
    /// Imports tasks across projects. This lives here rather than on the project-scoped tasks controller
    /// because a single file describes work for many projects, each row naming its own.
    /// </summary>
    [HttpPost("tasks/import")]
    [MustHavePermission(ApplicationAction.Import, ApplicationResource.Projects)]
    [EndpointSummary("Submit a csv file of project tasks to import. Returns the run — 200 once it has finished, 202 while it is still queued or running.")]
    [EndpointDescription("Each row names the project it belongs to, so one file can cover many projects.")]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [CsvImport(ProjectTaskImportDefinition.ImportKey)]
    public async Task<ActionResult> ImportTasks([FromForm, CsvRows(typeof(ImportProjectTaskRequest))] IFormFile file, [FromQuery] Guid? submissionGroupId, [FromQuery] bool validateOnly, [FromServices] ImportSubmissionResponder responder, CancellationToken cancellationToken)
    {
        try
        {
            var importedTasks = _csvService.ReadCsv<ImportProjectTaskRequest>(file.OpenReadStream());

            List<SubmittedImportRow<ImportProjectTaskDto>> rows = [];
            var validator = new ImportProjectTaskRequestValidator();
            foreach (var task in importedTasks)
            {
                var validationResults = await validator.ValidateAsync(task, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        error.ErrorMessage = $"{error.ErrorMessage} (Project: {task.ProjectKey}, Task: {task.Name})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                }

                rows.Add(new SubmittedImportRow<ImportProjectTaskDto>(task.ImportId, task.ToImportProjectTaskDto()));
            }
            var result = await _dispatcher.Send(new ImportProjectTasksCommand(rows, submissionGroupId, validateOnly), cancellationToken);

            return result.IsSuccess
                ? await responder.Respond(this, result.Value, cancellationToken)
                : BadRequest(result.ToBadRequestObject(HttpContext));
        }
        catch (CsvHelperException ex)
        {
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(ex.Message, HttpContext));
        }
    }

    /// <summary>
    /// Sets the status of project stages across projects. The status is applied exactly as supplied — the
    /// import does not derive a stage's status from its tasks — so a caller keeps full control over stage
    /// status. Companion to the task import, which does not touch stage status.
    /// </summary>
    [HttpPost("stages/import")]
    [MustHavePermission(ApplicationAction.Import, ApplicationResource.Projects)]
    [EndpointSummary("Submit a csv file of project stage statuses to import. Returns the run — 200 once it has finished, 202 while it is still queued or running.")]
    [EndpointDescription("Each row names the project and stage it sets, so one file can cover many projects.")]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [CsvImport(ProjectStageImportDefinition.ImportKey)]
    public async Task<ActionResult> ImportStages([FromForm, CsvRows(typeof(ImportProjectStageRequest))] IFormFile file, [FromQuery] Guid? submissionGroupId, [FromQuery] bool validateOnly, [FromServices] ImportSubmissionResponder responder, CancellationToken cancellationToken)
    {
        try
        {
            var importedStages = _csvService.ReadCsv<ImportProjectStageRequest>(file.OpenReadStream());

            List<SubmittedImportRow<ImportProjectStageDto>> rows = [];
            var validator = new ImportProjectStageRequestValidator();
            foreach (var stage in importedStages)
            {
                var validationResults = await validator.ValidateAsync(stage, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        error.ErrorMessage = $"{error.ErrorMessage} (Project: {stage.ProjectKey}, Stage: {stage.StageName})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                }

                rows.Add(new SubmittedImportRow<ImportProjectStageDto>(stage.ImportId, stage.ToImportProjectStageDto()));
            }
            var result = await _dispatcher.Send(new ImportProjectStagesCommand(rows, submissionGroupId, validateOnly), cancellationToken);

            return result.IsSuccess
                ? await responder.Respond(this, result.Value, cancellationToken)
                : BadRequest(result.ToBadRequestObject(HttpContext));
        }
        catch (CsvHelperException ex)
        {
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(ex.Message, HttpContext));
        }
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [EndpointSummary(
        "Update a project's name, description, business case, expected benefits, expenditure category, dates, roles, and strategic themes.")]
    [EndpointDescription(
        "The project's key and program are NOT changed here — use Projects_ChangeKey and Projects_ChangeProgram. The lifecycle is not changed here either — use Projects_AssignLifecycle or Projects_ChangeLifecycle. A changed `expenditureCategoryId` must be an Active category (ExpenditureCategories_GetOptions lists them); the project's current category is accepted even if it has since been archived. This is a whole-record update, not a patch: every field is overwritten from the request body, so omitting a field clears it. For a partial change, use Projects_Patch instead. Read the record first and echo back every value that should stay the same. Role lists REPLACE the existing assignments for that role — they do not add to them. An omitted or empty list REMOVES everyone currently holding that role. Always read the current record first and pass back the full membership you intend to keep, including people you are not changing. The id in the body must match the id path parameter. Requires delivery leadership — the caller must be an Owner or Manager of the record or of an ancestor; a permission claim alone is not enough. Changes a record other people rely on, so confirm with the user before calling.")]
    [McpTool("Projects_Update", "Update project", Idempotent = false)]
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

    [HttpPatch("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [EndpointSummary("Change some of a project's details, roles or strategic themes with a JSON Patch (RFC 6902), leaving the rest as they are.")]
    [EndpointDescription(
        "Prefer this to Projects_Update for any partial change. Paths are the update's camelCase fields: /name, /description, /businessCase, /expectedBenefits, /expenditureCategoryId, /start and /end (set both, or clear both), /sponsorIds, /ownerIds, /managerIds, /memberIds, /strategicThemeIds. `replace` changes a field; `add` with `/managerIds/-` appends one id to a list; to remove from a list, `replace` the whole list with the set that should remain — never remove by index. A field the patch does not name keeps its value. A new /expenditureCategoryId must be an Active category (ExpenditureCategories_GetOptions lists them). Role ids are employee ids. The key, program and lifecycle are not patchable (Projects_ChangeKey, Projects_ChangeProgram, Projects_AssignLifecycle, Projects_ChangeLifecycle). Requires delivery leadership — the caller must be an Owner or Manager of the record or of an ancestor; a permission claim alone is not enough. Changes a record other people rely on, so confirm with the user before calling.")]
    [McpTool("Projects_Patch", "Patch project")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<ActionResult> PatchProject(Guid id, [FromBody] JsonPatchDocument<UpdateProjectRequest> patchDocument, CancellationToken cancellationToken)
        => this.Patch(
            patchDocument,
            ct => _dispatcher.Send(new GetProjectQuery(id), ct),
            $"Project with ID '{id}' not found.",
            UpdateProjectRequest.FromDto,
            (request, ct) => _dispatcher.Send((request with { Id = id }).ToUpdateProjectCommand(), ct),
            cancellationToken);

    [HttpGet("{idOrKey}/health-check-evidence")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [EndpointSummary(
        "Gather the evidence for recommending a project health check in one read: the project, plan summary, overdue tasks with keys, stages flagged when past their end, the delivery forecast, the latest check, and activity since it.")]
    [EndpointDescription(
        "Use it first when recommending a health check. `overdueTasks` are the open tasks the plan summary counts as overdue. A stage with `pastEndNotComplete` ended before `asOf` and is neither completed nor canceled. `forecast` is the Projects_GetForecast result against the project's end, or null with `forecastUnavailableReason` when delivery forecasting is off — leave it out of the classification then. Critical-path evidence is never available (`criticalPathUnavailableReason`); never read that as \"no critical tasks\". `activitySinceLatestCheck` holds the project's own changes after the latest check, newest first, without tasks or stages; `activityTruncated` means there was more. The forecast options are those of Projects_GetForecast. "
        + "Take the worst status any signal supports: Unhealthy for a forecast chance below 50% or an 85% date well past the project's end, a Blocked by Dependency outcome, a large share of tasks overdue, or a stage past its end with substantial work left; AtRisk for a chance of 50–80% or an 85% date just past the end, several overdue tasks, a predecessor with a high shareOfTrialsSettingFinish, or stage progress lagging elapsed time; otherwise Healthy. A forecast whose outcome is neither Forecast nor Blocked by Dependency and that has no dates is left out of the classification too."
        + "Draft a note (at most 1024 characters) citing this evidence and present the status, the signal that set it and the note. Logging is a separate Projects_CreateProjectHealthCheck call, made only after the user confirms and only where that tool is listed.")]
    [McpTool("Projects_GetHealthCheckEvidence", "Get project health-check evidence")]
    [ProducesResponseType(typeof(ProjectHealthCheckEvidenceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectHealthCheckEvidenceDto>> GetHealthCheckEvidence(
        string idOrKey,
        [FromQuery] int? lookbackDays,
        [FromQuery] bool? ignoreDependencies,
        [FromQuery] bool? startedWorkFirst,
        [FromServices] IFeatureFlags featureFlags,
        [FromServices] IDateTimeProvider dateTimeProvider,
        CancellationToken cancellationToken)
    {
        if (lookbackDays is < ForecastOptions.MinLookbackDays or > ForecastOptions.MaxLookbackDays)
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(
                $"lookbackDays must be between {ForecastOptions.MinLookbackDays} and {ForecastOptions.MaxLookbackDays}.", HttpContext));

        var options = new ForecastOptions
        {
            LookbackDays = lookbackDays ?? ForecastOptions.DefaultLookbackDays,
            IgnoreDependencies = ignoreDependencies ?? false,
            StartedWorkFirst = startedWorkFirst ?? true,
        };

        var evidence = await new ProjectHealthCheckEvidenceComposer(_dispatcher, featureFlags, dateTimeProvider)
            .Compose(idOrKey, options, cancellationToken);

        return evidence is not null
            ? Ok(evidence)
            : NotFound();
    }


    [HttpPut("{id}/program")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [EndpointSummary(
        "Move a project into a different program, or out of its program entirely by passing a null programId.")]
    [EndpointDescription(
        "The target program must belong to the project's portfolio. Requires delivery leadership — the caller must be an Owner or Manager of the record or of an ancestor; a permission claim alone is not enough. Changes a record other people rely on, so confirm with the user before calling.")]
    [McpTool("Projects_ChangeProgram", "Change project program", Idempotent = false)]
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
    [EndpointSummary("Change a project's key.")]
    [EndpointDescription(
        "**The key is the project's human-facing identifier** — it appears in task keys and in links people have saved, so changing it invalidates existing references. Only do this when the user has explicitly asked for a rekey. Requires delivery leadership — the caller must be an Owner or Manager of the record or of an ancestor; a permission claim alone is not enough. Changes a record other people rely on, so confirm with the user before calling.")]
    [McpTool("Projects_ChangeKey", "Change project key", Idempotent = false)]
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
    [EndpointSummary("Approve a proposed project.")]
    [EndpointDescription(
        "A lifecycle must be assigned to the project first, and only proposed projects can be approved. Requires delivery leadership — the caller must be an Owner or Manager of the record or of an ancestor; a permission claim alone is not enough. Changes a published status that other people rely on, so confirm with the user before calling. Takes a UUID only, not a key.")]
    [McpTool("Projects_Approve", "Approve project")]
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
    [EndpointSummary("Activate a proposed or approved project.")]
    [EndpointDescription(
        "The project must already have a start and end date. Requires delivery leadership — the caller must be an Owner or Manager of the record or of an ancestor; a permission claim alone is not enough. Changes a published status that other people rely on, so confirm with the user before calling. Takes a UUID only, not a key.")]
    [McpTool("Projects_Activate", "Activate project")]
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
    [EndpointSummary("Complete an active project.")]
    [EndpointDescription(
        "The project must have a start and end date, and only active projects can be completed. Requires delivery leadership — the caller must be an Owner or Manager of the record or of an ancestor; a permission claim alone is not enough. Changes a published status that other people rely on, so confirm with the user before calling. Takes a UUID only, not a key.")]
    [McpTool("Projects_Complete", "Complete project")]
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
    [EndpointSummary("Cancel a project that is not already completed or canceled.")]
    [EndpointDescription(
        "Requires delivery leadership — the caller must be an Owner or Manager of the record or of an ancestor; a permission claim alone is not enough. Changes a published status that other people rely on, so confirm with the user before calling. Takes a UUID only, not a key.")]
    [McpTool("Projects_Cancel", "Cancel project")]
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

    [HttpPost("{id}/revert-status")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [EndpointSummary(
        "Move a project **backwards** to an earlier status — for example reopening a completed or canceled project, or returning an active one to approved.")]
    [EndpointDescription(
        "**Read `backwardStatusTargets` on Projects_GetProject and offer only what it contains** rather than assuming: a status carries the same entry requirements whichever direction it is reached from, so reverting to approved needs a lifecycle assigned and reverting to active needs a start and end date. A project cancelled straight from proposed may therefore only allow proposed. **A reason is required** and is kept in the project's status history. The call is rejected if the project's program or portfolio is closed — reopen the parent first. Requires delivery leadership — the caller must be an Owner or Manager of the record or of an ancestor; a permission claim alone is not enough. Changes a published status that other people rely on, so confirm with the user before calling. Takes a UUID only, not a key.")]
    [McpTool("Projects_RevertStatus", "Revert project status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> RevertStatus(Guid id, [FromBody] RevertProjectStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToRevertProjectStatusCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.Projects)]
    [EndpointSummary("Delete a project.")]
    [EndpointDescription(
        "Permanent: the project's stages, tasks, health checks and scores go with it. Only a project in Proposed status that has never moved out of Proposed can be deleted — one that was approved or activated and later reverted stays undeletable, so cancel it instead (Projects_Cancel). Rejected when the portfolio is archived. Requires the delete permission only, not delivery leadership. Confirm with the user before calling. Takes a UUID only, not a key.")]
    [McpTool("Projects_Delete", "Delete project")]
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
    [EndpointSummary("Get a list of all project statuses.")]
    [McpTool("Projects_GetStatuses", "List project statuses")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ProjectStatusDto>>> GetProjectStatuses(CancellationToken cancellationToken)
    {
        var items = await _dispatcher.Send(new GetProjectStatusesQuery(), cancellationToken);
        return Ok(items.OrderBy(c => c.Order));
    }

    [HttpGet("{idOrKey}/team")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [EndpointSummary("Get the team members for a project.")]
    [McpTool("Projects_GetProjectTeam", "Get project team")]
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
    [EndpointSummary("Get work items for a project.")]
    [McpTool("Projects_GetWorkItems", "List a project's work items")]
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

    [HttpGet("{idOrKey}/forecast")]
    [FeatureGate(FeatureFlags.Names.DeliveryForecasting)]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [EndpointSummary(
        "Forecast when a project's work items will be done, by Monte Carlo simulation of each team's recent throughput and each work item's backlog position and open predecessors.")]
    [EndpointDescription(
        "Returns an `outcome` (Forecast, Done, Not Enough History, Blocked by Dependency, Cannot Forecast, Nothing Remaining); on Forecast, completion `percentiles` (a `date` per `confidence`) and `chanceOfFinishingByTargetDate` (0 to 1) against `targetDate` when given, else the project's planned end. `excludedWorkItems` could not be forecast (see `issues`), which makes the dates a lower bound; `dependencies` gives each predecessor's `shareOfTrialsSettingFinish`. Requires the delivery-forecasting feature flag; returns 404 when it is off.")]
    [McpTool("Projects_GetForecast", "Forecast project completion")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkItemForecastDto>> GetProjectForecast(
        string idOrKey,
        [FromQuery] string? targetDate,
        [FromQuery] int? lookbackDays,
        [FromQuery] bool? ignoreDependencies,
        [FromQuery] bool? startedWorkFirst,
        CancellationToken cancellationToken)
    {
        if (!IsoDateQuery.TryParse(targetDate, out var targetDateOverride))
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(IsoDateQuery.FormatError, HttpContext));

        var project = await _dispatcher.Send(new GetProjectQuery(idOrKey), cancellationToken);
        if (project is null)
            return NotFound();

        var options = new ForecastOptions
        {
            LookbackDays = lookbackDays ?? ForecastOptions.DefaultLookbackDays,
            IgnoreDependencies = ignoreDependencies ?? false,
            StartedWorkFirst = startedWorkFirst ?? true,
        };

        return Ok(await _dispatcher.Send(new GetProjectForecastQuery(project.Id, targetDateOverride ?? project.End, options), cancellationToken));
    }

    [HttpPost("{id}/lifecycle")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [EndpointSummary("Assign a lifecycle to a project that has none, creating the project's stages from it.")]
    [EndpointDescription(
        "Only for a project with no lifecycle yet — when one is assigned, use Projects_ChangeLifecycle. `lifecycleId` must be an Active lifecycle (ProjectLifecycles_GetProjectLifecycles with state Active). Each lifecycle stage becomes a project stage, in order, ready to hold tasks; approving a project (Projects_Approve) requires a lifecycle. Rejected for a completed or canceled project. Requires delivery leadership — the caller must be an Owner or Manager of the record or of an ancestor; a permission claim alone is not enough. Confirm with the user before calling. Takes the project's UUID only, not a key.")]
    [McpTool("Projects_AssignLifecycle", "Assign project lifecycle")]
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
    [EndpointSummary("Change a project's lifecycle, remapping tasks between stages.")]
    [EndpointDescription(
        "**Replaces every stage of the project**: the current stages are removed with their status, dates, progress and assignees, and fresh stages are created from the new lifecycle; tasks are kept and moved. `lifecycleId` must be an Active lifecycle different from the current one. `stageMapping` maps each current project stage id (Projects_GetProjectStages) to the id of a stage in the new lifecycle (ProjectLifecycles_GetProjectLifecycle); every current stage that holds tasks must be mapped, and at least one entry is required. Tasks in an unmapped stage go to the new lifecycle's first stage. Only for a project that already has a lifecycle — otherwise use Projects_AssignLifecycle. Rejected for a completed or canceled project. Requires delivery leadership — the caller must be an Owner or Manager of the record or of an ancestor; a permission claim alone is not enough. Show the user the mapping and the stage data that will be lost, and confirm before calling. Takes the project's UUID only, not a key.")]
    [McpTool("Projects_ChangeLifecycle", "Change project lifecycle")]
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

    [HttpGet("{id}/stages")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [EndpointSummary("Get stages for a project.")]
    [McpTool("Projects_GetProjectStages", "List project stages")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ProjectStageListDto>>> GetProjectStages(Guid id, CancellationToken cancellationToken)
    {
        var stages = await _dispatcher.Send(new GetProjectStagesQuery(id), cancellationToken);

        return Ok(stages);
    }

    [HttpGet("{idOrKey}/plan-tree")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [EndpointSummary("Get a unified plan tree with stages as top-level nodes and tasks nested within.")]
    [EndpointDescription("Returns both stage nodes and task nodes with WBS codes.")]
    [McpTool("Projects_GetProjectPlanTree", "Get project plan tree")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ProjectPlanNodeDto>>> GetProjectPlanTree(string idOrKey, CancellationToken cancellationToken)
    {
        var nodes = await _dispatcher.Send(new GetProjectPlanTreeQuery(idOrKey), cancellationToken);

        return Ok(nodes);
    }

    [HttpGet("{idOrKey}/plan-summary")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [EndpointSummary("Get summary metrics for a project's plan, computed from leaf tasks.")]
    [EndpointDescription("Includes overdue, due this week, upcoming, and total task counts.")]
    [McpTool("Projects_GetProjectPlanSummary", "Get project plan summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProjectPlanSummaryDto>> GetProjectPlanSummary(string idOrKey, [FromQuery] Guid? employeeId, CancellationToken cancellationToken)
    {
        var summary = await _dispatcher.Send(new GetProjectPlanSummaryQuery(idOrKey, employeeId), cancellationToken);

        return Ok(summary);
    }

    [HttpGet("plan-summaries")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [EndpointSummary("Get plan summary metrics for multiple projects in one request, keyed by project ID.")]
    [EndpointDescription(
        "Prefer this over calling Projects_GetProjectPlanSummary once per project when surveying several projects. Counts the tasks the caller can see (every task where they lead the project, otherwise their own), or another employee's with employeeId, or every task on the projects with allTasks.")]
    [McpTool("Projects_GetProjectsPlanSummaries", "Get plan summaries for several projects")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<Dictionary<Guid, ProjectPlanSummaryDto>>> GetProjectsPlanSummaries([FromQuery] Guid[] projectId, [FromQuery] ProjectMemberRole[]? role, [FromQuery] Guid? employeeId, [FromQuery] bool allTasks, CancellationToken cancellationToken)
    {
        var summaries = await _dispatcher.Send(new GetProjectsPlanSummariesQuery(projectId, role, employeeId, allTasks), cancellationToken);

        return Ok(summaries);
    }

    [HttpGet("{id}/stages/{stageId}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Projects)]
    [EndpointSummary("Get project stage details.")]
    [McpTool("Projects_GetProjectStage", "Get project stage")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectStageDetailsDto>> GetProjectStage(Guid id, Guid stageId, CancellationToken cancellationToken)
    {
        var stage = await _dispatcher.Send(new GetProjectStageQuery(id, stageId), cancellationToken);

        return stage is not null
            ? Ok(stage)
            : NotFound();
    }

    [HttpPut("{id}/stages/{stageId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [EndpointSummary("Update a project stage's description, status, planned dates, progress and assignees.")]
    [EndpointDescription(
        "This is a whole-record update, not a patch: every field is overwritten from the request body, so an omitted date is cleared and an omitted progress becomes 0. For a partial change, use Projects_PatchStage instead. Read the stage first (Projects_GetProjectStage) and echo back every value that should stay the same. `status` is a task status id from Tasks_GetTaskStatuses. `plannedStart` and `plannedEnd` are set together or both left null, and must cover the dated top-level tasks in the stage. `assigneeIds` REPLACES the stage's assignees — an omitted or empty list removes every assignee; only an explicit null leaves them unchanged. Assignee ids are employee ids. The stage's name comes from its lifecycle and cannot be changed here. Requires the project update permission. Confirm with the user before calling. Takes the project's and the stage's UUIDs, not keys.")]
    [McpTool("Projects_UpdateStage", "Update a project stage")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateProjectStage(Guid id, Guid stageId, [FromBody] UpdateProjectStageRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(id, stageId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPatch("{id}/stages/{stageId}")]
    [Consumes("application/json", "application/json-patch+json")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Projects)]
    [EndpointSummary("Partially update a project stage using JSON Patch (RFC 6902).")]
    [EndpointDescription(
        "Paths are the update's camelCase fields: /description, /status (a task status id from Tasks_GetTaskStatuses), /plannedStart and /plannedEnd (set both, or clear both), /progress, /assigneeIds. `replace` changes a field; `add` with `/assigneeIds/-` appends one id; to remove from the list, `replace` it with the set that should remain — never remove by index. A field the patch does not name keeps its value. Assignee ids are employee ids. Confirm with the user before calling.")]
    [McpTool("Projects_PatchStage", "Patch a project stage")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<ActionResult> PatchProjectStage(
        Guid id,
        Guid stageId,
        [FromBody] JsonPatchDocument<UpdateProjectStageRequest> patchDocument,
        CancellationToken cancellationToken)
        => this.Patch(
            patchDocument,
            ct => _dispatcher.Send(new GetProjectStageQuery(id, stageId), ct),
            $"Project stage with ID '{stageId}' not found.",
            UpdateProjectStageRequest.FromDto,
            (request, ct) => _dispatcher.Send(request.ToCommand(id, stageId), ct),
            cancellationToken);
}
