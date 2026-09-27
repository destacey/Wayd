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
    /// Any instant on the window's first day, read as a day in <paramref name="timeZone"/>. An instant
    /// rather than a date because the generated client types every date parameter as a JavaScript
    /// <c>Date</c> and sends <c>toISOString()</c>, which no <c>LocalDate</c> binder accepts. The caller
    /// sends the start of its local day, which lands on that day in its own zone.
    /// </param>
    /// <param name="to">Any instant on the window's last day, which is inclusive.</param>
    /// <param name="timeZone">
    /// The IANA zone whose days the window and the daily buckets are, normally the viewer's. Defaults
    /// to UTC.
    /// </param>
    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Get version activity over a window.",
        "Scoping to a product covers that node and everything beneath it, so selecting a grouping rolls up its children rather than reporting nothing. Cut-to-released excludes versions released without ever being cut, which carry no latency. Versions are released at moments, so the window and the daily buckets are days in timeZone (default UTC).")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DeliveryOverviewDto>> GetDeliveryOverview(
        [FromQuery] Instant from,
        [FromQuery] Instant to,
        [FromQuery] Guid? productId,
        [FromQuery] string? timeZone,
        CancellationToken cancellationToken)
    {
        var zone = string.IsNullOrWhiteSpace(timeZone)
            ? DateTimeZone.Utc
            : DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZone.Trim());

        if (zone is null)
        {
            ModelState.AddModelError(nameof(timeZone), $"'{timeZone}' is not a recognised IANA time zone.");
            return ValidationProblem(ModelState);
        }

        var overview = await _dispatcher.Send(
            new GetDeliveryOverviewQuery(from.InZone(zone).Date, to.InZone(zone).Date, zone, productId),
            cancellationToken);

        return Ok(overview);
    }

    [HttpGet("recent")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Get what has happened to versions and packages lately.",
        "Read from status transitions rather than the records' own moments, which say the state a record is in rather than when it changed. Scoping to a product covers its subtree and excludes packages, which span several products.")]
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
