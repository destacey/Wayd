using CsvHelper;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Imports.Commands;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Releases.Commands;
using Wayd.ProductManagement.Application.Releases.Dtos;
using Wayd.ProductManagement.Application.Releases.Imports;
using Wayd.ProductManagement.Application.Releases.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.ProductManagement.Releases;

namespace Wayd.Web.Api.Controllers.ProductManagement;

/// <summary>
/// Releases: what was announced to customers, and the versions and packages that carried it.
/// </summary>
/// <remarks>
/// Distinct from a version, which is what was built. A release answers "what did we tell customers?";
/// a version answers "what version of this one artifact?".
/// <para>
/// A release is never cut — cutting freezes an artifact's scope and belongs to a version. Its contents
/// are set through their own endpoints rather than as fields on the update, because they carry a rule
/// the aggregate enforces: a version shipping inside one of the release's packages may not also be
/// carried directly, so that one shipment is announced once.
/// </para>
/// </remarks>
[Route("api/product-management/releases")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(FeatureFlags.Names.ProductManagement)]
[McpTools(McpToolset.Delivery)]
public class ReleasesController(IDispatcher dispatcher, ICsvService csvService) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICsvService _csvService = csvService;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Releases)]
    [OpenApiOperation(
        "List product releases — the announcements made to customers, such as `Wayd 2026.09`.",
        "A release is what was announced to customers, not what was built — for one artifact and its version number, use the Versions_* tools instead. Unannounced releases come first, then the most recently announced. Never ordered by the version label, which is free text and never parsed. Filtering by product deliberately excludes releases that name no product: one spanning product lines belongs to no single product, so listing it under one would misstate what that product announced.")]
    [McpTool("Releases_GetReleases", "List releases")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ReleaseDto>>> GetReleases(
        [FromQuery] Guid? productId,
        [FromQuery] StatusCategory[]? statusCategory,
        [FromQuery] Guid? containingVersionId,
        CancellationToken cancellationToken)
    {
        var releases = await _dispatcher.Send(
            new GetReleasesQuery(productId, statusCategory, containingVersionId), cancellationToken);

        return Ok(releases);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Releases)]
    [OpenApiOperation(
        "Get one release in full, including everything it announces: the packages it shipped and the versions it carries directly.",
        "Each contents entry reports its own shipped date, which is what tells you whether the release can be announced yet. A release is what was announced to customers, not what was built — for one artifact and its version number, use the Versions_* tools instead. Accepts the release's UUID or its short key.")]
    [McpTool("Releases_GetRelease", "Get release")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReleaseDto>> GetRelease(string idOrKey, CancellationToken cancellationToken)
    {
        var release = await _dispatcher.Send(new GetReleaseQuery(new IdOrKey(idOrKey)), cancellationToken);

        return release is not null
            ? Ok(release)
            : NotFound();
    }

    [HttpGet("{idOrKey}/activities")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Releases)]
    [OpenApiOperation(
        "Get a release's activity history, newest first: every change recorded on the release — details, contents, dates and status.",
        "Each entry has a `category` (Created, Updated, ScheduleChanged, StatusChanged, StateChanged, Health, Removed, Baseline), an `actorKind` (User, System, Import, Sync, Anonymous) with the acting `employee` when there is one, a `timestamp`, a one-line `summary`, and a `payload`: the event's fields as a JSON string. A change carries both ends, the value before and after. People in a payload are employee ids, not user ids. A Baseline entry marks where tracking began for a record that already existed, holding what it looked like then; nothing before it was recorded. An entry with `isRelated: true` was raised on another record and is listed here because it concerns this one; `raisedOn` names that record, or is null where it could not be resolved (typically removed since). Paged: the response carries `totalCount` and `hasNextPage`.")]
    [McpTool("Releases_GetActivities", "Get release activity history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<ActivityLogDto>>> GetActivities(string idOrKey, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.Send(new GetReleaseActivitiesQuery(new IdOrKey(idOrKey), page, pageSize), cancellationToken);

        return result.Value is not null
            ? Ok(result.Value)
            : NotFound();
    }

    [HttpGet("{idOrKey}/status-history")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Releases)]
    [OpenApiOperation(
        "Get a release's status change history, newest first.",
        "Each entry reports the status names as they were at the time, so a status renamed since does not rewrite the past. Correcting a date leaves this untouched — that is the point of having a separate action for it.")]
    [McpTool("Releases_GetStatusHistory", "Get release status history")]
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
    [OpenApiOperation(
        "Draft a release — the announcement, before it carries anything.",
        "A release is what was announced to customers, not what was built — for one artifact and its version number, use the Versions_* tools instead. Contents are attached afterwards with Releases_SetContents, because an announcement is commonly drafted before anyone knows which versions will make it. The product is optional and usually names a product *line* rather than a leaf; leave it empty for a release spanning product lines. Unlike a version, a release is not restricted to releasable product types.")]
    [McpTool("Releases_Plan", "Plan a release", Destructive = false)]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Plan(
        [FromBody] PlanReleaseRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToPlanReleaseCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetRelease), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("import")]
    [MustHavePermission(ApplicationAction.Import, ApplicationResource.Releases)]
    [OpenApiOperation(
        "Submit a csv file of releases to import. Returns the run — 200 once it has finished, 202 while it is still queued or running.",
        "Takes two files: one row per release, and one row per thing it announces, naming the ImportId of the release it belongs to. The contents file is optional — an empty release is a legitimate state. A release marked released is refused while anything it carries has not shipped.")]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [CsvImport(ReleaseImportDefinition.ImportKey)]
    public async Task<ActionResult> Import(
        [FromForm, CsvRows(typeof(ImportReleaseRequest))] IFormFile file,
        [FromForm, CsvRows(typeof(ImportReleaseContentRequest), Label = "Contents")] IFormFile? contentsFile,
        [FromQuery] Guid? submissionGroupId, [FromQuery] bool validateOnly,
        [FromServices] ImportSubmissionResponder responder,
        CancellationToken cancellationToken)
    {
        try
        {
            var importedReleases = _csvService.ReadCsv<ImportReleaseRequest>(file.OpenReadStream());

            List<ImportReleaseRequest> releases = [];
            var validator = new ImportReleaseRequestValidator();
            foreach (var release in importedReleases)
            {
                var validationResults = await validator.ValidateAsync(release, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        error.ErrorMessage = $"{error.ErrorMessage} (Release: {release.Version})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                }

                releases.Add(release);
            }

            var contentsByRelease = new Dictionary<string, List<ImportReleaseContentDto>>(StringComparer.OrdinalIgnoreCase);
            if (contentsFile is not null)
            {
                var importedContents = _csvService.ReadCsv<ImportReleaseContentRequest>(contentsFile.OpenReadStream());

                var contentValidator = new ImportReleaseContentRequestValidator();
                foreach (var content in importedContents)
                {
                    var validationResults = await contentValidator.ValidateAsync(content, cancellationToken);
                    if (!validationResults.IsValid)
                    {
                        foreach (var error in validationResults.Errors)
                        {
                            error.ErrorMessage = $"{error.ErrorMessage} (Release: {content.ReleaseImportId})";
                            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                        }
                        return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                    }

                    var parent = content.ReleaseImportId.Trim();
                    if (!contentsByRelease.TryGetValue(parent, out var forRelease))
                        contentsByRelease[parent] = forRelease = [];

                    forRelease.Add(content.ToImportReleaseContentDto());
                }
            }

            // The same key the run reports row outcomes against, so a content row and its release row
            // agree on what the parent is called whether or not the file supplied an ImportId.
            List<SubmittedImportRow<ImportReleaseDto>> rows = [];
            HashSet<string> claimedParents = new(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < releases.Count; i++)
            {
                var release = releases[i];
                var key = SubmittedImportRow.KeyFor(release.ImportId, i + 1);

                claimedParents.Add(key);
                rows.Add(new SubmittedImportRow<ImportReleaseDto>(
                    release.ImportId,
                    release.ToImportReleaseDto(
                        contentsByRelease.TryGetValue(key, out var forRelease) ? forRelease : [])));
            }

            // Every content row must find its release. A row naming one that is not in the release file is
            // a mistyped reference rather than a row to drop, so it fails the submission.
            var orphaned = contentsByRelease.Keys.Where(k => !claimedParents.Contains(k)).ToList();
            if (orphaned.Count > 0)
            {
                ModelState.AddModelError(
                    nameof(ImportReleaseContentRequest.ReleaseImportId),
                    $"The following content rows name a release that is not in the file: {string.Join(", ", orphaned.Select(v => $"'{v}'"))}.");
                return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
            }

            var result = await _dispatcher.Send(new ImportReleasesCommand(rows, submissionGroupId, validateOnly), cancellationToken);

            return result.IsSuccess
                ? await responder.Respond(this, result.Value, cancellationToken)
                : BadRequest(result.ToBadRequestObject(HttpContext));
        }
        catch (CsvHelperException ex)
        {
            return BadRequest(ProblemDetailsExtensions.ForBadRequest(ex.Message, HttpContext));
        }
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Releases)]
    [OpenApiOperation(
        "Update a release's descriptive fields.",
        "**This is a whole-record overwrite: an omitted field is cleared.** Send every value the release should end up with, including ones you are not changing — omitting the product makes the release span product lines, and omitting the notes deletes them. The dates and the contents are not here; each has its own tool because each carries a rule this one does not.")]
    [McpTool("Releases_Update", "Update a release", Idempotent = false)]
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

    [HttpPut("{id}/contents")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Releases)]
    [OpenApiOperation(
        "Set everything a release announces — the packages it shipped and the versions it carries directly — in one call.",
        "**This is a whole-set replacement of both routes: anything left out is removed, and sending two empty lists clears the release entirely.** Read the release first and send back the full intended result, not just what you are adding.\n\nContents arrive two ways and a release may use both: packages (the usual route, since a package is the deployment unit) and versions carried directly (for a single artifact that shipped alone, where nobody assembled a package).\n\n**A version shipping inside one of the supplied packages cannot also be carried directly** — that would announce the same shipment twice. The rule is judged against what the release ends up containing, so moving a version out of the direct list and into a package that ships it is allowed in this one call. A manifest line naming no version record covers nothing and never conflicts.\n\nAn empty release is legitimate rather than a draft: a repackaging or a pricing change is announced with nothing deployed. Contents freeze once the release is announced or withdrawn.")]
    [McpTool("Releases_SetContents", "Set what a release announces", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SetContents(
        Guid id, [FromBody] SetReleaseContentsRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new SetReleaseContentsCommand(id, request.VersionIds, request.PackageIds), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/target-date")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Releases)]
    [OpenApiOperation(
        "Move or clear a release's target date.",
        "Omitting the date records that the release is no longer targeted, which is a different statement from never having set one. Refused on a release in a terminal status.")]
    [McpTool("Releases_MoveTargetDate", "Move a release target date", Idempotent = false)]
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
        "Fix a release's target or announced date that was recorded wrongly.",
        "The status does not move and the status history is left untouched — that is the point of having this separate from Releases_MarkReleased, which asserts the release moved and refuses to run twice. **Both dates are sent, so an omitted target date is cleared.** The announced date cannot be cleared once set: an announced release with no announced date contradicts its own status — revert it instead. There is no cut date to correct; a release is never cut.")]
    [McpTool("Releases_CorrectDates", "Correct release dates", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CorrectDates(
        Guid id, [FromBody] CorrectReleaseDatesRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            new CorrectReleaseDatesCommand(id, request.TargetDate, request.ReleasedDate),
            cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/release")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Releases)]
    [OpenApiOperation(
        "Record that a release was announced to customers.",
        "**Refused while the release carries a version or package that has not shipped** — telling customers a release is out while something inside it has not gone anywhere is the one claim a release can make that its own contents contradict. Call Releases_GetRelease first and check each contents entry's shipped date; release the outstanding ones, or remove them from this release. An empty release announces normally. Shipping and announcing are separate acts, so this date is commonly later than the date the contents shipped.")]
    [McpTool("Releases_MarkReleased", "Announce a release")]
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

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.Releases)]
    [OpenApiOperation(
        "Permanently delete a release in any state, with its list of contents and its status history.",
        "**The versions and packages it listed are kept** — only the release and its links to them go. For a release created by mistake, or when the user asks to purge history; a real announcement that was retracted is `Releases_Withdraw`. Deleting an announced release is also how a package it lists becomes deletable, since its contents cannot otherwise change. Needs the release Delete permission.")]
    [McpTool("Releases_Delete", "Delete a release", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteReleaseCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/withdraw")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Releases)]
    [OpenApiOperation(
        "Retract a release after it was announced.",
        "Terminal, and it says **nothing about the versions it carried** — an artifact that shipped has shipped whatever the market was later told, so a version that was itself pulled is withdrawn on its own record. Use this only when a real announcement was retracted; if the release was marked announced by mistake and never actually went out, use Releases_Revert instead.")]
    [McpTool("Releases_Withdraw", "Withdraw a release")]
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

    [HttpPost("{id}/revert")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Releases)]
    [OpenApiOperation(
        "Record that a release marked as announced was **not in fact announced** — the wrong record was updated, and it never went out.",
        "Returns the release to a live status and clears its announced date. A reason is required, unlike a withdrawal's optional one: this contradicts something the append-only history already asserts, so the record has to say why. Do not use this for a release that really was announced and then retracted — that is Releases_Withdraw.")]
    [McpTool("Releases_Revert", "Revert a release")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Revert(
        Guid id, [FromBody] RevertReleaseRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RevertReleaseCommand(id, request.Reason), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
