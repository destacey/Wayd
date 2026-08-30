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
        "Get the delivery measures over a window.",
        "Deployment frequency and change failure rate. Lead time and time to restore are reported as unavailable.")]
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
