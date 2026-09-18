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
public class DeploymentEnvironmentsController(IDispatcher dispatcher, ICsvService csvService) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICsvService _csvService = csvService;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.DeploymentEnvironments)]
    [OpenApiOperation("Get a list of deployment environments.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<DeploymentEnvironmentDto>>> GetDeploymentEnvironments(
        [FromQuery] bool? isActive,
        [FromQuery] int? category,
        CancellationToken cancellationToken)
    {
        var environments = await _dispatcher.Send(
            new GetDeploymentEnvironmentsQuery(isActive, (EnvironmentCategory?)category), cancellationToken);

        return Ok(environments);
    }

    [HttpGet("rollout")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Get what is running in each environment, in rollout order.",
        "Each entry is the latest deployment that succeeded and was not rolled back, so a failed attempt correctly leaves its predecessor running. Derived from the deployment record rather than stored, so it is never out of step with it. Keyed on the product: a package deployment is expanded into its manifest, and each component takes its own product's slot.")]
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
    [OpenApiOperation("Create a deployment environment.", "")]
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
        "Update a deployment environment.",
        "Changing the category changes what past deployments to it count toward.")]
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
    [OpenApiOperation("Activate or deactivate a deployment environment.", "")]
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
}
