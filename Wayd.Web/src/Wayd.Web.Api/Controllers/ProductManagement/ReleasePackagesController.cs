using CsvHelper;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Settings;
using Wayd.Web.Api.Models.ProductManagement;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Imports.Commands;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.ReleasePackages.Commands;
using Wayd.ProductManagement.Application.ReleasePackages.Dtos;
using Wayd.ProductManagement.Application.ReleasePackages.Imports;
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
public class ReleasePackagesController(IDispatcher dispatcher, ICsvService csvService, ISettings<SchedulingSettings> schedulingSettings, ILogger<ReleasePackagesController> logger) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICsvService _csvService = csvService;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
    private readonly ILogger<ReleasePackagesController> _logger = logger;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "List release packages — coordinated shipments such as `WAYD-2026.09.1`.",
        "Unreleased first, then the most recently released. Use `containingVersionId` to answer \"what did this version ship in?\": a version carries no pointer back to its package, so membership is read from the manifest side rather than duplicated.")]
    [McpTool("ReleasePackages_GetReleasePackages", "List release packages")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ReleasePackageDto>>> GetReleasePackages(
        [FromQuery] StatusCategory[]? statusCategory,
        [FromQuery] Guid? containingProductId,
        [FromQuery] Guid? containingVersionId,
        CancellationToken cancellationToken)
    {
        var packages = await _dispatcher.Send(
            new GetReleasePackagesQuery(statusCategory, containingProductId, containingVersionId),
            cancellationToken);

        return Ok(packages);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Get one package in full, including its complete manifest — every component version it shipped, and whether each changed or was carried forward.",
        "Accepts the package's UUID or its short key.")]
    [McpTool("ReleasePackages_GetReleasePackage", "Get release package")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReleasePackageDto>> GetReleasePackage(string idOrKey, CancellationToken cancellationToken)
    {
        var package = await _dispatcher.Send(new GetReleasePackageQuery(new IdOrKey(idOrKey)), cancellationToken);

        return package is not null
            ? Ok(package)
            : NotFound();
    }

    [HttpGet("{idOrKey}/activities")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Get a release package's activity history, newest first: its assembly, manifest amendments and status.",
        "Each entry has a `category` (Created, Updated, ScheduleChanged, StatusChanged, StateChanged, Health, Removed, Baseline), an `actorKind` (User, System, Import, Sync, Anonymous) with the acting `employee` when there is one, a `timestamp`, a one-line `summary`, and a `payload`: the event's fields as a JSON string. A change carries both ends, the value before and after. People in a payload are employee ids, not user ids. A Baseline entry marks where tracking began for a record that already existed, holding what it looked like then; nothing before it was recorded. An entry with `isRelated: true` was raised on another record and is listed here because it concerns this one; `raisedOn` names that record, or is null where it could not be resolved (typically removed since). Paged: the response carries `totalCount` and `hasNextPage`.")]
    [McpTool("ReleasePackages_GetActivities", "Get release package activity history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<ActivityLogDto>>> GetActivities(string idOrKey, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.Send(new GetReleasePackageActivitiesQuery(new IdOrKey(idOrKey), page, pageSize), cancellationToken);

        return result.Value is not null
            ? Ok(result.Value)
            : NotFound();
    }

    [HttpGet("{idOrKey}/status-history")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Get a package's status change history, newest first.",
        "Each entry reports the status names as they were at the time, so a status renamed since does not rewrite the past.")]
    [McpTool("ReleasePackages_GetStatusHistory", "Get package status history")]
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
        "Assemble a package and its manifest together.",
        "**A package ships at least one component**, so the manifest is authored here rather than added afterwards — an empty one is refused. The package is versioned in its own right, separately from anything inside it. A component may appear only once in a manifest, though the same component version may appear in several different packages.")]
    [McpTool("ReleasePackages_Assemble", "Assemble a release package", Destructive = false)]
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
        "Submit a csv file of release packages to import. Returns the run — 200 once it has finished, 202 while it is still queued or running.",
        "Takes two files: one row per package, and one row per manifest line naming the ImportId of the package it belongs to. Both are required — a package cannot be assembled without a manifest.")]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [CsvImport(ReleasePackageImportDefinition.ImportKey)]
    public async Task<ActionResult> Import(
        [FromForm, CsvRows(typeof(ImportReleasePackageRequest))] IFormFile file,
        [FromForm, CsvRows(typeof(ImportReleasePackageComponentRequest), Label = "Manifest")] IFormFile manifestFile,
        [FromQuery] Guid? submissionGroupId, [FromQuery] bool validateOnly,
        [FromServices] ImportSubmissionResponder responder,
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
                    return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                }

                packages.Add(package);
            }

            var importedComponents = _csvService.ReadCsv<ImportReleasePackageComponentRequest>(manifestFile.OpenReadStream());

            var componentsByPackage = new Dictionary<string, List<ImportReleasePackageComponentDto>>(StringComparer.OrdinalIgnoreCase);
            var componentValidator = new ImportReleasePackageComponentRequestValidator();
            foreach (var component in importedComponents)
            {
                var validationResults = await componentValidator.ValidateAsync(component, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        error.ErrorMessage =
                            $"{error.ErrorMessage} (Package: {component.PackageImportId}, Component: {component.ProductId})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                }

                var parent = component.PackageImportId.Trim();
                if (!componentsByPackage.TryGetValue(parent, out var forPackage))
                    componentsByPackage[parent] = forPackage = [];

                forPackage.Add(component.ToImportReleasePackageComponentDto());
            }

            // The same key the run reports row outcomes against, so a manifest line and its package row
            // agree on what the parent is called whether or not the file supplied an ImportId.
            List<SubmittedImportRow<ImportReleasePackageDto>> rows = [];
            HashSet<string> claimedParents = new(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < packages.Count; i++)
            {
                var package = packages[i];
                var key = SubmittedImportRow.KeyFor(package.ImportId, i + 1);

                claimedParents.Add(key);
                rows.Add(new SubmittedImportRow<ImportReleasePackageDto>(
                    package.ImportId,
                    package.ToImportReleasePackageDto(
                        componentsByPackage.TryGetValue(key, out var forPackage) ? forPackage : [])));
            }

            // Every manifest line must find its package. A line naming one that is not in the package file
            // is a mistyped reference rather than a line to drop, so it fails the submission.
            var orphaned = componentsByPackage.Keys.Where(k => !claimedParents.Contains(k)).ToList();
            if (orphaned.Count > 0)
            {
                ModelState.AddModelError(
                    nameof(ImportReleasePackageComponentRequest.PackageImportId),
                    $"The following manifest lines name a package that is not in the file: {string.Join(", ", orphaned.Select(v => $"'{v}'"))}.");
                return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
            }

            var result = await _dispatcher.Send(new ImportReleasePackagesCommand(rows, submissionGroupId, validateOnly), cancellationToken);

            return result.IsSuccess
                ? await responder.Respond(this, result.Value, cancellationToken)
                : BadRequest(result.ToBadRequestObject(HttpContext));
        }
        catch (CsvHelperException ex)
        {
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(ex.Message, HttpContext));
        }
    }

    [HttpPut("{id}/manifest")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Replace a package's manifest as a whole.",
        "**This is a whole-set replacement: a line left out is removed from the package.** Components carry no identifier of their own and cannot be addressed individually, so read the package first and send back every line it should end up with. The manifest closes once the package is released or withdrawn — what was in the box cannot be rewritten after the box shipped.")]
    [McpTool("ReleasePackages_SetManifest", "Replace a package manifest", Idempotent = false)]
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
        "Record that a package shipped, and close its manifest.",
        "**A package with an empty manifest cannot be released.** This is not announcing anything to customers — that is Releases_MarkReleased on a release that carries this package.")]
    [McpTool("ReleasePackages_MarkReleased", "Mark a package released")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> MarkReleased(
        Guid id, [FromBody] MarkReleasePackageReleasedRequest request, CancellationToken cancellationToken)
    {
        var zone = await LegacyDeliveryDates.ZoneForRequest(
            request.UsesLegacyDates(), _schedulingSettings, _logger, "Mark package released", cancellationToken);

        var result = await _dispatcher.Send(
            new MarkReleasePackageReleasedCommand(id, request.ResolveReleasedAt(zone)), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/dates")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Fix a package's target date or released moment that was recorded wrongly.",
        "The status does not move and the status history is left untouched — that is the point of having this separate from ReleasePackages_MarkReleased, which asserts the package shipped and refuses to run twice. **Both values are sent, so an omitted target date is cleared.** The released moment can be changed on a released package but **cannot be cleared**, and **cannot be added to a package that has not been released** — use ReleasePackages_MarkReleased for that. The target date is a calendar date; the released moment is an instant, so send the CI/CD timestamp with its offset as-is — no conversion to a local date. Refused on a withdrawn package.")]
    [McpTool("ReleasePackages_CorrectDates", "Correct package dates", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CorrectDates(
        Guid id, [FromBody] CorrectReleasePackageDatesRequest request, CancellationToken cancellationToken)
    {
        var zone = await LegacyDeliveryDates.ZoneForRequest(
            request.UsesLegacyDates(), _schedulingSettings, _logger, "Correct package dates", cancellationToken);

        var result = await _dispatcher.Send(
            new CorrectReleasePackageDatesCommand(id, request.TargetDate, request.ResolveReleasedAt(zone)),
            cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Permanently delete a package with its manifest, its status history and **every deployment of it**.",
        "**Refused while any release lists it** — remove it with `Releases_SetContents` first; a released or withdrawn release's contents cannot change, so that release has to be deleted instead with `Releases_Delete`. The versions it names are separate records and are kept. The delivery measures and rollout stop counting those deployments. For a package assembled by mistake or when the user asks to purge history; otherwise withdraw it. Needs the delivery Delete permission.")]
    [McpTool("ReleasePackages_Delete", "Delete a package", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteReleasePackageCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/withdraw")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Pull a package.",
        "Terminal, and it closes the manifest. A released package can still be withdrawn; a withdrawn one cannot be released. Withdrawing keeps the package and every deployment of it — prefer it to `ReleasePackages_Delete`.")]
    [McpTool("ReleasePackages_Withdraw", "Withdraw a package")]
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
