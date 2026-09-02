using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Versions.Commands;
using Wayd.ProductManagement.Application.Versions.Dtos;
using Wayd.ProductManagement.Application.Versions.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.ProductManagement.Versions;

namespace Wayd.Web.Api.Controllers.ProductManagement;

/// <summary>
/// Versions of a product: what shipped, when, and what is still planned.
/// </summary>
/// <remarks>
/// Cutting, shipping and withdrawing are separate endpoints rather than fields on the update. Each is a
/// status transition the aggregate guards — a version cuts once, ships once, and is never deleted after
/// the fact — and each resolves its target status by <em>meaning</em> rather than by id, so an
/// organization can rename or reorder its workflow without breaking them.
/// </remarks>
[Route("api/product-management/versions")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(FeatureFlags.Names.ProductManagement)]
public class VersionsController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Delivery)]
    [OpenApiOperation("Get a list of versions.", "Ordered by released date then sequence — never by version, which is free text.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<VersionDto>>> GetVersions(
        [FromQuery] Guid? productId,
        [FromQuery] int[]? statusCategory,
        CancellationToken cancellationToken)
    {
        StatusCategory[]? categories = statusCategory is { Length: > 0 }
            ? [.. statusCategory.Select(c => (StatusCategory)c)]
            : null;

        var versions = await _dispatcher.Send(
            new GetVersionsQuery(productId, categories), cancellationToken);

        return Ok(versions);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Delivery)]
    [OpenApiOperation("Get version details.", "Accepts the version's id or its short key.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VersionDto>> GetVersion(string idOrKey, CancellationToken cancellationToken)
    {
        var version = await _dispatcher.Send(new GetVersionQuery(new IdOrKey(idOrKey)), cancellationToken);

        return version is not null
            ? Ok(version)
            : NotFound();
    }

    [HttpGet("{idOrKey}/status-history")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Get a version's status change history.",
        "Newest first. Each entry reports the status names as they were at the time, so a status renamed since does not rewrite the past.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<StatusTransitionDto>>> GetStatusHistory(
        string idOrKey, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new GetVersionStatusHistoryQuery(new IdOrKey(idOrKey)), cancellationToken);

        return result.IsFailure
            ? BadRequest(result.ToBadRequestObject(HttpContext))
            : result.Value is not null
                ? Ok(result.Value)
                : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Delivery)]
    [OpenApiOperation("Plan a version.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Plan(
        [FromBody] PlanVersionRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToPlanVersionCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetVersion), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Delivery)]
    [OpenApiOperation("Update a version.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(
        Guid id, [FromBody] UpdateVersionRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateVersionDetailsCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/target-date")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Delivery)]
    [OpenApiOperation("Move or clear a version's target date.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> MoveTargetDate(
        Guid id, [FromBody] MoveVersionTargetDateRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new MoveVersionTargetDateCommand(id, request.TargetDate), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/dates")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Correct a version's recorded target, cut and released dates.",
        "Fixes dates entered wrongly without changing the version's status. All three are sent, so an omitted date is cleared. The released date cannot be cleared — revert the version instead.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CorrectDates(
        Guid id, [FromBody] CorrectVersionDatesRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new CorrectVersionDatesCommand(id, request.TargetDate, request.CutDate, request.ReleasedDate),
            cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/cut")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Delivery)]
    [OpenApiOperation("Cut a version.", "Freezes scope and marks it ready to ship. One-way.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Cut(
        Guid id, [FromBody] CutVersionRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new CutVersionCommand(id, request.CutDate), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/version")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Delivery)]
    [OpenApiOperation("Record that a version shipped.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> MarkReleased(
        Guid id, [FromBody] MarkVersionReleasedRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new MarkVersionReleasedCommand(id, request.ReleasedDate), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/withdraw")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Delivery)]
    [OpenApiOperation("Withdraw a version.", "The version is kept: deployments may reference it.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Withdraw(
        Guid id, [FromBody] WithdrawVersionRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new WithdrawVersionCommand(id, request.Reason), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/revert")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Revert a version recorded as shipped.",
        "For a version marked released in error. Moves it back to Ready, or to the workflow's initial status where it was never cut, and clears the released date. Not a withdrawal — that pulls a version which really shipped.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Revert(
        Guid id, [FromBody] RevertVersionReleaseRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RevertVersionReleaseCommand(id, request.Reason), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
