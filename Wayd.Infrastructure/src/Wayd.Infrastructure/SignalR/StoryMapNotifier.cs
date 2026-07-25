using Microsoft.AspNetCore.SignalR;
using Wayd.Planning.Application.StoryMaps.Dtos;
using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Infrastructure.SignalR;

/// <summary>
/// Broadcasts story map changes to the map's SignalR group. Each map is its own group, keyed by the
/// map id, so a broadcast reaches exactly the clients viewing that map.
/// </summary>
internal sealed class StoryMapNotifier(IHubContext<StoryMapHub> hubContext) : IStoryMapNotifier
{
    private readonly IHubContext<StoryMapHub> _hubContext = hubContext;

    private IClientProxy Group(Guid storyMapId) => _hubContext.Clients.Group(storyMapId.ToString());

    #region Map

    public Task NotifyMapUpdated(Guid storyMapId) =>
        Group(storyMapId).SendAsync("MapUpdated", storyMapId);

    public Task NotifyMapArchived(Guid storyMapId) =>
        Group(storyMapId).SendAsync("MapArchived", storyMapId);

    public Task NotifyMapDeleted(Guid storyMapId) =>
        Group(storyMapId).SendAsync("MapDeleted", storyMapId);

    #endregion Map

    #region Goals

    public Task NotifyGoalAdded(Guid storyMapId, StoryMapGoalDto goal) =>
        Group(storyMapId).SendAsync("GoalAdded", storyMapId, goal);

    public Task NotifyGoalRenamed(Guid storyMapId, Guid goalId, string name) =>
        Group(storyMapId).SendAsync("GoalRenamed", storyMapId, goalId, name);

    public Task NotifyGoalReordered(Guid storyMapId, Guid goalId, int sortOrder) =>
        Group(storyMapId).SendAsync("GoalReordered", storyMapId, goalId, sortOrder);

    public Task NotifyGoalDeleted(Guid storyMapId, Guid goalId) =>
        Group(storyMapId).SendAsync("GoalDeleted", storyMapId, goalId);

    #endregion Goals

    #region Steps

    public Task NotifyStepAdded(Guid storyMapId, StoryMapStepDto step) =>
        Group(storyMapId).SendAsync("StepAdded", storyMapId, step);

    public Task NotifyStepRenamed(Guid storyMapId, Guid stepId, string name) =>
        Group(storyMapId).SendAsync("StepRenamed", storyMapId, stepId, name);

    public Task NotifyStepReordered(Guid storyMapId, Guid stepId, int sortOrder) =>
        Group(storyMapId).SendAsync("StepReordered", storyMapId, stepId, sortOrder);

    public Task NotifyStepMoved(Guid storyMapId, Guid stepId, Guid targetGoalId, int sortOrder) =>
        Group(storyMapId).SendAsync("StepMoved", storyMapId, stepId, targetGoalId, sortOrder);

    public Task NotifyStepDeleted(Guid storyMapId, Guid stepId) =>
        Group(storyMapId).SendAsync("StepDeleted", storyMapId, stepId);

    #endregion Steps

    #region Tasks

    public Task NotifyTaskAdded(Guid storyMapId, StoryMapTaskDto task) =>
        Group(storyMapId).SendAsync("TaskAdded", storyMapId, task);

    public Task NotifyTaskUpdated(Guid storyMapId, StoryMapTaskDto task) =>
        Group(storyMapId).SendAsync("TaskUpdated", storyMapId, task);

    public Task NotifyTaskMoved(Guid storyMapId, Guid taskId, Guid targetStepId, Guid targetSwimLaneId, int sortOrder) =>
        Group(storyMapId).SendAsync("TaskMoved", storyMapId, taskId, targetStepId, targetSwimLaneId, sortOrder);

    public Task NotifyTaskDeleted(Guid storyMapId, Guid taskId) =>
        Group(storyMapId).SendAsync("TaskDeleted", storyMapId, taskId);

    public Task NotifyTaskPersonasChanged(Guid storyMapId, Guid taskId, IReadOnlyList<Guid> personaIds) =>
        Group(storyMapId).SendAsync("TaskPersonasChanged", storyMapId, taskId, personaIds);

    #endregion Tasks

    #region Checklist

    public Task NotifyTaskChecklistChanged(Guid storyMapId, StoryMapTaskDto task) =>
        Group(storyMapId).SendAsync("TaskChecklistChanged", storyMapId, task);

    public Task NotifyChecklistItemPromoted(Guid storyMapId, StoryMapTaskDto newTask, StoryMapTaskDto sourceTask) =>
        Group(storyMapId).SendAsync("ChecklistItemPromoted", storyMapId, newTask, sourceTask);

    #endregion Checklist

    #region Work item links

    public Task NotifyTaskWorkItemLinked(Guid storyMapId, Guid taskId, int workItemId) =>
        Group(storyMapId).SendAsync("TaskWorkItemLinked", storyMapId, taskId, workItemId);

    public Task NotifyTaskWorkItemUnlinked(Guid storyMapId, Guid taskId) =>
        Group(storyMapId).SendAsync("TaskWorkItemUnlinked", storyMapId, taskId);

    #endregion Work item links

    #region SwimLanes

    public Task NotifySwimLaneAdded(Guid storyMapId, StoryMapSwimLaneDto lane) =>
        Group(storyMapId).SendAsync("SwimLaneAdded", storyMapId, lane);

    public Task NotifySwimLaneRenamed(Guid storyMapId, Guid swimLaneId, string name) =>
        Group(storyMapId).SendAsync("SwimLaneRenamed", storyMapId, swimLaneId, name);

    public Task NotifySwimLaneDatesChanged(Guid storyMapId, StoryMapSwimLaneDto lane) =>
        Group(storyMapId).SendAsync("SwimLaneDatesChanged", storyMapId, lane);

    public Task NotifySwimLaneReordered(Guid storyMapId, Guid swimLaneId, int sortOrder) =>
        Group(storyMapId).SendAsync("SwimLaneReordered", storyMapId, swimLaneId, sortOrder);

    public Task NotifySwimLaneRemoved(Guid storyMapId, Guid swimLaneId, Guid defaultSwimLaneId, int movedTaskCount) =>
        Group(storyMapId).SendAsync("SwimLaneRemoved", storyMapId, swimLaneId, defaultSwimLaneId, movedTaskCount);

    #endregion SwimLanes

    #region Personas

    public Task NotifyPersonaAdded(Guid storyMapId, StoryMapPersonaDto persona) =>
        Group(storyMapId).SendAsync("PersonaAdded", storyMapId, persona);

    public Task NotifyPersonaUpdated(Guid storyMapId, StoryMapPersonaDto persona) =>
        Group(storyMapId).SendAsync("PersonaUpdated", storyMapId, persona);

    public Task NotifyPersonaDeleted(Guid storyMapId, Guid personaId, int untaggedNodeCount) =>
        Group(storyMapId).SendAsync("PersonaDeleted", storyMapId, personaId, untaggedNodeCount);

    public Task NotifyPersonaReordered(Guid storyMapId, Guid personaId, int order) =>
        Group(storyMapId).SendAsync("PersonaReordered", storyMapId, personaId, order);

    public Task NotifyGoalPersonasChanged(Guid storyMapId, Guid goalId, IReadOnlyList<Guid> personaIds) =>
        Group(storyMapId).SendAsync("GoalPersonasChanged", storyMapId, goalId, personaIds);

    public Task NotifyStepPersonasChanged(Guid storyMapId, Guid stepId, IReadOnlyList<Guid> personaIds) =>
        Group(storyMapId).SendAsync("StepPersonasChanged", storyMapId, stepId, personaIds);

    #endregion Personas
}
