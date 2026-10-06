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
[McpTools(McpToolset.Delivery)]
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
        "Get version activity over a window — what was **cut and shipped**, measured from versions rather than deployments.",
        "Separate from `DeliveryMetrics_GetDeliveryMetrics`, which measures deployments: a product that ships continuously has a healthy cadence here whether or not its pipeline is recorded in Wayd.\n\nScoping to a product covers **that node and everything beneath it**, so a product line rolls up its children. Omit `productId` for the whole catalog.\n\nReturns:\n- `scope` — the selected product (null for the whole catalog) and `releasableNodeCount`, the denominator for judging the rest: three releases a week means something different across four products than across forty.\n- `frequency` — `count`, `windowDays`, `perWeek`, and `previousPerWeek` for the equal window just before. **A null previous value means nothing shipped then** — the change is unknowable, so do not report it as a rise from zero.\n- `cutToReleased` — `averageDays`, the elapsed time from cut to release in (fractional) days, with `measuredCount` of `releasedCount` and `previousAverageDays`. Versions released without ever being cut (imports and backfills do this) carry no latency and are excluded rather than counted as zero, so say how much of the window the average speaks for.\n- `activity` — the subtree depth-first, one row per node with `depth` and `isReleasable`. Groupings appear so the hierarchy reads but never release anything themselves. Every releasable node in scope is listed even if it shipped nothing — that is part of the answer. `days` lists only days with a release: `released`, and `withdrawn` (how many of that day's versions have since been withdrawn — a count, not a rate).")]
    [McpTool("DeliveryOverview_GetDeliveryOverview", "Get delivery overview")]
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
        "Get what has happened to versions and packages lately, most recent first — a feed answering \"what shipped recently?\".",
        "**One entry per record**, at its latest status change, not one per transition; the record's own status history has the rest.\n\nEach entry has `kind` (Version or ReleasePackage), `recordId`/`recordKey`, `label` (the version number or the package's own version, free text), `product` (null for a package, which spans several), `statusName` as the organization named it, `alias` (its well-known meaning — 10 Ready, 11 Released, 12 Withdrawn — which is what to reason on, since names are per-organization), `changedOn` (an instant), `releasedAt` (an instant) where the record has shipped (so a withdrawal says what it pulled), and `componentCount` for a package.\n\nRead from status transitions rather than the records' own dates, which carry no time of day. **Scoping to a product covers its subtree and leaves packages out entirely.**")]
    [McpTool("DeliveryOverview_GetRecentDeliveryEvents", "Get recent delivery events")]
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
