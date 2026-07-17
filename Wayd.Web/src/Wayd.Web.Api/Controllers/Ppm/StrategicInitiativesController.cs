using Wayd.Common.Application.Models;
using Wayd.ProjectPortfolioManagement.Application.Projects.Dtos;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Commands.Kpis;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Dtos;
using Wayd.ProjectPortfolioManagement.Application.StrategicInitiatives.Queries;
using Wayd.ProjectPortfolioManagement.Domain.Enums;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Ppm.StrategicInitiatives;

namespace Wayd.Web.Api.Controllers.Ppm;

[Route("api/ppm/strategic-initiatives")]
[ApiVersionNeutral]
[ApiController]
public class StrategicInitiativesController(ILogger<StrategicInitiativesController> logger, IDispatcher dispatcher) : ControllerBase
{
    private readonly ILogger<StrategicInitiativesController> _logger = logger;
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StrategicInitiatives)]
    [OpenApiOperation("Get a list of strategic initiatives.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<StrategicInitiativeListDto>>> GetStrategicInitiatives([FromQuery] int[]? status, [FromQuery] Guid? portfolioId, CancellationToken cancellationToken)
    {
        StrategicInitiativeStatus[]? filter = status is { Length: > 0 }
            ? [.. status.Select(s => (StrategicInitiativeStatus)s)]
            : null;

        IdOrKey? portfolioIdOrKey = portfolioId.HasValue
            ? new IdOrKey(portfolioId.Value)
            : null;

        var initiatives = await _dispatcher.Send(new GetStrategicInitiativesQuery(StatusFilter: filter, PortfolioIdOrKey: portfolioIdOrKey), cancellationToken);

        return Ok(initiatives);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StrategicInitiatives)]
    [OpenApiOperation("Get strategic initiative details.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StrategicInitiativeDetailsDto>> GetStrategicInitiative(string idOrKey, CancellationToken cancellationToken)
    {
        var initiative = await _dispatcher.Send(new GetStrategicInitiativeQuery(idOrKey), cancellationToken);

        return initiative is not null
            ? Ok(initiative)
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
    [OpenApiOperation("Approve a strategic initiative.", "")]
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
    [OpenApiOperation("Activate a strategic initiative.", "")]
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
    [OpenApiOperation("Complete a strategic initiative.", "")]
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
    [OpenApiOperation("Cancel a strategic initiative.", "")]
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
    [OpenApiOperation("Get a list of all strategic initiative statuses.", "")]
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
    [OpenApiOperation("Get a list of KPIs for a strategic initiative.", "")]
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
    [OpenApiOperation("Get a KPI for a strategic initiative.", "")]
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
    [OpenApiOperation("Get the checkpoints for a strategic initiative KPI.", "")]
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
    [OpenApiOperation("Get the checkpoint plan for a strategic initiative KPI. The checkpoint plan provides the checkpoints and their corresponding measurements.", "")]
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
    [OpenApiOperation("Get the measurements for a strategic initiative KPI.", "")]
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
    [OpenApiOperation("Add a measurement to the strategic initiative KPI.", "")]
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
    [OpenApiOperation("Remove a measurement from the strategic initiative KPI.", "")]
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
    [OpenApiOperation("Get a list of projects for the strategic initiative.", "")]
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
