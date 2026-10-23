using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Requests;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Application.Roadmaps.Commands;
using Wayd.Planning.Application.Roadmaps.Dtos;
using Wayd.Planning.Application.Roadmaps.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Planning.Roadmaps;

namespace Wayd.Web.Api.Controllers.Planning;

[Route("api/planning/[controller]")]
[ApiVersionNeutral]
[ApiController]
[McpTools(McpToolset.Planning, "wayd-roadmaps")]
public class RoadmapsController : ControllerBase
{
    private readonly ILogger<RoadmapsController> _logger;
    private readonly IDispatcher _dispatcher;

    public RoadmapsController(
        ILogger<RoadmapsController> logger,
        IDispatcher dispatcher)
    {
        _logger = logger;
        _dispatcher = dispatcher;
    }

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Roadmaps)]
    [EndpointSummary("Get a list of roadmaps.")]
    [McpTool("Roadmaps_GetRoadmaps", "List roadmaps")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RoadmapListDto>>> GetRoadmaps([FromQuery] RoadmapState[]? state, CancellationToken cancellationToken)
    {
        var roadmaps = await _dispatcher.Send(new GetRoadmapsQuery(state), cancellationToken);
        return Ok(roadmaps);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Roadmaps)]
    [EndpointSummary("Get roadmap details.")]
    [McpTool("Roadmaps_GetRoadmap", "Get roadmap")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoadmapDetailsDto>> GetRoadmap(string idOrKey, CancellationToken cancellationToken)
    {
        var roadmap = await _dispatcher.Send(new GetRoadmapQuery(idOrKey), cancellationToken);

        return roadmap is not null
            ? Ok(roadmap)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Roadmaps)]
    [EndpointSummary("Create a roadmap.")]
    [EndpointDescription(
        "`roadmapManagerIds` are employee UUIDs and must include the caller's own employee: only roadmap managers can change a roadmap. `visibilityId` is 1 Public (everyone can see it) or 2 Private (only its managers can); Roadmaps_GetVisibilityOptions lists them. `start` must be on or before `end`. The new roadmap is Active and has no items. Returns its `id` and `key`.")]
    [McpTool("Roadmaps_CreateRoadmap", "Create roadmap", Destructive = false)]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create([FromBody] CreateRoadmapRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateRoadmapCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetRoadmap), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("copy")]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Roadmaps)]
    [EndpointSummary("Copy an existing roadmap.")]
    [EndpointDescription(
        "Creates a new Active roadmap with the source's description, dates and every item (activities, timeboxes and milestones, with their nesting, colors and order), under the given `name`, `roadmapManagerIds` and `visibilityId` (1 Public, 2 Private). The source's color legend is not copied. `sourceRoadmapId` is the source's UUID; it must be public or managed by the caller. `roadmapManagerIds` are employee UUIDs and must include the caller's own employee. The source is unchanged. Returns the copy's `id` and `key`.")]
    [McpTool("Roadmaps_CopyRoadmap", "Copy roadmap", Destructive = false)]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Copy([FromBody] CopyRoadmapRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCopyRoadmapCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetRoadmap), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Roadmaps)]
    [EndpointSummary("Update a roadmap.")]
    [EndpointDescription(
        "A whole-record overwrite of the name, description, dates, managers and visibility: an omitted `description` is cleared and `roadmapManagerIds` replaces the managers, so leaving a manager out removes them. Read the roadmap with Roadmaps_GetRoadmap first and pass back what should stay. The managers must include the caller's own employee, and only a manager can update. `visibilityId` is 1 Public or 2 Private. An archived roadmap cannot be changed; reactivate it first. Takes the roadmap's UUID, in the path and again as `id` in the body.")]
    [McpTool("Roadmaps_UpdateRoadmap", "Update roadmap")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateRoadmapRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateRoadmapCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/colors")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Roadmaps)]
    [EndpointSummary("Update the configured colors for a roadmap.")]
    [EndpointDescription(
        "Replaces the roadmap's color legend: `colors` is the complete set, each a hex `color` (e.g. #4096FF) with a caption `name` (at most 32 characters), an `order` and `isDefault`; a color left out is removed and an empty list clears the legend. At most 30 colors, at most one default, no color value twice. Items keep their own colors. Read the current legend with Roadmaps_GetRoadmap first. Only a manager of an active roadmap can change it. Takes the roadmap's UUID, in the path and again as `roadmapId` in the body.")]
    [McpTool("Roadmaps_UpdateColors", "Set roadmap colors")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateColors(Guid id, [FromBody] UpdateRoadmapColorsRequest request, CancellationToken cancellationToken)
    {
        if (id != request.RoadmapId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateRoadmapColorsCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.Roadmaps)]
    [EndpointSummary("Delete a roadmap.")]
    [EndpointDescription(
        "Permanently deletes the roadmap and all its items. Only a manager can delete it, and an archived roadmap must be reactivated first; to retire a roadmap but keep it, use Roadmaps_ArchiveRoadmap instead. Takes the roadmap's UUID.")]
    [McpTool("Roadmaps_DeleteRoadmap", "Delete roadmap")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteRoadmapCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/archive")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Roadmaps)]
    [EndpointSummary("Archive a roadmap.")]
    [EndpointDescription(
        "Moves an Active roadmap to Archived, keeping it and its items; an archived roadmap is read-only until reactivated with Roadmaps_ActivateRoadmap. Only a manager can archive it. Takes the roadmap's UUID.")]
    [McpTool("Roadmaps_ArchiveRoadmap", "Archive roadmap")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ArchiveRoadmapCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/activate")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Roadmaps)]
    [EndpointSummary("Activate a roadmap.")]
    [EndpointDescription(
        "Moves an Archived roadmap back to Active so it can be changed again. Only a manager can activate it. Takes the roadmap's UUID.")]
    [McpTool("Roadmaps_ActivateRoadmap", "Activate roadmap")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ActivateRoadmapCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #region Roadmap Items

    [HttpGet("{idOrKey}/items")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Roadmaps)]
    [EndpointSummary("Get all items (activities, timeboxes, milestones) for a roadmap.")]
    [McpTool("Roadmaps_GetItems", "List roadmap items")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RoadmapItemListDto>>> GetItems(string idOrKey, CancellationToken cancellationToken)
    {
        var items = await _dispatcher.Send(new GetRoadmapItemsQuery(idOrKey), cancellationToken);
        return Ok(items);
    }

    [HttpGet("{idOrKey}/items/activities")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Roadmaps)]
    [EndpointSummary("Get roadmap activities.")]
    [McpTool("Roadmaps_GetActivities", "List roadmap activities")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RoadmapActivityListDto>>> GetActivities(string idOrKey, CancellationToken cancellationToken)
    {
        var items = await _dispatcher.Send(new GetRoadmapActivitiesQuery(idOrKey), cancellationToken);
        return Ok(items);
    }

    [HttpGet("{roadmapIdOrKey}/items/{itemId}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Roadmaps)]
    [EndpointSummary("Get roadmap item details.")]
    [McpTool("Roadmaps_GetItem", "Get roadmap item")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoadmapItemDetailsDto>> GetItem(string roadmapIdOrKey, Guid itemId, CancellationToken cancellationToken)
    {
        var item = await _dispatcher.Send(new GetRoadmapItemQuery(roadmapIdOrKey, itemId), cancellationToken);
        return this.OkPolymorphic(item);
    }

    [HttpPost("{roadmapId}/items")]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Roadmaps)]
    [EndpointSummary("Create a roadmap item of type: Activity, Timebox, Milestone.")]
    [EndpointDescription(
        "The body's `$type` picks the item type and must be its first property: \"activity\" or \"timebox\" with `start` and `end`, or \"milestone\" with `date`. Every type takes `roadmapId` (the roadmap's UUID, also in the path), `name`, and optional `description`, hex `color` and `parentId` — the UUID of an activity on the roadmap to nest the item under; only activities can be parents, and with no parent the item is top-level. Only a manager of an active roadmap can add items. Returns the new item's id.")]
    [McpTool("Roadmaps_CreateItem", "Create roadmap item", Destructive = false)]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201Guid))]
    public async Task<ActionResult<Guid>> CreateItem(Guid roadmapId, [FromBody] CreateRoadmapItemRequest request, CancellationToken cancellationToken)
    {
        if (roadmapId != request.RoadmapId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(roadmapId), nameof(request.RoadmapId), HttpContext));

        var command = request switch
        {
            CreateRoadmapActivityRequest activity => activity.ToCreateRoadmapItemCommand(),
            CreateRoadmapTimeboxRequest timebox => timebox.ToCreateRoadmapItemCommand(),
            CreateRoadmapMilestoneRequest milestone => milestone.ToCreateRoadmapItemCommand(),
            _ => throw new ArgumentException("Invalid roadmap item type", nameof(request))
        };

        var result = await _dispatcher.Send(command, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetItem), new { roadmapIdOrKey = roadmapId, itemId = result.Value.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{roadmapId}/items/{itemId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Roadmaps)]
    [EndpointSummary("Update a roadmap item of type: Activity, Timebox, Milestone.")]
    [EndpointDescription(
        "A whole-record overwrite: name, description, parent, color and dates are all set from the body, so an omitted `description` or `color` is cleared and an omitted `parentId` moves the item to the top level. Read the item with Roadmaps_GetItem first and pass back what should stay, or use Roadmaps_PatchItem to change only some fields. The body's `$type` must be its first property and match the item's type: \"activity\" or \"timebox\" with `start` and `end`, \"milestone\" with `date`. Both ids are UUIDs, given in the path and again as `roadmapId` and `itemId` in the body. Only a manager of an active roadmap can change it.")]
    [McpTool("Roadmaps_UpdateItem", "Update roadmap item")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateItem(Guid roadmapId, Guid itemId, [FromBody] UpdateRoadmapItemRequest request, CancellationToken cancellationToken)
    {
        if (roadmapId != request.RoadmapId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(roadmapId), nameof(request.RoadmapId), HttpContext));
        else if (itemId != request.ItemId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(itemId), nameof(request.ItemId), HttpContext));

        var command = request switch
        {
            UpdateRoadmapActivityRequest activity => activity.ToUpdateRoadmapItemCommand(),
            UpdateRoadmapTimeboxRequest timebox => timebox.ToUpdateRoadmapItemCommand(),
            UpdateRoadmapMilestoneRequest milestone => milestone.ToUpdateRoadmapItemCommand(),
            _ => throw new ArgumentException("Invalid roadmap item type", nameof(request))
        };

        var result = await _dispatcher.Send(command, cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPatch("{roadmapId}/items/{itemId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Roadmaps)]
    [EndpointSummary("Partially update a roadmap item using JSON Patch (RFC 6902).")]
    [EndpointDescription(
        "Works for an Activity, Timebox or Milestone; the item keeps its type. Paths are the update's camelCase fields: /name, /description, /parentId, /color, plus /start and /end for an activity or timebox, or /date for a milestone. `replace` changes a field; a field the patch does not name keeps its value. Confirm with the user before calling.")]
    [McpTool("Roadmaps_PatchItem", "Patch a roadmap item")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<ActionResult> PatchRoadmapItem(
        Guid roadmapId,
        Guid itemId,
        [FromBody] JsonPatchDocument<UpdateRoadmapItemRequest> patchDocument,
        CancellationToken cancellationToken)
        => this.Patch(
            patchDocument,
            ct => _dispatcher.Send(new GetRoadmapItemQuery(roadmapId.ToString(), itemId), ct),
            $"Roadmap item with ID '{itemId}' not found.",
            UpdateRoadmapItemRequest.FromDto,
            (request, ct) => _dispatcher.Send((request with { RoadmapId = roadmapId, ItemId = itemId }).ToUpdateRoadmapItemCommand(), ct),
            cancellationToken);

    [HttpPut("{roadmapId}/items/{itemId}/dates")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Roadmaps)]
    [EndpointSummary("Update the date(s) on a roadmap item of type: Activity, Timebox, Milestone.")]
    [EndpointDescription(
        "Changes only the item's dates, leaving its other fields as they are. The body's `$type` must be its first property and match the item's type: \"activity\" or \"timebox\" with `start` and `end`, \"milestone\" with `date`. Both ids are UUIDs, given in the path and again as `roadmapId` and `itemId` in the body. Only a manager of an active roadmap can change it.")]
    [McpTool("Roadmaps_UpdateItemDates", "Update roadmap item dates")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateItemDates(Guid roadmapId, Guid itemId, [FromBody] UpdateRoadmapItemDatesRequest request, CancellationToken cancellationToken)
    {
        if (roadmapId != request.RoadmapId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(roadmapId), nameof(request.RoadmapId), HttpContext));
        else if (itemId != request.ItemId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(itemId), nameof(request.ItemId), HttpContext));

        var command = request switch
        {
            UpdateRoadmapActivityDatesRequest activity => activity.ToUpdateRoadmapItemDatesCommand(),
            UpdateRoadmapTimeboxDatesRequest timebox => timebox.ToUpdateRoadmapItemDatesCommand(),
            UpdateRoadmapMilestoneDatesRequest milestone => milestone.ToUpdateRoadmapItemDatesCommand(),
            _ => throw new ArgumentException("Invalid roadmap item type", nameof(request))
        };

        var result = await _dispatcher.Send(command, cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{roadmapId}/items/{itemId}/placement")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Roadmaps)]
    [EndpointSummary("Update a roadmap activity's placement.")]
    [EndpointDescription(
        "Places an activity at `order` (1-based) among its siblings under `parentId`, the UUID of another activity, or at the top level when `parentId` is null. With the activity's current parent this reorders it; with a different one it moves it there, renumbering both sets of siblings. Activities only — a milestone or timebox moves to another parent through Roadmaps_UpdateItem or Roadmaps_PatchItem. Both ids are UUIDs, given in the path and again as `roadmapId` and `itemId` in the body. Only a manager of an active roadmap can change it.")]
    [McpTool("Roadmaps_UpdateActivityPlacement", "Place a roadmap activity")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateActivityPlacement(Guid roadmapId, Guid itemId, [FromBody] UpdateRoadmapActivityPlacementRequest request, CancellationToken cancellationToken)
    {
        if (roadmapId != request.RoadmapId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(roadmapId), nameof(request.RoadmapId), HttpContext));
        else if (itemId != request.ItemId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(itemId), nameof(request.ItemId), HttpContext));

        var result = await _dispatcher.Send(request.ToUpdateRoadmapActivityPlacementCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{roadmapId}/items/{itemId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Roadmaps)]
    [EndpointSummary("Delete a roadmap item.")]
    [EndpointDescription(
        "Deleting an activity also deletes everything nested under it, at every level; read the roadmap with Roadmaps_GetItems first to see what goes with it. Both ids are UUIDs. Only a manager of an active roadmap can delete items.")]
    [McpTool("Roadmaps_DeleteItem", "Delete roadmap item")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteItem(Guid roadmapId, Guid itemId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteRoadmapItemCommand(roadmapId, itemId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #endregion Roadmap Items

    [HttpGet("visibility-options")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Roadmaps)]
    [EndpointSummary("Get a list of all roadmap visibility options.")]
    [McpTool("Roadmaps_GetVisibilityOptions", "List roadmap visibility options")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<VisibilityDto>>> GetVisibilityOptions(CancellationToken cancellationToken)
    {
        var items = await _dispatcher.Send(new GetVisibilitiesQuery(), cancellationToken);
        return Ok(items.OrderBy(c => c.Order));
    }

    [HttpGet("states")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Roadmaps)]
    [EndpointSummary("Get a list of all roadmap states.")]
    [McpTool("Roadmaps_GetStateOptions", "List roadmap states")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RoadmapStateDto>>> GetStateOptions(CancellationToken cancellationToken)
    {
        var items = await _dispatcher.Send(new GetRoadmapStatesQuery(), cancellationToken);
        return Ok(items.OrderBy(s => s.Order));
    }
}
