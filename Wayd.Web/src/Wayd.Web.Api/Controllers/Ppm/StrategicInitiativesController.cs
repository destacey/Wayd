using CsvHelper;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands.Kpis;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Dtos;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Imports;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Queries;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Ppm.StrategicInitiatives;

namespace Wayd.Web.Api.Controllers.Ppm;

[Route("api/ppm/strategic-initiatives")]
[ApiVersionNeutral]
[ApiController]
public class StrategicInitiativesController(ILogger<StrategicInitiativesController> logger, IDispatcher dispatcher, ICsvService csvService) : ControllerBase
{
    private readonly ILogger<StrategicInitiativesController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICsvService _csvService = csvService;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StrategicInitiatives)]
    [OpenApiOperation("Get a list of strategic initiatives.", "Optionally filter by status and/or portfolioId.")]
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
    [OpenApiOperation("Get strategic initiative details, including its portfolio, date range, sponsors, and owners.", "")]
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
    [OpenApiOperation(
        "Get a strategic initiative's activity history, newest first: every change recorded on the initiative — details, roles, timeline, status, linked projects, and its KPIs with their targets, checkpoint plans and measurements.",
        "Each entry has a `category` (Created, Updated, ScheduleChanged, StatusChanged, StateChanged, Health, Removed, Baseline), an `actorKind` (User, System, Import, Sync, Anonymous) with the acting `employee` when there is one, a `timestamp`, a one-line `summary`, and a `payload`: the event's fields as a JSON string. A change carries both ends, the value before and after. People in a payload are employee ids, not user ids. A Baseline entry marks where tracking began for a record that already existed, holding what it looked like then; nothing before it was recorded. An entry with `isRelated: true` was raised on another record and is listed here because it concerns this one; `raisedOn` names that record, or is null where it could not be resolved (typically removed since). Paged: the response carries `totalCount` and `hasNextPage`.")]
    [McpTool("StrategicInitiatives_GetActivities", "Get strategic initiative activity history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<ActivityLogDto>>> GetActivities(string idOrKey, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.Send(new GetStrategicInitiativeActivitiesQuery(new IdOrKey(idOrKey), page, pageSize), cancellationToken);

        return result.Value is not null
            ? Ok(result.Value)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.StrategicInitiatives)]
    [OpenApiOperation("Create a strategic initiative.", "")]
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
    [OpenApiOperation("Submit a csv file of strategic initiatives to import. Returns the run — 200 once it has finished, 202 while it is still queued or running.", "Optionally accepts a second csv of KPIs, whose rows name the ImportId of the initiative they belong to.")]
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
    [OpenApiOperation("Update a strategic initiative.", "")]
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
    [OpenApiOperation(
        "Approve a proposed strategic initiative.",
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
    [OpenApiOperation(
        "Activate an approved strategic initiative.",
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
    [OpenApiOperation(
        "Complete an active or on-hold strategic initiative.",
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
    [OpenApiOperation(
        "Cancel a strategic initiative.",
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
    [OpenApiOperation("Delete a strategic initiative.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteStrategicInitiativeCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("statuses")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StrategicInitiatives)]
    [OpenApiOperation("Get a list of all strategic initiative statuses.", "Call this to resolve the integer enum values used by the status filter.")]
    [McpTool("StrategicInitiatives_GetStatuses", "List strategic initiative statuses")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<StrategicInitiativeStatusDto>>> GetStrategicInitiativeStatuses(CancellationToken cancellationToken)
    {
        var items = await _dispatcher.Send(new GetStrategicInitiativeStatusesQuery(), cancellationToken);
        return Ok(items.OrderBy(c => c.Order));
    }

    #region KPIs

    [HttpGet("{id}/kpis")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StrategicInitiatives)]
    [OpenApiOperation(
        "Get the KPIs for a strategic initiative — the measures that define whether it succeeded.",
        "Each KPI carries a starting (baseline) value, a target value, the latest actual value, and a computed progress percentage toward the target. targetDirection is Increase or Decrease; for a Decrease KPI a falling value is improvement, so never assume a lower number is worse.")]
    [McpTool("StrategicInitiatives_GetKpis", "List a strategic initiative's KPIs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<StrategicInitiativeKpiListDto>>> GetKpis(string id, CancellationToken cancellationToken)
    {
        var kpis = await _dispatcher.Send(new GetStrategicInitiativeKpisQuery(id), cancellationToken);

        return kpis is not null
            ? Ok(kpis)
            : NotFound();
    }

    [HttpGet("{id}/kpis/{kpiId}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StrategicInitiatives)]
    [OpenApiOperation("Get a single KPI for a strategic initiative.", "")]
    [McpTool("StrategicInitiatives_GetKpi", "Get KPI")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StrategicInitiativeKpiDetailsDto>> GetKpi(string id, string kpiId, CancellationToken cancellationToken)
    {
        var kpi = await _dispatcher.Send(new GetStrategicInitiativeKpiQuery(id, kpiId), cancellationToken);

        return kpi is not null
            ? Ok(kpi)
            : NotFound();
    }

    [HttpPost("{id}/kpis")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StrategicInitiatives)]
    [OpenApiOperation("Create a KPI for a strategic initiative.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> CreateKpi(Guid id, [FromBody] CreateStrategicInitiativeKpiRequest request, CancellationToken cancellationToken)
    {
        if (id != request.StrategicInitiativeId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToCreateStrategicInitiativeKpiCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetKpi), new { id = id, kpiId = result.Value }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/kpis/{kpiId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StrategicInitiatives)]
    [OpenApiOperation("Update a KPI for a strategic initiative.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateKpi(Guid id, Guid kpiId, [FromBody] UpdateStrategicInitiativeKpiRequest request, CancellationToken cancellationToken)
    {
        if (id != request.StrategicInitiativeId || kpiId != request.KpiId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateStrategicInitiativeKpiCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}/kpis/{kpiId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StrategicInitiatives)]
    [OpenApiOperation("Delete a KPI for a strategic initiative.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteKpi(Guid id, Guid kpiId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteStrategicInitiativeKpiCommand(id, kpiId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/kpis/reorder")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StrategicInitiatives)]
    [OpenApiOperation("Reorder KPIs for a strategic initiative.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ReorderKpis(Guid id, [FromBody] ReorderStrategicInitiativeKpisRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToReorderStrategicInitiativeKpisCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{id}/kpis/{kpiId}/checkpoints")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StrategicInitiatives)]
    [OpenApiOperation(
        "Get the checkpoints for a KPI — the dated milestones a KPI is expected to hit, each with its own target value and optional at-risk threshold.",
        "Returns the checkpoint definitions only, without the measurements taken against them; use StrategicInitiatives_GetKpiCheckpointPlan for both together.")]
    [McpTool("StrategicInitiatives_GetKpiCheckpoints", "List KPI checkpoints")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<StrategicInitiativeKpiCheckpointDto>>> GetKpiCheckpoints(string id, string kpiId, CancellationToken cancellationToken)
    {
        var checkpoints = await _dispatcher.Send(new GetStrategicInitiativeKpiCheckpointsQuery(id, kpiId), cancellationToken);

        return checkpoints is not null
            ? Ok(checkpoints)
            : NotFound();
    }

    [HttpGet("{id}/kpis/{kpiId}/checkpoints/plan")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StrategicInitiatives)]
    [OpenApiOperation(
        "Get the checkpoint plan for a KPI: every checkpoint paired with the measurement recorded against it, plus a computed health and trend per checkpoint.",
        "This is the best single call for assessing whether a KPI is on track over time. A checkpoint with no measurement has a null measurement, health, and trend.")]
    [McpTool("StrategicInitiatives_GetKpiCheckpointPlan", "Get KPI checkpoint plan")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<StrategicInitiativeKpiCheckpointDetailsDto>>> GetKpiCheckpointPlan(string id, string kpiId, CancellationToken cancellationToken)
    {
        var checkpointPlan = await _dispatcher.Send(new GetStrategicInitiativeKpiCheckpointPlanQuery(id, kpiId), cancellationToken);

        return checkpointPlan is not null
            ? Ok(checkpointPlan)
            : NotFound();
    }

    [HttpPost("{id}/kpis/{kpiId}/checkpoints/plan")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StrategicInitiatives)]
    [OpenApiOperation("Manage the checkpoint plan for a strategic initiative KPI.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ManageKpiCheckpointPlan(Guid id, Guid kpiId, [FromBody] ManageStrategicInitiativeKpiCheckpointPlanRequest request, CancellationToken cancellationToken)
    {
        if (id != request.StrategicInitiativeId || kpiId != request.KpiId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToManageStrategicInitiativeKpiCheckpointPlanCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpGet("{id}/kpis/{kpiId}/measurements")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StrategicInitiatives)]
    [OpenApiOperation("Get every measurement recorded against a KPI, each with its actual value, the date it was taken, who took it, and an optional note.", "")]
    [McpTool("StrategicInitiatives_GetKpiMeasurements", "List KPI measurements")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<StrategicInitiativeKpiMeasurementDto>>> GetKpiMeasurements(string id, string kpiId, CancellationToken cancellationToken)
    {
        var measurements = await _dispatcher.Send(new GetStrategicInitiativeKpiMeasurementsQuery(id, kpiId), cancellationToken);

        return measurements is not null
            ? Ok(measurements)
            : NotFound();
    }

    [HttpPost("{id}/kpis/{kpiId}/measurements")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StrategicInitiatives)]
    [OpenApiOperation(
        "Record a measurement against a KPI — the actual observed value at a point in time.",
        "Measurements accumulate as a history rather than overwriting; the KPI's headline actual value is the measurement with the latest measurementDate. Measurement dates must be unique within a KPI, so re-submitting an existing date is rejected rather than treated as an update. strategicInitiativeId and kpiId in the body must match the path parameters. Unlike the KPI read tools, this takes UUIDs only, not keys.")]
    [McpTool("StrategicInitiatives_AddKpiMeasurement", "Record a KPI measurement", Destructive = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> AddKpiMeasurement(Guid id, Guid kpiId, [FromBody] AddStrategicInitiativeKpiMeasurementRequest request, CancellationToken cancellationToken)
    {
        if (id != request.StrategicInitiativeId || kpiId != request.KpiId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToAddStrategicInitiativeKpiMeasurementCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}/kpis/{kpiId}/measurements/{measurementId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StrategicInitiatives)]
    [OpenApiOperation(
        "Remove a measurement from a KPI.",
        "This deletes the recorded history entry and changes the KPI's derived actual value and progress. To record a new observation, add a measurement instead — deletion is only for correcting a wrong entry, or for freeing up a date so it can be re-recorded.")]
    [McpTool("StrategicInitiatives_RemoveKpiMeasurement", "Remove a KPI measurement")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> RemoveKpiMeasurement(Guid id, Guid kpiId, Guid measurementId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RemoveStrategicInitiativeKpiMeasurementCommand(id, kpiId, measurementId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #endregion KPIs

    #region Projects

    [HttpGet("{idOrKey}/projects")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StrategicInitiatives)]
    [OpenApiOperation("Get the projects linked to a strategic initiative — the delivery work carried out to achieve it.", "")]
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
    [OpenApiOperation("Manage projects for the strategic initiative.", "")]
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
