using CsvHelper;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Kpis.Imports;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.Kpis;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Kpis;

namespace Wayd.Web.Api.Controllers;

/// <summary>
/// Bulk import of KPIs onto existing records, each with its history.
/// </summary>
/// <remarks>
/// Apart from <see cref="KpisController"/>, whose routes are one record's: an import spans every record it names.
/// </remarks>
[Route("api/kpis")]
[ApiController]
[FeatureGate(FeatureFlags.Names.Kpis)]
public class KpiImportsController(IDispatcher dispatcher, ICsvService csvService) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICsvService _csvService = csvService;

    [HttpPost("import")]
    [MustHavePermission(ApplicationAction.Administer, ApplicationResource.Kpis)]
    [EndpointSummary(
        "Submit csv files of KPIs, their measurements and their checkpoint plans to import. Returns the run — 200 once it has finished, 202 while it is still queued or running.")]
    [EndpointDescription(
        "Takes one row per KPI, naming the record it goes on by id; one row per measurement naming the ImportId of its KPI; and, optionally, one row per checkpoint naming the ImportId of its KPI. A KPI's WindowKind decides how each of its measurements says when it was taken: PeriodStart and PeriodEnd for PerPeriod, AsOf for Cumulative and Rolling, MeasuredAt for PointInTime. The measurements file may be empty of rows, but it must be sent with its header. A KPI, its measurements and its plan apply together: a value that clashes with another, one given the wrong way for the window, a goal the KPI cannot hold, or a plan with two checkpoints on one day or an at-risk value on the wrong side of its planned value, rejects that KPI alone.")]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [CsvImport(KpiImportDefinition.ImportKey)]
    public async Task<ActionResult> Import(
        [FromForm, CsvRows(typeof(ImportKpiRequest))] IFormFile file,
        [FromForm, CsvRows(typeof(ImportKpiMeasurementRequest), Label = "Measurements")] IFormFile measurementsFile,
        [FromForm, CsvRows(typeof(ImportKpiCheckpointRequest), Label = "Checkpoints")] IFormFile? checkpointsFile,
        [FromQuery] Guid? submissionGroupId, [FromQuery] bool validateOnly,
        [FromServices] ImportSubmissionResponder responder,
        CancellationToken cancellationToken)
    {
        try
        {
            var importedKpis = _csvService.ReadCsv<ImportKpiRequest>(file.OpenReadStream());

            List<ImportKpiRequest> kpis = [];
            var validator = new ImportKpiRequestValidator();
            foreach (var kpi in importedKpis)
            {
                var validationResults = await validator.ValidateAsync(kpi, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        error.ErrorMessage = $"{error.ErrorMessage} (KPI: {kpi.ImportId ?? kpi.Name})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                }

                kpis.Add(kpi);
            }

            var importedMeasurements = _csvService.ReadCsv<ImportKpiMeasurementRequest>(measurementsFile.OpenReadStream());

            var measurementsByKpi = new Dictionary<string, List<ImportKpiMeasurementDto>>(StringComparer.OrdinalIgnoreCase);
            var measurementValidator = new ImportKpiMeasurementRequestValidator();
            foreach (var measurement in importedMeasurements)
            {
                var validationResults = await measurementValidator.ValidateAsync(measurement, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        error.ErrorMessage =
                            $"{error.ErrorMessage} (KPI: {measurement.KpiImportId}, {measurement.Describe()})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                }

                var parent = measurement.KpiImportId.Trim();
                if (!measurementsByKpi.TryGetValue(parent, out var forKpi))
                    measurementsByKpi[parent] = forKpi = [];

                forKpi.Add(measurement.ToImportKpiMeasurementDto());
            }

            var checkpointsByKpi = new Dictionary<string, List<KpiCheckpointValues>>(StringComparer.OrdinalIgnoreCase);
            if (checkpointsFile is not null)
            {
                var importedCheckpoints = _csvService.ReadCsv<ImportKpiCheckpointRequest>(checkpointsFile.OpenReadStream());
                var checkpointValidator = new ImportKpiCheckpointRequestValidator();
                foreach (var checkpoint in importedCheckpoints)
                {
                    var validationResults = await checkpointValidator.ValidateAsync(checkpoint, cancellationToken);
                    if (!validationResults.IsValid)
                    {
                        foreach (var error in validationResults.Errors)
                        {
                            error.ErrorMessage =
                                FormattableString.Invariant($"{error.ErrorMessage} (KPI: {checkpoint.KpiImportId}, Checkpoint: {checkpoint.Date:yyyy-MM-dd})");
                            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                        }
                        return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                    }

                    var parent = checkpoint.KpiImportId.Trim();
                    if (!checkpointsByKpi.TryGetValue(parent, out var forKpi))
                        checkpointsByKpi[parent] = forKpi = [];

                    forKpi.Add(checkpoint.ToValues());
                }
            }

            // The same key the run reports row outcomes against, so a measurement and its KPI row agree on what
            // the parent is called whether or not the file supplied an ImportId.
            List<SubmittedImportRow<ImportKpiDto>> rows = [];
            HashSet<string> claimedParents = new(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < kpis.Count; i++)
            {
                var kpi = kpis[i];
                var key = SubmittedImportRow.KeyFor(kpi.ImportId, i + 1);

                claimedParents.Add(key);
                rows.Add(new SubmittedImportRow<ImportKpiDto>(
                    kpi.ImportId,
                    kpi.ToImportKpiDto(
                        measurementsByKpi.TryGetValue(key, out var forKpi) ? forKpi : [],
                        checkpointsByKpi.TryGetValue(key, out var plan) ? plan : [])));
            }

            // A measurement naming a KPI that is not in the file is a mistyped reference rather than a value to
            // drop, so it fails the submission.
            var orphaned = measurementsByKpi.Keys.Where(k => !claimedParents.Contains(k)).ToList();
            if (orphaned.Count > 0)
            {
                ModelState.AddModelError(
                    nameof(ImportKpiMeasurementRequest.KpiImportId),
                    $"The following measurements name a KPI that is not in the file: {string.Join(", ", orphaned.Select(v => $"'{v}'"))}.");
                return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
            }

            var orphanedCheckpoints = checkpointsByKpi.Keys.Where(k => !claimedParents.Contains(k)).ToList();
            if (orphanedCheckpoints.Count > 0)
            {
                ModelState.AddModelError(
                    nameof(ImportKpiCheckpointRequest.KpiImportId),
                    $"The following checkpoints name a KPI that is not in the file: {string.Join(", ", orphanedCheckpoints.Select(v => $"'{v}'"))}.");
                return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
            }

            var result = await _dispatcher.Send(new ImportKpisCommand(rows, submissionGroupId, validateOnly), cancellationToken);

            return result.IsSuccess
                ? await responder.Respond(this, result.Value, cancellationToken)
                : BadRequest(result.ToBadRequestObject(HttpContext));
        }
        catch (CsvHelperException ex)
        {
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(ex.Message, HttpContext));
        }
    }
}
