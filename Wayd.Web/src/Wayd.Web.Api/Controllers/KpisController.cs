using Microsoft.AspNetCore.Authorization;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Kpis;
using Wayd.Common.Application.Kpis.Commands;
using Wayd.Common.Application.Kpis.Dtos;
using Wayd.Common.Application.Kpis.Queries;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.Kpis;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Kpis;

namespace Wayd.Web.Api.Controllers;

/// <summary>
/// The KPIs on a record — a team or a team of teams, a project, a program, a portfolio, and every kind of record that
/// registers a KPI subject type — and their measurements.
/// </summary>
/// <remarks>
/// Needs only a signed-in account: every action asks the record's own subject type what the caller may read and
/// change, from the record as it is now. A record the caller cannot read is 404, never 403.
/// </remarks>
[Route("api/kpis/{subjectType}/{subjectId:guid}")]
[ApiController]
[Authorize]
[FeatureGate(FeatureFlags.Names.Kpis)]
[McpTools(McpToolset.Kpis, "wayd-kpis")]
public class KpisController(IDispatcher dispatcher) : ControllerBase
{
    private const string SubjectDocs =
        "`subjectType` names the kind of record — `organization.team` for a team or a team of teams, `ppm.project`, `ppm.program` or `ppm.portfolio` — and `subjectId` is the record's id; a `subjectType` that cannot carry KPIs returns 400. Returns 404 when the record does not exist or you cannot read it.";

    private const string WindowDocs =
        "The KPI's `window` says what span each value covers: `PerPeriod` (each measurement gives its own `periodStart` and `periodEnd`, and periods do not overlap), `Cumulative` (from a fixed `anchorDate` to the measurement's `asOf` day), `Rolling` (`rollingCount` `rollingUnit`s ending on the `asOf` day) or `PointInTime` (the state at the exact moment `measuredAt`, several a day allowed).";

    private const string ManageDocs =
        "Needs, on a team, membership of the team or of a team of teams above it; on a project, program or portfolio, its Update permission and Owner or Manager on the record or an ancestor (or `Permissions.ProjectPortfolioManagement.Administer` in place of the role); or `Permissions.Kpis.Administer` on any record. Returns 403 otherwise. Returns 409 when someone else changed the KPI at the same moment.";

    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [McpTool("Kpis_GetScorecard", "Get a scorecard")]
    [EndpointSummary("Get a record's scorecard.")]
    [EndpointDescription(
        $"{SubjectDocs} Returns the record's name, whether you may manage its KPIs (`canManage`), and every active KPI ordered by category (uncategorized last), then position. Each KPI carries its `goal` as entered and resolved, its `window`, its `reading` of the latest value — `changeFromBaseline`, `changeFromBaselinePercent`, `progressToTarget` (1 is the target reached), `health` and `trend` — and up to 12 `recentValues`, oldest first. `expressedAs` says which reading the KPI is shown as. When `rollingAverageOf` is set, the reading's `value` is the average of that many latest measurements (`averagedCount` says how many there were) and is what health and trend judge; `latestValue` is always the newest raw measurement.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<KpiScorecardDto>> GetScorecard(string subjectType, Guid subjectId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetKpiScorecardQuery(subjectType, subjectId), cancellationToken);

        if (result.IsFailure)
            return BadRequest(result.ToBadRequestObject(HttpContext));

        return result.Value is not null ? Ok(result.Value) : NotFound();
    }

    [HttpGet("closed")]
    [McpTool("Kpis_GetClosed", "List closed KPIs")]
    [EndpointSummary("Get a record's closed KPIs.")]
    [EndpointDescription($"{SubjectDocs} Returns the KPIs taken off the scorecard, kept with their measurements as history, in scorecard order.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<List<KpiSummaryDto>>> GetClosed(string subjectType, Guid subjectId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetClosedKpisQuery(subjectType, subjectId), cancellationToken);

        if (result.IsFailure)
            return BadRequest(result.ToBadRequestObject(HttpContext));

        return result.Value is not null ? Ok(result.Value) : NotFound();
    }

    [HttpGet("{kpiId:guid}")]
    [McpTool("Kpis_GetKpi", "Get a KPI and its measurements")]
    [EndpointSummary("Get a KPI with every measurement taken.")]
    [EndpointDescription($"{SubjectDocs} Returns the KPI as the scorecard shows it, open or closed, with every measurement, newest period first, and whether you may change it. Returns 404 too when the KPI is on another record.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<KpiDetailsDto>> GetKpi(string subjectType, Guid subjectId, Guid kpiId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetKpiQuery(subjectType, subjectId, kpiId), cancellationToken);

        if (result.IsFailure)
            return BadRequest(result.ToBadRequestObject(HttpContext));

        return result.Value is not null ? Ok(result.Value) : NotFound();
    }

    [HttpPost]
    [McpTool("Kpis_AddKpi", "Add a KPI", Destructive = false)]
    [EndpointSummary("Add a KPI to a record.")]
    [EndpointDescription(
        $"{SubjectDocs} Adds an active KPI, last on the scorecard, and returns its id and key. The `target` is `Absolute` (a value to reach) or `RelativeChange` (a percentage from the baseline, which then needs a baseline value); the baseline must be short of the target in the KPI's direction. An optional at-risk threshold, `Absolute` or `ProgressToTarget`, must also be short of the target. {WindowDocs} Leave `window` out for `PerPeriod`. Only a `PerPeriod` KPI may set `rollingAverageOf` (2 to 12), to be read as the average of its latest measurements, as velocity is read over the last 7 sprints. {ManageDocs} Requires your account to be linked to an employee.")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ObjectIdAndKey>> AddKpi(string subjectType, Guid subjectId, [FromBody] KpiRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new AddKpiCommand(subjectType, subjectId, request.Name, request.Description, request.CategoryId, request.Prefix,
                request.Suffix, request.ExpressedAs, request.Baseline.ToInput(), request.Target.ToInput(), request.Window?.ToInput(),
                request.RollingAverageOf),
            cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetKpi), new { subjectType, subjectId, kpiId = result.Value.Id }, result.Value)
            : ErrorResult(result.Error);
    }

    [HttpPut("{kpiId:guid}")]
    [McpTool("Kpis_UpdateKpi", "Update a KPI")]
    [EndpointSummary("Change a KPI's details, baseline and target.")]
    [EndpointDescription(
        $"{SubjectDocs} Replaces everything about the KPI that is entered rather than measured, in one save: send every field, including the ones that stay. The new goal is checked as a whole, against the same rules as adding a KPI, so a baseline and target may move together. A new category must be active; the one the KPI has may stay. Leave `window` out to keep it; it can change only while the KPI has no measurements, since they were recorded under it. `rollingAverageOf` is replaced like any other field: leave it out to read the latest value alone. {ManageDocs}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateKpi(string subjectType, Guid subjectId, Guid kpiId, [FromBody] KpiRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new UpdateKpiCommand(subjectType, subjectId, kpiId, request.Name, request.Description, request.CategoryId,
                request.Prefix, request.Suffix, request.ExpressedAs, request.Baseline.ToInput(), request.Target.ToInput(),
                request.Window?.ToInput(), request.RollingAverageOf),
            cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }

    [HttpPost("{kpiId:guid}/close")]
    [McpTool("Kpis_CloseKpi", "Close a KPI")]
    [EndpointSummary("Close a KPI, keeping its measurements as history.")]
    [EndpointDescription($"{SubjectDocs} A closed KPI leaves the scorecard and takes no measurements until it is reopened. {ManageDocs}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Close(string subjectType, Guid subjectId, Guid kpiId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new SetKpiStatusCommand(subjectType, subjectId, kpiId, KpiStatus.Closed), cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }

    [HttpPost("{kpiId:guid}/reopen")]
    [McpTool("Kpis_ReopenKpi", "Reopen a KPI")]
    [EndpointSummary("Reopen a closed KPI.")]
    [EndpointDescription($"{SubjectDocs} {ManageDocs}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Reopen(string subjectType, Guid subjectId, Guid kpiId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new SetKpiStatusCommand(subjectType, subjectId, kpiId, KpiStatus.Active), cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }

    [HttpDelete("{kpiId:guid}")]
    [McpTool("Kpis_RemoveKpi", "Remove a KPI")]
    [EndpointSummary("Remove a KPI and its measurements for good.")]
    [EndpointDescription($"{SubjectDocs} Close a KPI instead to keep its history. {ManageDocs}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Remove(string subjectType, Guid subjectId, Guid kpiId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RemoveKpiCommand(subjectType, subjectId, kpiId), cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }

    [HttpPut("order")]
    [McpTool("Kpis_ReorderKpis", "Reorder a scorecard's KPIs")]
    [EndpointSummary("Put a record's KPIs in a new order.")]
    [EndpointDescription($"{SubjectDocs} `kpiIds` lists every KPI on the record, open and closed, each once. {ManageDocs}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Reorder(string subjectType, Guid subjectId, [FromBody] ReorderKpisRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ReorderKpisCommand(subjectType, subjectId, request.KpiIds), cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }

    [HttpPost("{kpiId:guid}/measurements")]
    [McpTool("Kpis_RecordMeasurement", "Record a KPI measurement", Destructive = false)]
    [EndpointSummary("Record a measurement against a KPI.")]
    [EndpointDescription(
        $"{SubjectDocs} Records the raw value on an open KPI, taken by you, and returns its id. {WindowDocs} Give exactly the one the KPI's window takes — a period, an `asOf` day or a `measuredAt` moment — or the request is refused with 400; read the KPI first if you do not know its window. A per-period KPI's periods cannot overlap; a cumulative or rolling KPI takes one value per `asOf` day; a point-in-time KPI one per moment. {ManageDocs} Requires your account to be linked to an employee.")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<Guid>> RecordMeasurement(string subjectType, Guid subjectId, Guid kpiId, [FromBody] KpiMeasurementRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RecordKpiMeasurementCommand(subjectType, subjectId, kpiId, request.ToInput()), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetKpi), new { subjectType, subjectId, kpiId }, result.Value)
            : ErrorResult(result.Error);
    }

    [HttpPut("{kpiId:guid}/measurements/{measurementId:guid}")]
    [McpTool("Kpis_ChangeMeasurement", "Correct a KPI measurement")]
    [EndpointSummary("Correct a measurement.")]
    [EndpointDescription($"{SubjectDocs} Replaces the value and time of a measurement on an open KPI, given as recording it takes them; who took it stays as recorded. {ManageDocs}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ChangeMeasurement(string subjectType, Guid subjectId, Guid kpiId, Guid measurementId, [FromBody] KpiMeasurementRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new ChangeKpiMeasurementCommand(subjectType, subjectId, kpiId, measurementId, request.ToInput()), cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }

    [HttpDelete("{kpiId:guid}/measurements/{measurementId:guid}")]
    [McpTool("Kpis_RemoveMeasurement", "Remove a KPI measurement")]
    [EndpointSummary("Remove a measurement.")]
    [EndpointDescription($"{SubjectDocs} Removes a measurement from an open KPI. {ManageDocs}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> RemoveMeasurement(string subjectType, Guid subjectId, Guid kpiId, Guid measurementId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RemoveKpiMeasurementCommand(subjectType, subjectId, kpiId, measurementId), cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }

    [HttpGet("{kpiId:guid}/checkpoints")]
    [McpTool("Kpis_GetCheckpointPlan", "Get a KPI's checkpoint plan")]
    [EndpointSummary("Get a KPI's checkpoint plan.")]
    [EndpointDescription($"{SubjectDocs} Returns each checkpoint in date order with the latest measurement whose period ends under it — after the checkpoint before, on or before this one — and that measurement's health and trend against the checkpoint. Empty when the KPI has no plan. Returns 404 too when the KPI is on another record.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyList<KpiCheckpointReadingDto>>> GetCheckpointPlan(string subjectType, Guid subjectId, Guid kpiId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new GetKpiCheckpointPlanQuery(subjectType, subjectId, kpiId), cancellationToken);

        if (result.IsFailure)
            return BadRequest(result.ToBadRequestObject(HttpContext));

        return result.Value is not null ? Ok(result.Value) : NotFound();
    }

    [HttpPut("{kpiId:guid}/checkpoints")]
    [McpTool("Kpis_ChangeCheckpointPlan", "Replace a KPI's checkpoint plan")]
    [EndpointSummary("Replace a KPI's checkpoint plan.")]
    [EndpointDescription($"{SubjectDocs} Send the whole plan: a checkpoint on a day already planned is changed, one on a new day added, and a planned day left out removed; an empty list clears it. While a measurement's period ends on or before a checkpoint, its health is judged against the first such checkpoint — its planned value, and its at-risk value or none — instead of the target; past the last checkpoint the target and the KPI's own at-risk threshold apply. A closed KPI's plan cannot change. {ManageDocs}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ChangeCheckpointPlan(string subjectType, Guid subjectId, Guid kpiId, [FromBody] KpiCheckpointPlanRequest request, CancellationToken cancellationToken)
    {
        var plan = request.Checkpoints.Select(c => c.ToValues()).ToList();
        var result = await _dispatcher.Send(new ChangeKpiCheckpointPlanCommand(subjectType, subjectId, kpiId, plan), cancellationToken);

        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }

    private ActionResult ErrorResult(string error)
    {
        if (KpiAccessErrors.IsNotFound(error))
            return NotFound();

        if (KpiAccessErrors.IsForbidden(error))
            return Problem(detail: error, statusCode: StatusCodes.Status403Forbidden);

        if (KpiAccessErrors.IsConflict(error))
            return Problem(detail: error, statusCode: StatusCodes.Status409Conflict);

        return BadRequest(ProblemDetailsExtensions.ForBadRequest(error, HttpContext));
    }
}
