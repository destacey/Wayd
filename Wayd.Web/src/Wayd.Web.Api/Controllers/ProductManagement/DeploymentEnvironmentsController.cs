using CsvHelper;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Commands;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Dtos;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Imports;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.ProductManagement.DeploymentEnvironments;

namespace Wayd.Web.Api.Controllers.ProductManagement;

/// <summary>
/// The environments deployments target, in rollout order.
/// </summary>
/// <remarks>
/// Each environment's category — not its name — is what delivery measures scoped to production count
/// on, because names are free text and endlessly varied.
/// </remarks>
[Route("api/product-management/deployment-environments")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(FeatureFlags.Names.ProductManagement)]
[McpTools(McpToolset.Delivery)]
public class DeploymentEnvironmentsController(IDispatcher dispatcher, ICsvService csvService) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICsvService _csvService = csvService;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.DeploymentEnvironments)]
    [OpenApiOperation(
        "List the deployment environments defined for the organization.",
        "Environments are defined once and any product can deploy into any of them. Each carries a **category** and a **ring order**, so progressive rollout is representable. Filter by category rather than matching on names, which are free text.")]
    [McpTool("DeploymentEnvironments_GetDeploymentEnvironments", "List deployment environments")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<DeploymentEnvironmentDto>>> GetDeploymentEnvironments(
        [FromQuery] bool? isActive,
        [FromQuery] EnvironmentCategory? category,
        CancellationToken cancellationToken)
    {
        var environments = await _dispatcher.Send(
            new GetDeploymentEnvironmentsQuery(isActive, category), cancellationToken);

        return Ok(environments);
    }

    [HttpGet("rollout")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Get what is running in each environment right now, in rollout order (lowest ring first).",
        "Answers \"what version of X is in production?\" and \"how far has this got?\" in one call.\n\nEach environment lists `running`: **one entry per product**, never per package. A package deployment is expanded into its manifest and each component takes its own product's slot, so two successive bundles carrying the same component do not both show as running. Each entry is the **latest deployment that succeeded and was not rolled back** — a failed attempt leaves its predecessor running, and a rollback takes its own deployment out and leaves the one before it in. So \"what is here\" and \"what happened last\" differ: check `hasFailedAttemptSince`, true when a later deployment touching that product failed or was rolled back there.\n\nEach entry carries `versionLabel` (always set, free text), `version` (null for a packaged component never cut as a version in Wayd), `package` (set when it arrived inside one), `artifactId`, `deployedAt`, and the `deploymentId`/`deploymentKey` it was read from. An empty `running` list means nothing has ever succeeded into that environment — a complete answer, not missing data.")]
    [McpTool("DeploymentEnvironments_GetRollout", "Get environment rollout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<EnvironmentRolloutDto>>> GetRollout(
        [FromQuery] bool? includeInactive,
        CancellationToken cancellationToken)
    {
        var rollout = await _dispatcher.Send(
            new GetEnvironmentRolloutQuery(includeInactive ?? false), cancellationToken);

        return Ok(rollout);
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.DeploymentEnvironments)]
    [OpenApiOperation(
        "Define a deployment environment.",
        "The **category** is what every production-scoped measure counts on, so set it deliberately rather than relying on the name. **Ring order** places the environment in a progressive rollout sequence — lower rings are reached first.")]
    [McpTool("DeploymentEnvironments_Create", "Create a deployment environment", Destructive = false)]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create(
        [FromBody] CreateDeploymentEnvironmentRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateDeploymentEnvironmentCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetDeploymentEnvironments), null, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("import")]
    [MustHavePermission(ApplicationAction.Import, ApplicationResource.DeploymentEnvironments)]
    [OpenApiOperation(
        "Submit a csv file of deployment environments to import. Returns the run — 200 once it has finished, 202 while it is still queued or running.",
        "Each row is created active unless IsActive is false, in which case it is created and then retired — for the environments a historical backfill's deployments still point at.")]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [CsvImport(DeploymentEnvironmentImportDefinition.ImportKey)]
    public async Task<ActionResult> Import([FromForm, CsvRows(typeof(ImportDeploymentEnvironmentRequest))] IFormFile file, [FromQuery] Guid? submissionGroupId, [FromQuery] bool validateOnly, [FromServices] ImportSubmissionResponder responder, CancellationToken cancellationToken)
    {
        try
        {
            var importedEnvironments = _csvService.ReadCsv<ImportDeploymentEnvironmentRequest>(file.OpenReadStream());

            List<SubmittedImportRow<ImportDeploymentEnvironmentDto>> rows = [];
            var validator = new ImportDeploymentEnvironmentRequestValidator();
            foreach (var environment in importedEnvironments)
            {
                var validationResults = await validator.ValidateAsync(environment, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        error.ErrorMessage = $"{error.ErrorMessage} (Environment: {environment.Name})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                }

                rows.Add(new SubmittedImportRow<ImportDeploymentEnvironmentDto>(
                    environment.ImportId, environment.ToImportDeploymentEnvironmentDto()));
            }

            var result = await _dispatcher.Send(new ImportDeploymentEnvironmentsCommand(rows, submissionGroupId, validateOnly), cancellationToken);

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
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.DeploymentEnvironments)]
    [OpenApiOperation(
        "Update an environment's name, category or ring order.",
        "**This is a whole-record overwrite — send every field, including ones you are not changing.**\n\nChanging the category is not an ordinary edit: each deployment **froze** the category of the environment it went into, so reclassifying changes where *future* deployments count and leaves past ones exactly as they were. A staging environment promoted to production does not retroactively inflate deployment frequency. Refused on a retired environment.")]
    [McpTool("DeploymentEnvironments_Update", "Update a deployment environment", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(
        Guid id, [FromBody] UpdateDeploymentEnvironmentRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateDeploymentEnvironmentCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/active")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.DeploymentEnvironments)]
    [OpenApiOperation(
        "Retire an environment or reinstate one.",
        "**Retire rather than delete** unless the user explicitly wants the history gone: deleting an environment takes every deployment into it with it, while retiring keeps them.\n\nA retired environment is no longer offered as a deployment target, but it and every deployment recorded against it are kept, and those deployments keep counting toward the measures they already count toward. Editing and reclassifying are refused on a retired environment, so reinstate it first if you need to change it.")]
    [McpTool("DeploymentEnvironments_SetActive", "Retire or reinstate an environment", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SetActive(
        Guid id, [FromBody] SetDeploymentEnvironmentActiveRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToSetDeploymentEnvironmentActiveCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.DeploymentEnvironments)]
    [OpenApiOperation(
        "Permanently delete an environment **and every deployment into it**, with their status history.",
        "The delivery measures and rollout stop counting those deployments. For an environment defined by mistake, or when the user asks to purge history — otherwise retire it with `DeploymentEnvironments_SetActive`, which keeps them. The `deploymentCount` from `DeploymentEnvironments_GetDeploymentEnvironments` says how many would go; state it before confirming. Needs the environment Delete permission, and — when the environment has any deployments — the delivery Delete permission as well.")]
    [McpTool("DeploymentEnvironments_Delete", "Delete a deployment environment", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteDeploymentEnvironmentCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
