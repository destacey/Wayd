using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.ProductManagement.Application.DeliveryMetrics.Dtos;
using Wayd.ProductManagement.Application.DeliveryMetrics.Queries;

namespace Wayd.Web.Api.Controllers.ProductManagement;

/// <summary>
/// Delivery measures computed from the deployment record.
/// </summary>
/// <remarks>
/// Two of the four DORA measures are computable from what this module records. The other two are
/// returned as unavailable, each with the reason, rather than omitted or approximated — a reader can
/// then tell "we do not measure this yet" from "nothing deployed".
/// <para>
/// No separate feature flag: the module's own gate already covers this, and a flag on the two
/// unavailable measures would claim they are built and switched off, which they are not.
/// </para>
/// </remarks>
[Route("api/product-management/delivery-metrics")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(FeatureFlags.Names.ProductManagement)]
public class DeliveryMetricsController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.DeliveryMetrics)]
    [OpenApiOperation(
        "Get the delivery measures over a window, computed from deployment records.",
        "Returns **deployment frequency** and **change failure rate**, plus an `unavailable` list naming the measures this module cannot compute yet and why — read that list rather than treating a missing measure as zero.\n\nTwo caveats worth carrying into any answer. **Production-scoped measures depend on environment categories**, not names, so a deployment into an environment whose category is not Production does not count toward deployment frequency. And **change failure rate is a proxy**: a pipeline run that failed before reaching production is a failure that was *prevented*, while a real change failure is a deployment that succeeded and then broke something — which the pipeline has no way to know. Report it as approximate rather than as the metric.")]
    [McpTool("DeliveryMetrics_GetDeliveryMetrics", "Get delivery metrics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DeliveryMetricsDto>> GetDeliveryMetrics(
        [FromQuery] Instant from,
        [FromQuery] Instant to,
        [FromQuery] Guid? productId,
        CancellationToken cancellationToken)
    {
        var metrics = await _dispatcher.Send(new GetDeliveryMetricsQuery(from, to, productId), cancellationToken);

        return Ok(metrics);
    }
}
