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
[McpTools(McpToolset.Planning)]
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
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> CreateItem(Guid roadmapId, [FromBody] CreateRoadmapItemRequest request, CancellationToken cancellationToken)
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
    [EndpointDescription("Applies a JSON Patch document to update specific fields of an Activity, Timebox, or Milestone.")]
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
            (request, ct) => _dispatcher.Send(request.ToUpdateRoadmapItemCommand(), ct),
            cancellationToken);

    [HttpPut("{roadmapId}/items/{itemId}/dates")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Roadmaps)]
    [EndpointSummary("Update the date(s) on a roadmap item of type: Activity, Timebox, Milestone.")]
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
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RoadmapStateDto>>> GetStateOptions(CancellationToken cancellationToken)
    {
        var items = await _dispatcher.Send(new GetRoadmapStatesQuery(), cancellationToken);
        return Ok(items.OrderBy(s => s.Order));
    }
}
