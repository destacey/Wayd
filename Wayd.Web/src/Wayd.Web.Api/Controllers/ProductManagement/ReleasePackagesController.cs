using CsvHelper;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows.Dtos;
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
public class ReleasePackagesController(IDispatcher dispatcher, ICsvService csvService) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICsvService _csvService = csvService;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Get a list of release packages.",
        "containingProductId matches any manifest line for that product; containingVersionId matches only the packages naming that exact release, which is what a release's own page needs.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ReleasePackageDto>>> GetReleasePackages(
        [FromQuery] int[]? statusCategory,
        [FromQuery] Guid? containingProductId,
        [FromQuery] Guid? containingVersionId,
        CancellationToken cancellationToken)
    {
        StatusCategory[]? categories = statusCategory is { Length: > 0 }
            ? [.. statusCategory.Select(c => (StatusCategory)c)]
            : null;

        var packages = await _dispatcher.Send(
            new GetReleasePackagesQuery(categories, containingProductId, containingVersionId),
            cancellationToken);

        return Ok(packages);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Delivery)]
    [OpenApiOperation("Get release package details.", "Accepts the package's id or its short key.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReleasePackageDto>> GetReleasePackage(string idOrKey, CancellationToken cancellationToken)
    {
        var package = await _dispatcher.Send(new GetReleasePackageQuery(new IdOrKey(idOrKey)), cancellationToken);

        return package is not null
            ? Ok(package)
            : NotFound();
    }

    [HttpGet("{idOrKey}/status-history")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Get a release package's status change history.",
        "Newest first. Each entry reports the status names as they were at the time, so a status renamed since does not rewrite the past.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<StatusTransitionDto>>> GetStatusHistory(
        string idOrKey, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new GetReleasePackageStatusHistoryQuery(new IdOrKey(idOrKey)), cancellationToken);

        return result.IsFailure
            ? BadRequest(result.ToBadRequestObject(HttpContext))
            : result.Value is not null
                ? Ok(result.Value)
                : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Assemble a release package.",
        "A package is what moved through environments together, and it ships at least one component, so the manifest is authored here rather than added afterwards. A component may appear only once. Each line may name the version record it came from, which is what lets a release know that version is already inside a package; a carried-forward line naming a version never cut here holds its version as text instead.")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Assemble(
        [FromBody] AssembleReleasePackageRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToAssembleReleasePackageCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetReleasePackage), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("import")]
    [MustHavePermission(ApplicationAction.Import, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Import release packages from a csv file.",
        "Takes two files: one row per package, and one row per manifest line pointing back at its package by version. Both are required — a package cannot be assembled without a manifest.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Import(
        [FromForm] IFormFile file,
        [FromForm] IFormFile manifestFile,
        CancellationToken cancellationToken)
    {
        try
        {
            var importedPackages = _csvService.ReadCsv<ImportReleasePackageRequest>(file.OpenReadStream());

            List<ImportReleasePackageRequest> packages = [];
            var validator = new ImportReleasePackageRequestValidator();
            foreach (var package in importedPackages)
            {
                var validationResults = await validator.ValidateAsync(package, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        error.ErrorMessage = $"{error.ErrorMessage} (Package: {package.Version})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(validationResults);
                }

                packages.Add(package);
            }

            if (packages.Count == 0)
                return BadRequest(ProblemDetailsExtensions.ForBadRequest("No release packages imported.", HttpContext));

            var importedComponents = _csvService.ReadCsv<ImportReleasePackageComponentRequest>(manifestFile.OpenReadStream());

            List<ImportReleasePackageComponentDto> components = [];
            var componentValidator = new ImportReleasePackageComponentRequestValidator();
            foreach (var component in importedComponents)
            {
                var validationResults = await componentValidator.ValidateAsync(component, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        error.ErrorMessage =
                            $"{error.ErrorMessage} (Package: {component.PackageVersion}, Component: {component.ProductName})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(validationResults);
                }

                components.Add(component.ToImportReleasePackageComponentDto());
            }

            // Every manifest line must find its package. A line naming one that is not in the package
            // file is a mistyped version rather than a line to drop, so it fails the batch.
            var packageVersions = packages
                .Select(p => p.Version.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var orphaned = components
                .Select(c => c.PackageVersion.Trim())
                .Where(v => !packageVersions.Contains(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (orphaned.Count > 0)
            {
                return BadRequest(ProblemDetailsExtensions.ForBadRequest(
                    $"The following manifest lines name a package that is not in the import: {string.Join(", ", orphaned.Select(v => $"'{v}'"))}.",
                    HttpContext));
            }

            var componentsByPackage = components
                .GroupBy(c => c.PackageVersion.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<ImportReleasePackageComponentDto>)[.. g], StringComparer.OrdinalIgnoreCase);

            var dtos = packages
                .Select(p => p.ToImportReleasePackageDto(
                    componentsByPackage.GetValueOrDefault(p.Version.Trim(), [])))
                .ToList();

            var result = await _dispatcher.Send(new ImportReleasePackagesCommand(dtos), cancellationToken);

            return result.IsSuccess
                ? NoContent()
                : BadRequest(result.ToBadRequestObject(HttpContext));
        }
        catch (CsvHelperException ex)
        {
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(ex.Message, HttpContext));
        }
    }

    [HttpPut("{id}/manifest")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Delivery)]
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
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Record that a package shipped.",
        "Closes the manifest: what was in the box cannot be rewritten after the box shipped. A package with an empty manifest cannot be released.")]
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
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Delivery)]
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
