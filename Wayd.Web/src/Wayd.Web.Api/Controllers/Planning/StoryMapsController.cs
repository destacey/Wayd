using Microsoft.FeatureManagement.Mvc;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Planning.Application.StoryMaps.Commands;
using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Queries;
using Wayd.Web.Api.Extensions;
using Wayd.Web.Api.Models.Planning.StoryMaps;

namespace Wayd.Web.Api.Controllers.Planning;

[Route("api/planning/story-maps")]
[ApiVersionNeutral]
[ApiController]
[FeatureGate(FeatureFlags.Names.StoryMaps)]
[McpTools(McpToolset.Planning)]
public class StoryMapsController(IDispatcher dispatcher) : ControllerBase
{
    private readonly IDispatcher _dispatcher = dispatcher;

    [HttpGet]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Get a list of story maps (id, key, name, description, status, owner).", "")]
    [McpTool("StoryMaps_GetStoryMaps", "List story maps")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<StoryMapListDto>>> GetList(CancellationToken cancellationToken, [FromQuery] bool includeArchived = false)
    {
        var maps = await _dispatcher.Send(new GetStoryMapsQuery(includeArchived), cancellationToken);
        return Ok(maps);
    }

    [HttpGet("{idOrKey}")]
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StoryMaps)]
    [OpenApiOperation(
        "Get a story map in full: goals, each with ordered steps and tasks (including checklists, persona tags, and linked work item IDs), plus the map's swim lanes and personas.",
        "")]
    [McpTool("StoryMaps_GetStoryMap", "Get story map")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StoryMapDetailsDto>> Get(string idOrKey, CancellationToken cancellationToken)
    {
        var map = await _dispatcher.Send(new GetStoryMapQuery(idOrKey), cancellationToken);
        return map is not null
            ? Ok(map)
            : NotFound();
    }

    [HttpPost]
    [MustHavePermission(ApplicationAction.Create, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Create a story map.", "Returns the new map's ID and key.")]
    [McpTool("StoryMaps_CreateStoryMap", "Create a story map", Destructive = false)]
    [ApiConventionMethod(typeof(WaydApiConventions), nameof(WaydApiConventions.CreateReturn201IdAndKey))]
    public async Task<ActionResult<ObjectIdAndKey>> Create([FromBody] CreateStoryMapRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCreateStoryMapCommand(), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { idOrKey = result.Value.Id.ToString() }, result.Value)
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Update a story map's name and description.", "")]
    [McpTool("StoryMaps_UpdateStoryMap", "Update a story map")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateStoryMapRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToUpdateStoryMapCommand(id), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/owner")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Change the owner of a story map.", "")]
    [McpTool("StoryMaps_ChangeStoryMapOwner", "Change story map owner")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ChangeOwner(Guid id, [FromBody] ChangeStoryMapOwnerRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToChangeStoryMapOwnerCommand(id), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{id}/archive")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Archive a story map.", "")]
    [McpTool("StoryMaps_ArchiveStoryMap", "Archive a story map")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new ArchiveStoryMapCommand(id), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{id}")]
    [MustHavePermission(ApplicationAction.Delete, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Delete a story map and everything on it.", "This is permanent — prefer StoryMaps_ArchiveStoryMap unless deletion is explicitly intended.")]
    [McpTool("StoryMaps_DeleteStoryMap", "Delete a story map")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteStoryMapCommand(id), cancellationToken);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    // ---------------------------------------------------------------------------------------------
    // Goals
    // ---------------------------------------------------------------------------------------------

    [HttpPost("{storyMapId}/goals")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Add a goal to a story map.", "The goal is created with one step already in it.")]
    [McpTool("StoryMaps_AddGoal", "Add a story map goal", Destructive = false)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StoryMapGoalDto>> AddGoal(Guid storyMapId, [FromBody] AddGoalRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(storyMapId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{storyMapId}/goals/{goalId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Rename a goal.", "")]
    [McpTool("StoryMaps_RenameGoal", "Rename a story map goal")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RenameGoal(Guid storyMapId, Guid goalId, [FromBody] RenameGoalRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(storyMapId, goalId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{storyMapId}/goals/{goalId}/order")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Reorder a goal within the map.", "")]
    [McpTool("StoryMaps_ReorderGoal", "Reorder a story map goal")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ReorderGoal(Guid storyMapId, Guid goalId, [FromBody] ReorderGoalRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(storyMapId, goalId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{storyMapId}/goals/{goalId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Delete a goal, along with its steps and their tasks.", "")]
    [McpTool("StoryMaps_DeleteGoal", "Delete a story map goal")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteGoal(Guid storyMapId, Guid goalId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteGoalCommand(storyMapId, goalId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    // ---------------------------------------------------------------------------------------------
    // Steps
    // ---------------------------------------------------------------------------------------------

    [HttpPost("{storyMapId}/steps")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Add a step to a goal.", "")]
    [McpTool("StoryMaps_AddStep", "Add a story map step", Destructive = false)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StoryMapStepDto>> AddStep(Guid storyMapId, [FromBody] AddStepRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(storyMapId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{storyMapId}/steps/{stepId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Rename a step.", "")]
    [McpTool("StoryMaps_RenameStep", "Rename a story map step")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RenameStep(Guid storyMapId, Guid stepId, [FromBody] RenameStepRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(storyMapId, stepId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{storyMapId}/steps/{stepId}/order")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Reorder a step within its goal.", "")]
    [McpTool("StoryMaps_ReorderStep", "Reorder a story map step")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ReorderStep(Guid storyMapId, Guid stepId, [FromBody] ReorderStepRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(storyMapId, stepId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{storyMapId}/steps/{stepId}/move")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Move a step into a different goal.", "")]
    [McpTool("StoryMaps_MoveStep", "Move a step to another goal")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> MoveStep(Guid storyMapId, Guid stepId, [FromBody] MoveStepRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(storyMapId, stepId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{storyMapId}/steps/{stepId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Delete a step and its tasks.", "")]
    [McpTool("StoryMaps_DeleteStep", "Delete a story map step")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteStep(Guid storyMapId, Guid stepId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteStepCommand(storyMapId, stepId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    // ---------------------------------------------------------------------------------------------
    // Tasks
    // ---------------------------------------------------------------------------------------------

    [HttpPost("{storyMapId}/tasks")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Add a task (story card) to a step.", "Without a swimLaneId, it lands in the default swim lane.")]
    [McpTool("StoryMaps_AddTask", "Add a story map task", Destructive = false)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StoryMapTaskDto>> AddTask(Guid storyMapId, [FromBody] AddTaskRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(storyMapId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{storyMapId}/tasks/{taskId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Update a task's title and description.", "")]
    [McpTool("StoryMaps_UpdateTask", "Update a story map task")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdateTask(Guid storyMapId, Guid taskId, [FromBody] UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(storyMapId, taskId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{storyMapId}/tasks/{taskId}/title")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Rename a task.", "Updates only the title, leaving the description untouched. Prefer this over the combined update when editing one field, so a concurrent edit to the other is not reverted.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RenameTask(Guid storyMapId, Guid taskId, [FromBody] RenameTaskRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(storyMapId, taskId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{storyMapId}/tasks/{taskId}/description")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Set a task's description.", "Updates only the description, leaving the title untouched. Prefer this over the combined update when editing one field, so a concurrent edit to the other is not reverted.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SetTaskDescription(Guid storyMapId, Guid taskId, [FromBody] SetTaskDescriptionRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(storyMapId, taskId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{storyMapId}/tasks/{taskId}/move")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Move a task to a step and swim lane.", "")]
    [McpTool("StoryMaps_MoveTask", "Move a story map task")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> MoveTask(Guid storyMapId, Guid taskId, [FromBody] MoveTaskRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(storyMapId, taskId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{storyMapId}/tasks/{taskId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Delete a task.", "")]
    [McpTool("StoryMaps_DeleteTask", "Delete a story map task")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> DeleteTask(Guid storyMapId, Guid taskId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeleteTaskCommand(storyMapId, taskId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{storyMapId}/tasks/{taskId}/personas")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Set the personas tagged on a task.", "Replaces the full set — pass every persona ID that should remain tagged.")]
    [McpTool("StoryMaps_SetTaskPersonas", "Set a task's personas")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SetTaskPersonas(Guid storyMapId, Guid taskId, [FromBody] SetTaskPersonasRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(storyMapId, taskId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    // ---------------------------------------------------------------------------------------------
    // Checklist
    // ---------------------------------------------------------------------------------------------

    [HttpPost("{storyMapId}/tasks/{taskId}/checklist")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Add a checklist item to a task.", "Returns the updated task.")]
    [McpTool("StoryMaps_AddChecklistItem", "Add a checklist item", Destructive = false)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StoryMapTaskDto>> AddChecklistItem(Guid storyMapId, Guid taskId, [FromBody] AddChecklistItemRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(storyMapId, taskId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{storyMapId}/tasks/{taskId}/checklist/{itemId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Rename a checklist item.", "")]
    [McpTool("StoryMaps_RenameChecklistItem", "Rename a checklist item")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RenameChecklistItem(Guid storyMapId, Guid taskId, Guid itemId, [FromBody] RenameChecklistItemRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(storyMapId, taskId, itemId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{storyMapId}/tasks/{taskId}/checklist/{itemId}/checked")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Check or uncheck a checklist item.", "")]
    [McpTool("StoryMaps_SetChecklistItemChecked", "Check or uncheck a checklist item")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SetChecklistItemChecked(Guid storyMapId, Guid taskId, Guid itemId, [FromBody] SetChecklistItemCheckedRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(storyMapId, taskId, itemId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{storyMapId}/tasks/{taskId}/checklist/{itemId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Remove a checklist item from a task.", "")]
    [McpTool("StoryMaps_RemoveChecklistItem", "Remove a checklist item")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RemoveChecklistItem(Guid storyMapId, Guid taskId, Guid itemId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RemoveChecklistItemCommand(storyMapId, taskId, itemId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPost("{storyMapId}/tasks/{taskId}/checklist/{itemId}/promote")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Promote a checklist item into its own task in the same step.", "Returns the new task.")]
    [McpTool("StoryMaps_PromoteChecklistItem", "Promote a checklist item to a task")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StoryMapTaskDto>> PromoteChecklistItem(Guid storyMapId, Guid taskId, Guid itemId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new PromoteChecklistItemCommand(storyMapId, taskId, itemId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    // ---------------------------------------------------------------------------------------------
    // Work item links
    // ---------------------------------------------------------------------------------------------

    [HttpPut("{storyMapId}/tasks/{taskId}/work-item-link")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Link a task to an existing work item. A work item can be linked to at most one task per map.", "")]
    [McpTool("StoryMaps_LinkWorkItem", "Link a story map task to a work item")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> LinkWorkItem(Guid storyMapId, Guid taskId, [FromBody] LinkWorkItemRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(storyMapId, taskId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{storyMapId}/tasks/{taskId}/work-item-link")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Unlink a task from its work item.", "")]
    [McpTool("StoryMaps_UnlinkWorkItem", "Unlink a story map task from its work item")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UnlinkWorkItem(Guid storyMapId, Guid taskId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new UnlinkWorkItemCommand(storyMapId, taskId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    // ---------------------------------------------------------------------------------------------
    // SwimLanes
    // ---------------------------------------------------------------------------------------------

    [HttpPost("{storyMapId}/swim-lanes")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Add a swim lane, appended below the existing ones.", "")]
    [McpTool("StoryMaps_AddSwimLane", "Add a swim lane", Destructive = false)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StoryMapSwimLaneDto>> AddSwimLane(Guid storyMapId, [FromBody] AddSwimLaneRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(storyMapId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{storyMapId}/swim-lanes/{swimLaneId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Rename a swim lane. The default swim lane cannot be renamed.", "")]
    [McpTool("StoryMaps_RenameSwimLane", "Rename a swim lane")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RenameSwimLane(Guid storyMapId, Guid swimLaneId, [FromBody] RenameSwimLaneRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(storyMapId, swimLaneId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{storyMapId}/swim-lanes/{swimLaneId}/dates")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Set a swim lane's descriptive start and end dates.", "Pass null to clear a date.")]
    [McpTool("StoryMaps_SetSwimLaneDates", "Set swim lane dates")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SetSwimLaneDates(Guid storyMapId, Guid swimLaneId, [FromBody] SetSwimLaneDatesRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(storyMapId, swimLaneId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{storyMapId}/swim-lanes/{swimLaneId}/order")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Reorder a swim lane. The default swim lane cannot be reordered.", "")]
    [McpTool("StoryMaps_ReorderSwimLane", "Reorder a swim lane")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ReorderSwimLane(Guid storyMapId, Guid swimLaneId, [FromBody] ReorderSwimLaneRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(storyMapId, swimLaneId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{storyMapId}/swim-lanes/{swimLaneId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Remove a swim lane.", "Its tasks return to the default swim lane; the response is the number of tasks moved.")]
    [McpTool("StoryMaps_RemoveSwimLane", "Remove a swim lane")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<int>> RemoveSwimLane(Guid storyMapId, Guid swimLaneId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new RemoveSwimLaneCommand(storyMapId, swimLaneId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    // ---------------------------------------------------------------------------------------------
    // Personas
    // ---------------------------------------------------------------------------------------------

    [HttpPost("{storyMapId}/personas")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Define a persona on the map.", "")]
    [McpTool("StoryMaps_AddPersona", "Add a persona", Destructive = false)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StoryMapPersonaDto>> AddPersona(Guid storyMapId, [FromBody] AddPersonaRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(storyMapId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{storyMapId}/personas/{personaId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Update a persona's name, description, and color.", "")]
    [McpTool("StoryMaps_UpdatePersona", "Update a persona")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdatePersona(Guid storyMapId, Guid personaId, [FromBody] UpdatePersonaRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(storyMapId, personaId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpDelete("{storyMapId}/personas/{personaId}")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Delete a persona and strip its tag from every goal, step, and task.", "The response is the number of nodes untagged.")]
    [McpTool("StoryMaps_DeletePersona", "Delete a persona")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<int>> DeletePersona(Guid storyMapId, Guid personaId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(new DeletePersonaCommand(storyMapId, personaId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{storyMapId}/personas/{personaId}/order")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Reorder a persona within the map's persona list.", "")]
    [McpTool("StoryMaps_ReorderPersona", "Reorder a persona")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ReorderPersona(Guid storyMapId, Guid personaId, [FromBody] ReorderPersonaRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(storyMapId, personaId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{storyMapId}/goals/{goalId}/personas")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Set the personas tagged on a goal.", "Replaces the full set — pass every persona ID that should remain tagged.")]
    [McpTool("StoryMaps_SetGoalPersonas", "Set a goal's personas")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SetGoalPersonas(Guid storyMapId, Guid goalId, [FromBody] SetGoalPersonasRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(storyMapId, goalId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }

    [HttpPut("{storyMapId}/steps/{stepId}/personas")]
    [MustHavePermission(ApplicationAction.Update, ApplicationResource.StoryMaps)]
    [OpenApiOperation("Set the personas tagged on a step.", "Replaces the full set — pass every persona ID that should remain tagged.")]
    [McpTool("StoryMaps_SetStepPersonas", "Set a step's personas")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SetStepPersonas(Guid storyMapId, Guid stepId, [FromBody] SetStepPersonasRequest request, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(request.ToCommand(storyMapId, stepId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.ToBadRequestObject(HttpContext));
    }
}
