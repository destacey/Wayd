using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.DemandManagement.Application.IntakeRequestTagCategories;
using Wayd.DemandManagement.Application.IntakeRequestTags.Commands;
using Wayd.DemandManagement.Application.IntakeRequestTags.Dtos;
using Wayd.DemandManagement.Application.IntakeRequestTags.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.DemandManagement.IntakeRequestTags;

namespace Wayd.Web.Api.Controllers.DemandManagement;

/// <summary>
/// Intake request tags across every axis, and the free tags — tags on no axis — that agents add while tagging.
/// </summary>
/// <remarks>
/// A tag on an axis is changed through <see cref="IntakeRequestTagCategoriesController"/>, because uniqueness
/// within an axis is the axis's rule. A free tag has no axis, so it is changed here, by the Demand Management
/// Admin. Applying a tag to a request is on the intake requests endpoints.
/// </remarks>
[Route("api/demand-management/intake-request-tags")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(FeatureFlags.Names.DemandManagement)]
[McpTools(McpToolset.Intake, "wayd-intake")]
public class IntakeRequestTagsController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [McpTool("IntakeRequestTags_Search", "Search intake request tags")]
    [Authorize]
    [EndpointSummary("Search intake request tags, on axes and free, with how many visible requests carry each.")]
    [EndpointDescription("Sorted by name. `search` keeps tags whose name contains it, ignoring case. `isFree` true returns only free tags — tags on no axis, added by an agent typing a name while tagging — and false only tags on axes. A free tag has no `categoryId` or `categoryName`, and is listed only when the caller can see a request carrying it, or holds the Demand Management Admin permission. `requestCount` counts only requests the caller can see.\n\nTags that cannot be applied — inactive ones, and those on inactive axes — are left out unless `includeInactive` is true.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<IntakeRequestTagListItemDto>>> Search([FromQuery] string? search = null, [FromQuery] bool? isFree = null, [FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var tags = await _dispatcher.Send(new GetIntakeRequestTagsQuery(search, isFree, includeInactive), cancellationToken);
        return Ok(tags);
    }

    [HttpPut("{id}")]
    [McpTool("IntakeRequestTags_UpdateFreeTag", "Rename a free intake request tag")]
    [MustHavePermission(ApplicationAction.Administer, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Rename a free tag or change its description.")]
    [EndpointDescription("Free tags only; a tag on an axis is renamed with `IntakeRequestTagCategories_UpdateTag`. Names are unique among free tags, ignoring case — to fold a free tag into one that already has the name, use `IntakeRequestTags_Merge`. Every request carrying the tag reads the new name.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateFreeTag(Guid id, [FromBody] UpdateFreeIntakeRequestTagRequest request, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new UpdateFreeIntakeRequestTagCommand(id, request.Name, request.Description), cancellationToken));

    [HttpPost("{id}/merge")]
    [McpTool("IntakeRequestTags_Merge", "Merge a free intake request tag into another tag")]
    [MustHavePermission(ApplicationAction.Administer, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Merge a free tag into another tag.")]
    [EndpointDescription("Every request carrying the free tag `id`, on every desk, carries `targetTagId` instead, and the free tag is deleted. The target may be another free tag, to fold near-duplicates together, or an active tag on an active axis — which is how a free tag that caught on is promoted: add the tag to its axis with `IntakeRequestTagCategories_AddTag`, then merge into it.\n\n**On an axis that takes one tag, a request's existing tag there is replaced**, as tagging always does. Each request records the change in its history.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Merge(Guid id, [FromBody] MergeIntakeRequestTagRequest request, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new MergeIntakeRequestTagCommand(id, request.TargetTagId), cancellationToken));

    [HttpDelete("{id}")]
    [McpTool("IntakeRequestTags_DeleteFreeTag", "Delete a free intake request tag", Idempotent = false)]
    [MustHavePermission(ApplicationAction.Administer, ApplicationResource.IntakeRequests)]
    [EndpointSummary("Delete a free tag, taking it off every request that carries it.")]
    [EndpointDescription("Free tags only; a tag on an axis is deleted with `IntakeRequestTagCategories_DeleteTag`. **Unlike that call, this succeeds while requests carry the tag: it removes the tag from each of them, on every desk**, and each records the change. To keep those requests filed under another tag, use `IntakeRequestTags_Merge` instead. A free tag is also deleted on its own once the last request carrying it loses it.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteFreeTag(Guid id, CancellationToken cancellationToken) =>
        NoContentOrError(await _dispatcher.Send(new DeleteFreeIntakeRequestTagCommand(id), cancellationToken));

    private ActionResult NoContentOrError(Result result) =>
        result.IsSuccess
            ? NoContent()
            : result.Error is IntakeRequestTagCategoryErrors.TagNotFoundError
                ? NotFound()
                : BadRequest(result.ToBadRequestObject(HttpContext));
}
