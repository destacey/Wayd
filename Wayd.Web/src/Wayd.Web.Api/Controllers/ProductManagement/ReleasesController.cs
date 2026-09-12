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
public class ReleasesController(IDispatcher dispatcher, ICsvService csvService) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICsvService _csvService = csvService;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Releases)]
    [OpenApiOperation("Get a list of releases.", "Ordered by released date then sequence — never by the version label, which is free text.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<ReleaseDto>>> GetReleases(
        [FromQuery] Guid? productId,
        [FromQuery] int[]? statusCategory,
        [FromQuery] Guid? containingVersionId,
        CancellationToken cancellationToken)
    {
        StatusCategory[]? categories = statusCategory is { Length: > 0 }
            ? [.. statusCategory.Select(c => (StatusCategory)c)]
            : null;

        var releases = await _dispatcher.Send(
            new GetReleasesQuery(productId, categories, containingVersionId), cancellationToken);

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

    [HttpGet("{idOrKey}/activities")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Releases)]
    [OpenApiOperation("Get activity history for the release.", "")]
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
    [OpenApiOperation("Plan a release.", "Contents are attached afterwards — an announcement is commonly drafted before anyone knows which versions will make it.")]
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
    public async Task<ActionResult> Import(
        [FromForm] IFormFile file,
        [FromForm] IFormFile? contentsFile,
        [FromQuery] Guid? submissionGroupId,
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

            var result = await _dispatcher.Send(new ImportReleasesCommand(rows, submissionGroupId), cancellationToken);

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

    [HttpPut("{id}/contents")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Releases)]
    [OpenApiOperation(
        "Set what a release announces.",
        "Whole-set replacement of both routes at once: anything left out is removed, and both lists empty clears the release. A version shipping inside one of the supplied packages cannot also be carried directly, so that one shipment is announced once.")]
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
        "Correct a release's recorded target and released dates.",
        "Fixes dates entered wrongly without changing the release's status. Both are sent, so an omitted target date is cleared. The released date cannot be cleared — revert the release instead. There is no cut date: a release is never cut.")]
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
        "Record that a release was announced.",
        "Refused while the release carries a version or package that has not shipped — telling customers a release shipped while something inside it has not is the one claim a release can make that its own contents contradict.")]
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
    [OpenApiOperation(
        "Retract a release.",
        "Says nothing about the versions it carried: an artifact that shipped has shipped whatever the market was later told, so each version is withdrawn separately where it too was pulled.")]
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
        "Revert a release announced in error.",
        "For a release marked announced by mistake. Moves it back to Ready and clears the released date. Not a withdrawal — that retracts an announcement which really went out.")]
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
