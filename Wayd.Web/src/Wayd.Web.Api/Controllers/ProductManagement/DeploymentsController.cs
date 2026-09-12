using CsvHelper;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.ProductManagement.Application.Deployments.Commands;
using Wayd.ProductManagement.Application.Deployments.Dtos;
using Wayd.ProductManagement.Application.Deployments.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.ProductManagement.Deployments;

namespace Wayd.Web.Api.Controllers.ProductManagement;

/// <summary>
/// One release or package reaching one environment — the record the delivery measures read.
/// </summary>
/// <remarks>
/// A deployment is for a release <em>or</em> a package, never both: where a package exists it is the
/// unit that shipped, so one pipeline run counts once.
/// <para>
/// Each deployment freezes its environment's category as it stood at the time, so reclassifying an
/// environment later cannot retroactively rewrite what past deployments counted as.
/// </para>
/// </remarks>
[Route("api/product-management/deployments")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(FeatureFlags.Names.ProductManagement)]
public class DeploymentsController(IDispatcher dispatcher, ICsvService csvService) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICsvService _csvService = csvService;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Delivery)]
    [OpenApiOperation("Get a list of deployments.", "Most recently started first.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<DeploymentDto>>> GetDeployments(
        [FromQuery] Guid? versionId,
        [FromQuery] Guid? packageId,
        [FromQuery] Guid? environmentId,
        [FromQuery] int? environmentCategory,
        [FromQuery] Instant? startedOnOrAfter,
        CancellationToken cancellationToken)
    {
        var deployments = await _dispatcher.Send(
            new GetDeploymentsQuery(
                versionId,
                packageId,
                environmentId,
                (EnvironmentCategory?)environmentCategory,
                startedOnOrAfter),
            cancellationToken);

        return Ok(deployments);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Delivery)]
    [OpenApiOperation("Get deployment details.", "Accepts the deployment's id or its short key.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeploymentDto>> GetDeployment(string idOrKey, CancellationToken cancellationToken)
    {
        var deployment = await _dispatcher.Send(new GetDeploymentQuery(new IdOrKey(idOrKey)), cancellationToken);

        return deployment is not null
            ? Ok(deployment)
            : NotFound();
    }

    [HttpGet("{idOrKey}/activities")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Delivery)]
    [OpenApiOperation("Get activity history for the deployment.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<ActivityLogDto>>> GetActivities(string idOrKey, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.Send(new GetDeploymentActivitiesQuery(new IdOrKey(idOrKey), page, pageSize), cancellationToken);

        return result.Value is not null
            ? Ok(result.Value)
            : NotFound();
    }

    [HttpGet("{idOrKey}/status-history")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Get a deployment's status change history.",
        "Newest first. Each entry reports the status names as they were at the time, so a status renamed since does not rewrite the past.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<StatusTransitionDto>>> GetStatusHistory(
        string idOrKey, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new GetDeploymentStatusHistoryQuery(new IdOrKey(idOrKey)), cancellationToken);

        return result.IsFailure
            ? BadRequest(result.ToBadRequestObject(HttpContext))
            : result.Value is not null
                ? Ok(result.Value)
                : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Start a deployment.",
        "Carries either a version or a package, never both and never neither. Where a package exists it is the unit that shipped, so one pipeline run counts once rather than once per component. Only an active environment is accepted, and leaving the start time empty records it as starting now.")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Start(
        [FromBody] StartDeploymentRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToStartDeploymentCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetDeployment), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("import")]
    [MustHavePermission(ApplicationAction.Import, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Submit a csv file of deployments to import. Returns the run — 200 once it has finished, 202 while it is still queued or running.",
        "Each row names its version or package by id and its environment by name, and is walked to the outcome it describes with the timestamps it carries: no outcome leaves it in flight, Succeeded and Failed complete it, and RolledBack records a success and then the rollback. A build number is never resolved to a version.")]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Import([FromForm] IFormFile file, [FromQuery] Guid? submissionGroupId, [FromServices] ImportSubmissionResponder responder, CancellationToken cancellationToken)
    {
        try
        {
            var importedDeployments = _csvService.ReadCsv<ImportDeploymentRequest>(file.OpenReadStream()).ToList();

            List<SubmittedImportRow<ImportDeploymentDto>> rows = [];
            var validator = new ImportDeploymentRequestValidator();
            for (var i = 0; i < importedDeployments.Count; i++)
            {
                var deployment = importedDeployments[i];

                var validationResults = await validator.ValidateAsync(deployment, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        // The row's own key rather than what it deployed: a deployment has no natural
                        // key, so nothing else on the row says which line failed. This is the same key
                        // the run reports outcomes against.
                        error.ErrorMessage =
                            $"{error.ErrorMessage} (Row: {SubmittedImportRow.KeyFor(deployment.ImportId, i + 1)})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                }

                rows.Add(new SubmittedImportRow<ImportDeploymentDto>(deployment.ImportId, deployment.ToImportDeploymentDto()));
            }

            var result = await _dispatcher.Send(new ImportDeploymentsCommand(rows, submissionGroupId), cancellationToken);

            return result.IsSuccess
                ? await responder.Respond(this, result.Value, cancellationToken)
                : BadRequest(result.ToBadRequestObject(HttpContext));
        }
        catch (CsvHelperException ex)
        {
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(ex.Message, HttpContext));
        }
    }

    [HttpPost("{id}/succeed")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Delivery)]
    [OpenApiOperation("Record that a deployment reached its environment.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Succeed(
        Guid id, [FromBody] SucceedDeploymentRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new SucceedDeploymentCommand(id, request.CompletedAt), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/fail")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Record that a deployment did not reach its environment.",
        "Counts toward change failure rate only in production.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Fail(
        Guid id, [FromBody] FailDeploymentRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new FailDeploymentCommand(id, request.Reason, request.CompletedAt), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/roll-back")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Record that a deployment was reverted.",
        "Permitted only from a succeeded deployment.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RollBack(
        Guid id, [FromBody] RollBackDeploymentRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new RollBackDeploymentCommand(id, request.Reason, request.RolledBackAt), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
