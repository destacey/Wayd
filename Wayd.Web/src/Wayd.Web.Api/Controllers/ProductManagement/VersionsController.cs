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
using Wayd.ProductManagement.Application.Versions.Commands;
using Wayd.ProductManagement.Application.Versions.Dtos;
using Wayd.ProductManagement.Application.Versions.Imports;
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
[McpTools(McpToolset.Delivery)]
public class VersionsController(IDispatcher dispatcher, ICsvService csvService, ISettings<SchedulingSettings> schedulingSettings, ILogger<VersionsController> logger) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;
    private readonly ICsvService _csvService = csvService;
    private readonly ISettings<SchedulingSettings> _schedulingSettings = schedulingSettings;
    private readonly ILogger<VersionsController> _logger = logger;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "List versions — the artifacts that were built, such as `Wayd API 4.12.0`.",
        "A version is one artifact that was built, not the announcement made to customers — for that, use the Releases_* tools instead. Everything not yet shipped comes first, then what has shipped, newest first: what is still coming is usually what you are looking for. Never ordered by the version number, which is free text and never parsed — `4.8.2` and `2026.04` are both just labels. There is no package filter here: a version carries no pointer to the package it shipped in, so ask that question from the packages side with ReleasePackages_GetReleasePackages.")]
    [McpTool("Versions_GetVersions", "List versions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<VersionDto>>> GetVersions(
        [FromQuery] Guid? productId,
        [FromQuery] StatusCategory[]? statusCategory,
        CancellationToken cancellationToken)
    {
        var versions = await _dispatcher.Send(
            new GetVersionsQuery(productId, statusCategory), cancellationToken);

        return Ok(versions);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Get one version in full — its product, version number, and its target date and its cut and released moments.",
        "A version is one artifact that was built, not the announcement made to customers — for that, use the Releases_* tools instead. Accepts the version's UUID or its short key.")]
    [McpTool("Versions_GetVersion", "Get version")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VersionDto>> GetVersion(string idOrKey, CancellationToken cancellationToken)
    {
        var version = await _dispatcher.Send(new GetVersionQuery(new IdOrKey(idOrKey)), cancellationToken);

        return version is not null
            ? Ok(version)
            : NotFound();
    }

    [HttpGet("{idOrKey}/activities")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Get a version's activity history, newest first: every change recorded on the version — details, dates and status.",
        "Each entry has a `category` (Created, Updated, ScheduleChanged, StatusChanged, StateChanged, Health, Removed, Baseline), an `actorKind` (User, System, Import, Sync, Anonymous) with the acting `employee` when there is one, a `timestamp`, a one-line `summary`, and a `payload`: the event's fields as a JSON string. A change carries both ends, the value before and after. People in a payload are employee ids, not user ids. A Baseline entry marks where tracking began for a record that already existed, holding what it looked like then; nothing before it was recorded. An entry with `isRelated: true` was raised on another record and is listed here because it concerns this one; `raisedOn` names that record, or is null where it could not be resolved (typically removed since). Paged: the response carries `totalCount` and `hasNextPage`.")]
    [McpTool("Versions_GetActivities", "Get version activity history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<ActivityLogDto>>> GetActivities(string idOrKey, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _dispatcher.Send(new GetVersionActivitiesQuery(new IdOrKey(idOrKey), page, pageSize), cancellationToken);

        return result.Value is not null
            ? Ok(result.Value)
            : NotFound();
    }

    [HttpGet("{idOrKey}/status-history")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Get a version's status change history, newest first — when it was cut, how long it sat ready before shipping, who moved it.",
        "Each entry reports the status names as they were at the time, so a status renamed since does not rewrite the past. Correcting a date leaves this untouched.")]
    [McpTool("Versions_GetStatusHistory", "Get version status history")]
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
    [OpenApiOperation(
        "Record a version against a product.",
        "A version is one artifact that was built, not the announcement made to customers — for that, use the Releases_* tools instead. The product is required and **must be of a releasable type** — a version is a cut of something that ships, so the API refuses a product line or other non-releasable node. Only the target date is set here; cutting and releasing are their own actions, because each records something that happened and each carries its own rule.")]
    [McpTool("Versions_Plan", "Plan a version", Destructive = false)]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Plan(
        [FromBody] PlanVersionRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToPlanVersionCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetVersion), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("import")]
    [MustHavePermission(ApplicationAction.Import, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Submit a csv file of versions to import. Returns the run — 200 once it has finished, 202 while it is still queued or running.",
        "Each row is planned against its product by id and walked to the state its moments describe: neither leaves it planned, a cut moment makes it ready, a released moment makes it released. Moments are ISO-8601 timestamps with an offset.")]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ImportProcessDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [CsvImport(VersionImportDefinition.ImportKey)]
    public async Task<ActionResult> Import([FromForm, CsvRows(typeof(ImportVersionRequest))] IFormFile file, [FromQuery] Guid? submissionGroupId, [FromQuery] bool validateOnly, [FromServices] ImportSubmissionResponder responder, CancellationToken cancellationToken)
    {
        try
        {
            var importedVersions = _csvService.ReadCsv<ImportVersionRequest>(file.OpenReadStream());

            List<SubmittedImportRow<ImportVersionDto>> rows = [];
            var validator = new ImportVersionRequestValidator();
            foreach (var version in importedVersions)
            {
                var validationResults = await validator.ValidateAsync(version, cancellationToken);
                if (!validationResults.IsValid)
                {
                    foreach (var error in validationResults.Errors)
                    {
                        // Both halves of the key: a number alone does not identify a row, since two
                        // products may each carry the same one.
                        error.ErrorMessage = $"{error.ErrorMessage} (Product: {version.ProductId}, Version: {version.Number})";
                        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                    }
                    return UnprocessableEntity(ProblemDetailsExtensions.ForValidationErrors(ModelState, HttpContext));
                }

                rows.Add(new SubmittedImportRow<ImportVersionDto>(version.ImportId, version.ToImportVersionDto()));
            }

            var result = await _dispatcher.Send(new ImportVersionsCommand(rows, submissionGroupId, validateOnly), cancellationToken);

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
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Update a version's descriptive fields.",
        "**This is a whole-record overwrite: an omitted field is cleared.** Send every value the version should end up with, including ones you are not changing. The dates are not here — each carries a rule the aggregate enforces, and folding them into a blanket save would hide which rule refused.")]
    [McpTool("Versions_Update", "Update a version", Idempotent = false)]
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
    [OpenApiOperation(
        "Move or clear a version's target date.",
        "Omitting the date records that the version is no longer targeted, which is a different statement from never having set one. Refused on a released or withdrawn version.")]
    [McpTool("Versions_MoveTargetDate", "Move a version target date", Idempotent = false)]
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
        "Fix a version's target date or cut or released moment that was recorded wrongly.",
        "The status does not move and the status history is left untouched — that is the point of having this separate from Versions_Cut and Versions_MarkReleased, which assert the version moved and refuse to run twice. **All three are sent, so an omitted target date or cut moment is cleared.** The released moment cannot be cleared once set: a released record with no released moment contradicts its own status — revert it instead. A version cannot be released before it was cut. The target date is a calendar date; the cut and released moments are instants, sent with their offset exactly as the CI/CD system reports them. Refused on a withdrawn version.")]
    [McpTool("Versions_CorrectDates", "Correct version dates", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CorrectDates(
        Guid id, [FromBody] CorrectVersionDatesRequest request, CancellationToken cancellationToken)
    {
        var zone = await LegacyDeliveryDates.ZoneForRequest(
            request.UsesLegacyDates(), _schedulingSettings, _logger, "Correct version dates", cancellationToken);

        var result = await _dispatcher.Send(
            new CorrectVersionDatesCommand(id, request.TargetDate, request.ResolveCutAt(zone), request.ResolveReleasedAt(zone)),
            cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/cut")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Record that a version was cut — scope is frozen and it is ready to ship.",
        "**One-way: a version cannot be cut twice**, and a released or withdrawn version refuses it. Cutting is not a prerequisite for releasing: a version imported after the fact can be marked released without ever having been cut, which is why this is a separate action rather than a step.")]
    [McpTool("Versions_Cut", "Cut a version")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Cut(
        Guid id, [FromBody] CutVersionRequest request, CancellationToken cancellationToken)
    {
        var zone = await LegacyDeliveryDates.ZoneForRequest(
            request.UsesLegacyDates(), _schedulingSettings, _logger, "Cut version", cancellationToken);

        var result = await _dispatcher.Send(new CutVersionCommand(id, request.ResolveCutAt(zone)), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    // Named for the act, matching the same action on a release and a package. It was {id}/version
    // after Release was renamed to Version, which read as though it created one.
    [HttpPost("{id}/release")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Record that a version shipped.",
        "**This is not announcing it to customers** — that is Releases_MarkReleased on a release. A version can be marked released without ever having been cut, which is what makes importing historical versions possible; where it was cut, it cannot be released before it was cut. Refused on a withdrawn version.")]
    [McpTool("Versions_MarkReleased", "Mark a version released")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> MarkReleased(
        Guid id, [FromBody] MarkVersionReleasedRequest request, CancellationToken cancellationToken)
    {
        var zone = await LegacyDeliveryDates.ZoneForRequest(
            request.UsesLegacyDates(), _schedulingSettings, _logger, "Mark version released", cancellationToken);

        var result = await _dispatcher.Send(
            new MarkVersionReleasedCommand(id, request.ResolveReleasedAt(zone)), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Permanently delete a version with its status history and **every deployment of it**.",
        "The delivery measures and rollout stop counting those deployments. **Refused while a release lists it or a package manifest names it** — remove it with `Releases_SetContents` or `ReleasePackages_SetManifest` first, or delete that release or package (an announced release or released package cannot change, so deleting it is the only way). For a version recorded by mistake or when the user asks to purge history; a real version that was pulled is `Versions_Withdraw`. Needs the delivery Delete permission.")]
    [McpTool("Versions_Delete", "Delete a version", Idempotent = false)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteVersionCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/withdraw")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Delivery)]
    [OpenApiOperation(
        "Pull a version.",
        "Terminal. **A released version can still be withdrawn** — pulling something after it shipped is exactly the case this exists for — but a withdrawn one cannot be released. Use this only when a real version was pulled; if it was marked released by mistake and never actually shipped, use Versions_Revert instead.")]
    [McpTool("Versions_Withdraw", "Withdraw a version")]
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
        "Record that a version marked as shipped did **not in fact ship** — the wrong record was updated.",
        "Returns it to Ready, or to the initial status where it was never cut, and clears the released moment. A reason is required, unlike a withdrawal's optional one: this contradicts something the append-only history already asserts. Do not use this for a version that really shipped and was then pulled — that is Versions_Withdraw.")]
    [McpTool("Versions_Revert", "Revert a version")]
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
