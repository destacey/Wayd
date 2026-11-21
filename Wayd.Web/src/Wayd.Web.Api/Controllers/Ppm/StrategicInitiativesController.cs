using CsvHelper;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Dtos;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Imports;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Queries;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Ppm.StrategicInitiatives;

namespace Wayd.Web.Api.Controllers.Ppm;

[Route("api/ppm/strategic-initiatives")]
[ApiController]
[McpTools(McpToolset.Ppm, "wayd-ppm")]
public class StrategicInitiativesController(ILogger<StrategicInitiativesController> logger, IDispatcher dispatcher, ICsvService csvService) : ControllerBase
{
    private readonly ILogger<StrategicInitiativesController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICsvService _csvService = csvService;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StrategicInitiatives)]
    [EndpointSummary("Get a list of strategic initiatives.")]
    [EndpointDescription("Optionally filter by status and/or portfolioId.")]
    [McpTool("StrategicInitiatives_GetStrategicInitiatives", "List strategic initiatives")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<StrategicInitiativeListDto>>> GetStrategicInitiatives([FromQuery] StrategicInitiativeStatus[]? status, [FromQuery] Guid? portfolioId, CancellationToken cancellationToken)
    {
        IdOrKey? portfolioIdOrKey = portfolioId.HasValue
            ? new IdOrKey(portfolioId.Value)
            : null;

        var initiatives = await _dispatcher.Send(new GetStrategicInitiativesQuery(StatusFilter: status, PortfolioIdOrKey: portfolioIdOrKey), cancellationToken);

        return Ok(initiatives);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StrategicInitiatives)]
    [EndpointSummary("Get strategic initiative details, including its portfolio, date range, sponsors, and owners.")]
    [McpTool("StrategicInitiatives_GetStrategicInitiative", "Get strategic initiative")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StrategicInitiativeDetailsDto>> GetStrategicInitiative(string idOrKey, CancellationToken cancellationToken)
    {
        var initiative = await _dispatcher.Send(new GetStrategicInitiativeQuery(idOrKey), cancellationToken);

        return initiative is not null
            ? Ok(initiative)
            : NotFound();
    }

    [HttpGet("{idOrKey}/activities")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StrategicInitiatives)]
    [EndpointSummary(
        "Get a strategic initiative's activity history, newest first: every change recorded on the initiative — details, roles, timeline, status, linked projects, and its KPIs with their targets, checkpoint plans and measurements.")]
    [EndpointDescription(
        "Each entry has a `category` (Created, Updated, ScheduleChanged, StatusChanged, StateChanged, Health, Removed, Baseline), an `actorKind` (User, System, Import, Sync, Anonymous) with the acting `employee` when there is one, a `timestamp`, a one-line `summary`, and a `payload`: the event's fields as a JSON string. A change carries both ends, the value before and after. People in a payload are employee ids, not user ids. A Baseline entry marks where tracking began for a record that already existed, holding what it looked like then; nothing before it was recorded. An entry with `isRelated: true` was raised on another record and is listed here because it concerns this one; `raisedOn` names that record, or is null where it could not be resolved (typically removed since). Paged: the response carries `totalCount` and `hasNextPage`.")]
    [McpTool("StrategicInitiatives_GetActivities", "Get strategic initiative activity history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResponse<ActivityLogDto>>> GetActivities(string idOrKey, [FromQuery] int page = PageRequest.FirstPage, [FromQuery] int pageSize = PageSizes.Default, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.Send(new GetStrategicInitiativeActivitiesQuery(new IdOrKey(idOrKey), new PageRequest(page, pageSize)), cancellationToken);

        return result.Value is not null
            ? Ok(result.Value)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.StrategicInitiatives)]
    [EndpointSummary("Create a strategic initiative.")]
    [EndpointDescription(
        "It starts in Proposed status. `portfolioId` must be an active or on-hold portfolio (Portfolios_GetPortfolioOptions resolves a name). `start` and `end` are calendar dates, end on or after start. `sponsorIds` and `ownerIds` are employee ids. Add KPIs afterwards with Kpis_AddKpi (subjectType `ppm.strategic-initiative`) and link projects with StrategicInitiatives_ManageProjects. Returns the new initiative's id and key. Confirm with the user before calling.")]
    [McpTool("StrategicInitiatives_Create", "Create strategic initiative", Destructive = false)]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create([FromBody] CreateStrategicInitiativeRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateStrategicInitiativeCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetStrategicInitiative), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    /// <summary>
    /// Imports strategic initiatives and, optionally, their KPIs. Both files are taken in one call because
    /// KPIs cannot be added to an initiative once it is closed, so they have to land before each
    /// initiative is driven to its final status.
    /// </summary>
    [HttpPost("import")]
    [MustHavePermission(ApplicationAction.Import, ApplicationResource.StrategicInitiatives)]
    [EndpointSummary("Submit a csv file of strategic initiatives to import. Returns the run — 200 once it has finished, 202 while it is still queued or running.")]
    [EndpointDescription("Optionally accepts a second csv of KPIs, whose rows name the ImportId of the initiative they belong to.")]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [CsvImport(StrategicInitiativeImportDefinition.ImportKey)]
    public async Task<ActionResult> Import(
        [FromForm, CsvRows(typeof(ImportStrategicInitiativeRequest))] IFormFile file,
        [FromForm, CsvRows(typeof(ImportStrategicInitiativeKpiRequest), Label = "KPIs")] IFormFile? kpiFile, [FromQuery] Guid? submissionGroupId, [FromQuery] bool validateOnly, [FromServices] ImportSubmissionResponder responder, CancellationToken cancellationToken)
    {
        try
        {
            var importedInitiatives = _csvService.ReadCsv<ImportStrategicInitiativeRequest>(file.OpenReadStream()).ToList();

            var validator = new ImportStrategicInitiativeRequestValidator();
            foreach (var initiative in importedInitiatives)
            {
                var validationResults = await validator.ValidateAsync(initiative, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        error.ErrorMessage = $"{error.ErrorMessage} (Name: {initiative.Name})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                }
            }

            var kpisByInitiative = new Dictionary<string, List<ImportStrategicInitiativeKpiDto>>(StringComparer.OrdinalIgnoreCase);
            if (kpiFile is not null)
            {
                var importedKpis = _csvService.ReadCsv<ImportStrategicInitiativeKpiRequest>(kpiFile.OpenReadStream());

                var kpiValidator = new ImportStrategicInitiativeKpiRequestValidator();
                foreach (var kpi in importedKpis)
                {
                    var validationResults = await kpiValidator.ValidateAsync(kpi, cancellationToken);
                    if (!validationResults.IsValid)
                    {
                        foreach (var error in validationResults.Errors)
                        {
                            error.ErrorMessage = $"{error.ErrorMessage} (Strategic Initiative: {kpi.StrategicInitiativeImportId}, KPI: {kpi.Name})";
                            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                        }
                        return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                    }

                    var parent = kpi.StrategicInitiativeImportId.Trim();
                    if (!kpisByInitiative.TryGetValue(parent, out var forInitiative))
                        kpisByInitiative[parent] = forInitiative = [];

                    forInitiative.Add(kpi.ToImportStrategicInitiativeKpiDto());
                }
            }

            // The same key the run reports row outcomes against, so a KPI row and its initiative row agree
            // on what the parent is called whether or not the file supplied an ImportId.
            List<SubmittedImportRow<ImportStrategicInitiativeDto>> rows = [];
            HashSet<string> claimedParents = new(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < importedInitiatives.Count; i++)
            {
                var initiative = importedInitiatives[i];
                var key = SubmittedImportRow.KeyFor(initiative.ImportId, i + 1);

                claimedParents.Add(key);
                rows.Add(new SubmittedImportRow<ImportStrategicInitiativeDto>(
                    initiative.ImportId,
                    initiative.ToImportStrategicInitiativeDto(
                        kpisByInitiative.TryGetValue(key, out var kpis) ? kpis : [])));
            }

            var orphans = kpisByInitiative.Keys.Where(k => !claimedParents.Contains(k)).ToList();
            if (orphans.Count > 0)
            {
                ModelState.AddModelError(
                    nameof(ImportStrategicInitiativeKpiRequest.StrategicInitiativeImportId),
                    $"The following KPI rows name a strategic initiative that is not in the file: {string.Join(", ", orphans.Select(o => $"'{o}'"))}.");
                return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
            }

            var result = await _dispatcher.Send(new ImportStrategicInitiativesCommand(rows, submissionGroupId, validateOnly), cancellationToken);

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
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StrategicInitiatives)]
    [EndpointSummary("Update a strategic initiative's name, description, dates, sponsors and owners.")]
    [EndpointDescription(
        "This is a whole-record update, not a patch: every field is overwritten from the request body, so read the initiative first (StrategicInitiatives_GetStrategicInitiative) and echo back every value that should stay the same. Role lists REPLACE the existing assignments for that role — they do not add to them. An omitted or empty `sponsorIds` or `ownerIds` REMOVES everyone in that role; only an explicit null leaves it unchanged. Role ids are employee ids. The id in the body must match the id path parameter. Status, portfolio, KPIs and linked projects are not changed here. Checks permissions only, not delivery leadership. Changes a record other people rely on, so confirm with the user before calling. Takes a UUID only, not a key.")]
    [McpTool("StrategicInitiatives_Update", "Update strategic initiative", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateStrategicInitiativeRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateStrategicInitiativeCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/approve")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StrategicInitiatives)]
    [EndpointSummary("Approve a proposed strategic initiative.")]
    [EndpointDescription(
        "Changes a published status that other people rely on, so confirm with the user before calling. Takes a UUID only, not a key.")]
    [McpTool("StrategicInitiatives_Approve", "Approve strategic initiative")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ApproveStrategicInitiativeCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/activate")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StrategicInitiatives)]
    [EndpointSummary("Activate an approved strategic initiative.")]
    [EndpointDescription(
        "Changes a published status that other people rely on, so confirm with the user before calling. Takes a UUID only, not a key.")]
    [McpTool("StrategicInitiatives_Activate", "Activate strategic initiative")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ActivateStrategicInitiativeCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/complete")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StrategicInitiatives)]
    [EndpointSummary("Complete an active or on-hold strategic initiative.")]
    [EndpointDescription(
        "**Completing closes the initiative**, after which its KPIs and linked projects can no longer be added, edited, reordered, or removed. Changes a published status that other people rely on, so confirm with the user before calling. Takes a UUID only, not a key.")]
    [McpTool("StrategicInitiatives_Complete", "Complete strategic initiative")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Complete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new CompleteStrategicInitiativeCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/cancel")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StrategicInitiatives)]
    [EndpointSummary("Cancel a strategic initiative.")]
    [EndpointDescription(
        "**Cancelling closes the initiative**, after which its KPIs and linked projects can no longer be added, edited, reordered, or removed. Changes a published status that other people rely on, so confirm with the user before calling. Takes a UUID only, not a key.")]
    [McpTool("StrategicInitiatives_Cancel", "Cancel strategic initiative")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new CancelStrategicInitiativeCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.StrategicInitiatives)]
    [EndpointSummary("Delete a strategic initiative.")]
    [EndpointDescription(
        "Permanent: its KPIs, checkpoints and measurements go with it, and its links to projects are removed (the projects themselves stay). Only a Proposed or Approved initiative can be deleted; an active or closed one cannot — cancel it instead (StrategicInitiatives_Cancel). Rejected when the portfolio is archived. Confirm with the user before calling. Takes a UUID only, not a key.")]
    [McpTool("StrategicInitiatives_Delete", "Delete strategic initiative")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteStrategicInitiativeCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("statuses")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StrategicInitiatives)]
    [EndpointSummary("Get a list of all strategic initiative statuses.")]
    [EndpointDescription("Call this to resolve the integer enum values used by the status filter.")]
    [McpTool("StrategicInitiatives_GetStatuses", "List strategic initiative statuses")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<StrategicInitiativeStatusDto>>> GetStrategicInitiativeStatuses(CancellationToken cancellationToken)
    {
        var items = await _dispatcher.Send(new GetStrategicInitiativeStatusesQuery(), cancellationToken);
        return Ok(items.OrderBy(c => c.Order));
    }

    #region Projects

    [HttpGet("{idOrKey}/projects")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StrategicInitiatives)]
    [EndpointSummary("Get the projects linked to a strategic initiative — the delivery work carried out to achieve it.")]
    [McpTool("StrategicInitiatives_GetProjects", "List a strategic initiative's projects")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ProjectListDto>>> GetProjects(string idOrKey, CancellationToken cancellationToken)
    {
        var projects = await _dispatcher.Send(new GetStrategicInitiativeProjectsQuery(idOrKey), cancellationToken);

        return projects is not null
            ? Ok(projects)
            : NotFound();
    }

    [HttpPost("{id}/projects")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StrategicInitiatives)]
    [EndpointSummary("Set which projects are linked to a strategic initiative as the work delivering it.")]
    [EndpointDescription(
        "**`projectIds` is the complete set**: projects not in it are unlinked and new ones are linked, so an empty list unlinks every project. Read the current links first (StrategicInitiatives_GetProjects) and include every project that should stay. Every id must be an existing project's UUID. Rejected once the initiative is completed or canceled. The `id` in the body must match the id path parameter. Confirm with the user before calling, naming any project that would be unlinked. Takes UUIDs only, not keys.")]
    [McpTool("StrategicInitiatives_ManageProjects", "Set strategic initiative projects")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ManageProjects(Guid id, [FromBody] ManageStrategicInitiativeProjectsRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToManageStrategicInitiativeProjectsCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #endregion Projects
}
