using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.ProductManagement.Application.DeliveryOverview.Dtos;
using Wayd.ProductManagement.Application.DeliveryOverview.Queries;

namespace Wayd.Web.Api.Controllers.ProductManagement;

/// <summary>
/// Version activity over a window — what was cut and shipped, rather than what reached an environment.
/// </summary>
/// <remarks>
/// Separate from delivery metrics, which measure deployments. The two answer different questions and
/// a product that ships continuously has a healthy cadence here whether or not its pipeline is
/// recorded in Wayd at all.
/// </remarks>
[Route("api/product-management/delivery-overview")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(FeatureFlags.Names.ProductManagement)]
public class DeliveryOverviewController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    /// <param name="from">
    /// An instant rather than a date, though the window is a date range and the versions it counts
    /// carry dates. The generated client types every date parameter as a JavaScript <c>Date</c> and
    /// sends <c>toISOString()</c>, which no <c>LocalDate</c> binder accepts — so a date-typed
    /// parameter here is unreachable from the client that calls it. Truncated to its UTC date below,
    /// matching how the other windowed endpoints take their bounds.
    /// </param>
    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Get version activity over a window.",
        "Scoping to a product covers that node and everything beneath it, so selecting a grouping rolls up its children rather than reporting nothing. Cut-to-released excludes versions released without ever being cut, which carry no latency.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DeliveryOverviewDto>> GetDeliveryOverview(
        [FromQuery] Instant from,
        [FromQuery] Instant to,
        [FromQuery] Guid? productId,
        CancellationToken cancellationToken)
    {
        var overview = await _dispatcher.Send(
            new GetDeliveryOverviewQuery(from.InUtc().Date, to.InUtc().Date, productId),
            cancellationToken);

        return Ok(overview);
    }

    [HttpGet("recent")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Get what has happened to versions and packages lately.",
        "Read from status transitions rather than the records' own dates, which carry no time of day and say the state a record is in rather than the moment it changed. Scoping to a product covers its subtree and excludes packages, which span several products.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RecentDeliveryEventDto>>> GetRecentDeliveryEvents(
        [FromQuery] int? take,
        [FromQuery] Guid? productId,
        CancellationToken cancellationToken)
    {
        var events = await _dispatcher.Send(
            new GetRecentDeliveryEventsQuery(take ?? 10, productId), cancellationToken);

        return Ok(events);
    }
}
