using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Releases.Commands;
using Wayd.ProductManagement.Application.Releases.Dtos;
using Wayd.ProductManagement.Application.Releases.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.ProductManagement.Releases;

namespace Wayd.Web.Api.Controllers.ProductManagement;

/// <summary>
/// Releases of a product: what shipped, when, and what is still planned.
/// </summary>
/// <remarks>
/// Cutting, shipping and withdrawing are separate endpoints rather than fields on the update. Each is a
/// status transition the aggregate guards — a release cuts once, ships once, and is never deleted after
/// the fact — and each resolves its target status by <em>meaning</em> rather than by id, so an
/// organization can rename or reorder its workflow without breaking them.
/// </remarks>
[Route("api/product-management/releases")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(FeatureFlags.Names.ProductManagement)]
public class ReleasesController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Releases)]
    [OpenApiOperation("Get a list of releases.", "Ordered by released date then sequence — never by version, which is free text.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ReleaseDto>>> GetReleases(
        [FromQuery] Guid? productId,
        [FromQuery] Guid? packageId,
        [FromQuery] int[]? statusCategory,
        CancellationToken cancellationToken)
    {
        StatusCategory[]? categories = statusCategory is { Length: > 0 }
            ? [.. statusCategory.Select(c => (StatusCategory)c)]
            : null;

        var releases = await _dispatcher.Send(
            new GetReleasesQuery(productId, packageId, categories), cancellationToken);

        return Ok(releases);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Releases)]
    [OpenApiOperation("Get release details.", "Accepts the release's id or its short key.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReleaseDto>> GetRelease(string idOrKey, CancellationToken cancellationToken)
    {
        var release = await _dispatcher.Send(new GetReleaseQuery(new IdOrKey(idOrKey)), cancellationToken);

        return release is not null
            ? Ok(release)
            : NotFound();
    }

    [HttpGet("{idOrKey}/status-history")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Releases)]
    [OpenApiOperation(
        "Get a release's status change history.",
        "Newest first. Each entry reports the status names as they were at the time, so a status renamed since does not rewrite the past.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<StatusTransitionDto>>> GetStatusHistory(
        string idOrKey, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new GetReleaseStatusHistoryQuery(new IdOrKey(idOrKey)), cancellationToken);

        return result.IsFailure
            ? BadRequest(result.ToBadRequestObject(HttpContext))
            : result.Value is not null
                ? Ok(result.Value)
                : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Releases)]
    [OpenApiOperation("Plan a release.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Plan(
        [FromBody] PlanReleaseRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToPlanReleaseCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetRelease), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Releases)]
    [OpenApiOperation("Update a release.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(
        Guid id, [FromBody] UpdateReleaseRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateReleaseDetailsCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/target-date")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Releases)]
    [OpenApiOperation("Move or clear a release's target date.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> MoveTargetDate(
        Guid id, [FromBody] MoveReleaseTargetDateRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new MoveReleaseTargetDateCommand(id, request.TargetDate), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/dates")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Releases)]
    [OpenApiOperation(
        "Correct a release's recorded cut and released dates.",
        "Fixes dates entered wrongly. Does not change the release's status, and cannot add or remove a date the release does not already have.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CorrectDates(
        Guid id, [FromBody] CorrectReleaseDatesRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new CorrectReleaseDatesCommand(id, request.CutDate, request.ReleasedDate), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/cut")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Releases)]
    [OpenApiOperation("Cut a release.", "Freezes scope and marks it ready to ship. One-way.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Cut(
        Guid id, [FromBody] CutReleaseRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new CutReleaseCommand(id, request.CutDate), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/release")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Releases)]
    [OpenApiOperation("Record that a release shipped.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> MarkReleased(
        Guid id, [FromBody] MarkReleaseReleasedRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new MarkReleaseReleasedCommand(id, request.ReleasedDate), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/withdraw")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Releases)]
    [OpenApiOperation("Withdraw a release.", "The release is kept: deployments may reference it.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Withdraw(
        Guid id, [FromBody] WithdrawReleaseRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new WithdrawReleaseCommand(id, request.Reason), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
