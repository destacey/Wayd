using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.ReleasePackages.Commands;
using Wayd.ProductManagement.Application.ReleasePackages.Dtos;
using Wayd.ProductManagement.Application.ReleasePackages.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.ProductManagement.ReleasePackages;

namespace Wayd.Web.Api.Controllers.ProductManagement;

/// <summary>
/// Coordinated shipments: several component releases going out as one unit.
/// </summary>
/// <remarks>
/// A package's manifest records every component version it shipped, changed and carried forward alike,
/// so a reader can reconstruct exactly what was in the box. It is replaced wholesale rather than edited
/// entry by entry — a partially-updated manifest would claim a set of versions that never shipped
/// together.
/// </remarks>
[Route("api/product-management/release-packages")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(FeatureFlags.Names.ProductManagement)]
public class ReleasePackagesController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ReleasePackages)]
    [OpenApiOperation("Get a list of release packages.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ReleasePackageDto>>> GetReleasePackages(
        [FromQuery] int[]? statusCategory,
        [FromQuery] Guid? containingProductId,
        CancellationToken cancellationToken)
    {
        StatusCategory[]? categories = statusCategory is { Length: > 0 }
            ? [.. statusCategory.Select(c => (StatusCategory)c)]
            : null;

        var packages = await _dispatcher.Send(
            new GetReleasePackagesQuery(categories, containingProductId), cancellationToken);

        return Ok(packages);
    }

    [HttpGet("{id}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.ReleasePackages)]
    [OpenApiOperation("Get release package details.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReleasePackageDto>> GetReleasePackage(Guid id, CancellationToken cancellationToken)
    {
        var package = await _dispatcher.Send(new GetReleasePackageQuery(id), cancellationToken);

        return package is not null
            ? Ok(package)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.ReleasePackages)]
    [OpenApiOperation("Assemble a release package.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Assemble(
        [FromBody] AssembleReleasePackageRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToAssembleReleasePackageCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetReleasePackage), new { id = result.Value.Id }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/manifest")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ReleasePackages)]
    [OpenApiOperation("Replace a package's manifest.", "Whole-manifest replacement, never incremental.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> SetManifest(
        Guid id, [FromBody] SetReleasePackageManifestRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToSetReleasePackageManifestCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/release")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ReleasePackages)]
    [OpenApiOperation("Record that a package shipped.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> MarkReleased(
        Guid id, [FromBody] MarkReleasePackageReleasedRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new MarkReleasePackageReleasedCommand(id, request.ReleasedDate), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/withdraw")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.ReleasePackages)]
    [OpenApiOperation("Withdraw a package.", "The package is kept: deployments may reference it.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Withdraw(
        Guid id, [FromBody] WithdrawReleasePackageRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new WithdrawReleasePackageCommand(id, request.Reason), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
