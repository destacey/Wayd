using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Commands;
using Wayd.ProductManagement.Application.DeploymentEnvironments.Dtos;
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
public class DeploymentEnvironmentsController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

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
