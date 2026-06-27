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
public class RoadmapsController : ControllerBase
{
    private readonly ILogger<RoadmapsController> _logger;
    private readonly ISender _sender;
    private readonly IValidator<UpdateRoadmapActivityRequest> _updateActivityValidator;
    private readonly IValidator<UpdateRoadmapMilestoneRequest> _updateMilestoneValidator;
    private readonly IValidator<UpdateRoadmapTimeboxRequest> _updateTimeboxValidator;

    public RoadmapsController(
        ILogger<RoadmapsController> logger,
        ISender sender,
        IValidator<UpdateRoadmapActivityRequest> updateActivityValidator,
        IValidator<UpdateRoadmapMilestoneRequest> updateMilestoneValidator,
        IValidator<UpdateRoadmapTimeboxRequest> updateTimeboxValidator)
    {
        _logger = logger;
        _sender = sender;
        _updateActivityValidator = updateActivityValidator;
        _updateMilestoneValidator = updateMilestoneValidator;
        _updateTimeboxValidator = updateTimeboxValidator;
    }

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Roadmaps)]
    [OpenApiOperation("Get a list of roadmaps.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RoadmapListDto>>> GetRoadmaps([FromQuery] int[]? state, CancellationToken cancellationToken)
    {
        RoadmapState[]? filter = null;
        if (state is { Length: > 0 })
        {
            var invalid = state.Where(s => !Enum.IsDefined(typeof(RoadmapState), s)).ToArray();
            if (invalid.Length > 0)
                return BadRequest(ProblemDetailsExtensions.ForBadRequest($"Invalid roadmap state value(s): {string.Join(", ", invalid)}.", HttpContext));

            filter = [.. state.Select(s => (RoadmapState)s)];
        }

        var roadmaps = await _sender.Send(new GetRoadmapsQuery(filter), cancellationToken);
        return Ok(roadmaps);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Roadmaps)]
    [OpenApiOperation("Get roadmap details.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoadmapDetailsDto>> GetRoadmap(string idOrKey, CancellationToken cancellationToken)
    {
        var roadmap = await _sender.Send(new GetRoadmapQuery(idOrKey), cancellationToken);

        return roadmap is not null
            ? Ok(roadmap)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Roadmaps)]
    [OpenApiOperation("Create a roadmap.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create([FromBody] CreateRoadmapRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(request.ToCreateRoadmapCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetRoadmap), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("copy")]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Roadmaps)]
    [OpenApiOperation("Copy an existing roadmap.", "")]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Copy([FromBody] CopyRoadmapRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(request.ToCopyRoadmapCommand(), cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetRoadmap), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Roadmaps)]
    [OpenApiOperation("Update a roadmap.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateRoadmapRequest request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _sender.Send(request.ToUpdateRoadmapCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/colors")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Roadmaps)]
    [OpenApiOperation("Update the configured colors for a roadmap.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateColors(Guid id, [FromBody] UpdateRoadmapColorsRequest request, CancellationToken cancellationToken)
    {
        if (id != request.RoadmapId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(HttpContext));

        var result = await _sender.Send(request.ToUpdateRoadmapColorsCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.Roadmaps)]
    [OpenApiOperation("Delete a roadmap.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteRoadmapCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/archive")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Roadmaps)]
    [OpenApiOperation("Archive a roadmap.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ArchiveRoadmapCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{id}/activate")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Roadmaps)]
    [OpenApiOperation("Activate a roadmap.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ActivateRoadmapCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #region Roadmap Items

    [HttpGet("{idOrKey}/items")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Roadmaps)]
    [OpenApiOperation("Get roadmap items", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RoadmapItemListDto>>> GetItems(string idOrKey, CancellationToken cancellationToken)
    {
        var items = await _sender.Send(new GetRoadmapItemsQuery(idOrKey), cancellationToken);
        return Ok(items);
    }

    [HttpGet("{idOrKey}/items/activities")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Roadmaps)]
    [OpenApiOperation("Get roadmap activities", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RoadmapActivityListDto>>> GetActivities(string idOrKey, CancellationToken cancellationToken)
    {
        var items = await _sender.Send(new GetRoadmapActivitiesQuery(idOrKey), cancellationToken);
        return Ok(items);
    }

    [HttpGet("{roadmapIdOrKey}/items/{itemId}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Roadmaps)]
    [OpenApiOperation("Get roadmap item details", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RoadmapItemDetailsDto>> GetItem(string roadmapIdOrKey, Guid itemId, CancellationToken cancellationToken)
    {
        var item = await _sender.Send(new GetRoadmapItemQuery(roadmapIdOrKey, itemId), cancellationToken);
        return Ok(item);
    }

    [HttpPost("{roadmapId}/items")]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.Roadmaps)]
    [OpenApiOperation("Create a roadmap item of type: Activity, Timebox, Milestone.", "")]
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

        var result = await _sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetItem), new { roadmapIdOrKey = roadmapId, itemId = result.Value.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{roadmapId}/items/{itemId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Roadmaps)]
    [OpenApiOperation("Update a roadmap item of type: Activity, Timebox, Milestone.", "")]
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

        var result = await _sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPatch("{roadmapId}/items/{itemId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Roadmaps)]
    [OpenApiOperation("Partially update a roadmap item using JSON Patch (RFC 6902).", "Applies a JSON Patch document to update specific fields of an Activity, Timebox, or Milestone.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> PatchRoadmapItem(
        Guid roadmapId,
        Guid itemId,
        [FromBody] JsonPatchDocument<UpdateRoadmapItemRequest> patchDocument,
        CancellationToken cancellationToken)
    {
        if (patchDocument == null)
            return BadRequest("Patch document cannot be null.");

        // Get the current item state
        var itemDto = await _sender.Send(new GetRoadmapItemQuery(roadmapId.ToString(), itemId), cancellationToken);
        if (itemDto is null)
            return NotFound($"Roadmap item with ID '{itemId}' not found.");

        // Convert DTO to the appropriate UpdateRoadmapItemRequest type
        UpdateRoadmapItemRequest updateRequest = itemDto switch
        {
            RoadmapActivityDetailsDto activity => UpdateRoadmapActivityRequest.FromDto(activity),
            RoadmapMilestoneDetailsDto milestone => UpdateRoadmapMilestoneRequest.FromDto(milestone),
            RoadmapTimeboxDetailsDto timebox => UpdateRoadmapTimeboxRequest.FromDto(timebox),
            _ => throw new ArgumentException("Invalid roadmap item type", nameof(itemDto))
        };

        // Apply the patch document to the update request
        patchDocument.ApplyTo(updateRequest, error =>
        {
            ModelState.AddModelError(error.AffectedObject.GetType().Name, error.ErrorMessage);
        });

        // Validate the patched request with FluentValidation
        var validationResult = updateRequest switch
        {
            UpdateRoadmapActivityRequest activity => await _updateActivityValidator.ValidateAsync(activity, cancellationToken),
            UpdateRoadmapMilestoneRequest milestone => await _updateMilestoneValidator.ValidateAsync(milestone, cancellationToken),
            UpdateRoadmapTimeboxRequest timebox => await _updateTimeboxValidator.ValidateAsync(timebox, cancellationToken),
            _ => throw new ArgumentException("Invalid roadmap item type", nameof(updateRequest))
        };

        if (!validationResult.IsValid)
        {
            foreach (var error in validationResult.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }
        }

        // Return all validation errors (patch + business rules) as 422
        if (!ModelState.IsValid)
            return ValidationProblem(
                modelStateDictionary: ModelState,
                statusCode: StatusCodes.Status422UnprocessableEntity);

        var command = updateRequest switch
        {
            UpdateRoadmapActivityRequest activity => activity.ToUpdateRoadmapItemCommand(),
            UpdateRoadmapTimeboxRequest timebox => timebox.ToUpdateRoadmapItemCommand(),
            UpdateRoadmapMilestoneRequest milestone => milestone.ToUpdateRoadmapItemCommand(),
            _ => throw new ArgumentException("Invalid roadmap item type", nameof(updateRequest))
        };

        var result = await _sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{roadmapId}/items/{itemId}/dates")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Roadmaps)]
    [OpenApiOperation("Update the date(s) on a roadmap item of type: Activity, Timebox, Milestone.", "")]
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

        var result = await _sender.Send(command, cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{roadmapId}/items/{itemId}/placement")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Roadmaps)]
    [OpenApiOperation("Update a roadmap activity's placement.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateActivityPlacement(Guid roadmapId, Guid itemId, [FromBody] UpdateRoadmapActivityPlacementRequest request, CancellationToken cancellationToken)
    {
        if (roadmapId != request.RoadmapId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(roadmapId), nameof(request.RoadmapId), HttpContext));
        else if (itemId != request.ItemId)
            return BadRequest(ProblemDetailsExtensions.ForRouteParamMismatch(nameof(itemId), nameof(request.ItemId), HttpContext));

        var result = await _sender.Send(request.ToUpdateRoadmapActivityPlacementCommand(), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{roadmapId}/items/{itemId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.Roadmaps)]
    [OpenApiOperation("Delete a roadmap item.", "")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteItem(Guid roadmapId, Guid itemId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteRoadmapItemCommand(roadmapId, itemId), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    #endregion Roadmap Items

    [HttpGet("visibility-options")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Roadmaps)]
    [OpenApiOperation("Get a list of all visibility.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<VisibilityDto>>> GetVisibilityOptions(CancellationToken cancellationToken)
    {
        var items = await _sender.Send(new GetVisibilitiesQuery(), cancellationToken);
        return Ok(items.OrderBy(c => c.Order));
    }

    [HttpGet("states")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.Roadmaps)]
    [OpenApiOperation("Get a list of all roadmap states.", "")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RoadmapStateDto>>> GetStateOptions(CancellationToken cancellationToken)
    {
        var items = await _sender.Send(new GetRoadmapStatesQuery(), cancellationToken);
        return Ok(items.OrderBy(s => s.Order));
    }
}
