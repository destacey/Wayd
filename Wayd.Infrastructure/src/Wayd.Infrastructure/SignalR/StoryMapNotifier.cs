using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Infrastructure.SignalR;

/// <summary>
/// Broadcasts story map changes to the map's SignalR group. Each map is its own group, keyed by the
/// map id, so a broadcast reaches exactly the clients viewing that map.
/// </summary>
/// <remarks>
/// Handlers notify <b>after</b> committing, so a broadcast failure must never fail the command: the
/// change is already saved, and surfacing an error would have the client retry and duplicate it.
/// Every method routes through <see cref="Send"/>, which logs and swallows — a missed broadcast
/// costs a collaborator a refresh, which is strictly better than a phantom failure.
/// </remarks>
internal sealed class StoryMapNotifier(
    IHubContext<StoryMapHub> hubContext,
    ILogger<StoryMapNotifier> logger) : IStoryMapNotifier
{
    private readonly IHubContext<StoryMapHub> _hubContext = hubContext;
    private readonly ILogger<StoryMapNotifier> _logger = logger;

    /// <summary>Sends to the map's group, with the map id leading the payload as every event expects.</summary>
    private async Task Send(Guid storyMapId, string method, params object?[] args)
    {
        try
        {
            object?[] payload = [storyMapId, .. args];
            await _hubContext.Clients.Group(storyMapId.ToString()).SendCoreAsync(method, payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Story map broadcast {Method} failed for map {StoryMapId}; the change is saved and viewers will see it on their next refresh.",
                method,
                storyMapId);
        }
    }

    #region Map

    public Task NotifyMapUpdated(Guid storyMapId) =>
        Send(storyMapId, "MapUpdated");

    public Task NotifyMapArchived(Guid storyMapId) =>
        Send(storyMapId, "MapArchived");

    public Task NotifyMapDeleted(Guid storyMapId) =>
        Send(storyMapId, "MapDeleted");

    #endregion Map

    #region Goals

    public Task NotifyGoalAdded(Guid storyMapId, StoryMapGoalDto goal) =>
        Send(storyMapId, "GoalAdded", goal);

    public Task NotifyGoalRenamed(Guid storyMapId, Guid goalId, string name) =>
        Send(storyMapId, "GoalRenamed", goalId, name);

    public Task NotifyGoalReordered(Guid storyMapId, Guid goalId, int sortOrder) =>
        Send(storyMapId, "GoalReordered", goalId, sortOrder);

    public Task NotifyGoalDeleted(Guid storyMapId, Guid goalId) =>
        Send(storyMapId, "GoalDeleted", goalId);

    #endregion Goals

    #region Steps

    public Task NotifyStepAdded(Guid storyMapId, StoryMapStepDto step) =>
        Send(storyMapId, "StepAdded", step);

    public Task NotifyStepRenamed(Guid storyMapId, Guid stepId, string name) =>
        Send(storyMapId, "StepRenamed", stepId, name);

    public Task NotifyStepReordered(Guid storyMapId, Guid stepId, int sortOrder) =>
        Send(storyMapId, "StepReordered", stepId, sortOrder);

    public Task NotifyStepMoved(Guid storyMapId, Guid stepId, Guid targetGoalId, int sortOrder) =>
        Send(storyMapId, "StepMoved", stepId, targetGoalId, sortOrder);

    public Task NotifyStepDeleted(Guid storyMapId, Guid stepId) =>
        Send(storyMapId, "StepDeleted", stepId);

    #endregion Steps

    #region Tasks

    public Task NotifyTaskAdded(Guid storyMapId, StoryMapTaskDto task) =>
        Send(storyMapId, "TaskAdded", task);

    public Task NotifyTaskUpdated(Guid storyMapId, StoryMapTaskDto task) =>
        Send(storyMapId, "TaskUpdated", task);

    public Task NotifyTaskMoved(Guid storyMapId, Guid taskId, Guid targetStepId, Guid targetSwimLaneId, int sortOrder) =>
        Send(storyMapId, "TaskMoved", taskId, targetStepId, targetSwimLaneId, sortOrder);

    public Task NotifyTaskDeleted(Guid storyMapId, Guid taskId) =>
        Send(storyMapId, "TaskDeleted", taskId);

    public Task NotifyTaskPersonasChanged(Guid storyMapId, Guid taskId, IReadOnlyList<Guid> personaIds) =>
        Send(storyMapId, "TaskPersonasChanged", taskId, personaIds);

    #endregion Tasks

    #region Checklist

    public Task NotifyTaskChecklistChanged(Guid storyMapId, StoryMapTaskDto task) =>
        Send(storyMapId, "TaskChecklistChanged", task);

    public Task NotifyChecklistItemPromoted(Guid storyMapId, StoryMapTaskDto newTask, StoryMapTaskDto sourceTask) =>
        Send(storyMapId, "ChecklistItemPromoted", newTask, sourceTask);

    #endregion Checklist

    #region Work item links

    public Task NotifyTaskWorkItemLinked(Guid storyMapId, Guid taskId, int workItemId) =>
        Send(storyMapId, "TaskWorkItemLinked", taskId, workItemId);

    public Task NotifyTaskWorkItemUnlinked(Guid storyMapId, Guid taskId) =>
        Send(storyMapId, "TaskWorkItemUnlinked", taskId);

    #endregion Work item links

    #region SwimLanes

    public Task NotifySwimLaneAdded(Guid storyMapId, StoryMapSwimLaneDto lane) =>
        Send(storyMapId, "SwimLaneAdded", lane);

    public Task NotifySwimLaneRenamed(Guid storyMapId, Guid swimLaneId, string name) =>
        Send(storyMapId, "SwimLaneRenamed", swimLaneId, name);

    public Task NotifySwimLaneDatesChanged(Guid storyMapId, StoryMapSwimLaneDto lane) =>
        Send(storyMapId, "SwimLaneDatesChanged", lane);

    public Task NotifySwimLaneReordered(Guid storyMapId, Guid swimLaneId, int sortOrder) =>
        Send(storyMapId, "SwimLaneReordered", swimLaneId, sortOrder);

    public Task NotifySwimLaneRemoved(Guid storyMapId, Guid swimLaneId, Guid defaultSwimLaneId, int movedTaskCount) =>
        Send(storyMapId, "SwimLaneRemoved", swimLaneId, defaultSwimLaneId, movedTaskCount);

    #endregion SwimLanes

    #region Personas

    public Task NotifyPersonaAdded(Guid storyMapId, StoryMapPersonaDto persona) =>
        Send(storyMapId, "PersonaAdded", persona);

    public Task NotifyPersonaUpdated(Guid storyMapId, StoryMapPersonaDto persona) =>
        Send(storyMapId, "PersonaUpdated", persona);

    public Task NotifyPersonaDeleted(Guid storyMapId, Guid personaId, int untaggedNodeCount) =>
        Send(storyMapId, "PersonaDeleted", personaId, untaggedNodeCount);

    public Task NotifyPersonaReordered(Guid storyMapId, Guid personaId, int order) =>
        Send(storyMapId, "PersonaReordered", personaId, order);

    public Task NotifyGoalPersonasChanged(Guid storyMapId, Guid goalId, IReadOnlyList<Guid> personaIds) =>
        Send(storyMapId, "GoalPersonasChanged", goalId, personaIds);

    public Task NotifyStepPersonasChanged(Guid storyMapId, Guid stepId, IReadOnlyList<Guid> personaIds) =>
        Send(storyMapId, "StepPersonasChanged", stepId, personaIds);

    #endregion Personas
}
