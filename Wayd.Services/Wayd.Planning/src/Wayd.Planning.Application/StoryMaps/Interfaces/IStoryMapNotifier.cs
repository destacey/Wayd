using Wayd.Planning.Application.StoryMaps.Dtos;

namespace Wayd.Planning.Application.StoryMaps.Interfaces;

/// <summary>
/// Broadcasts story map changes to everyone currently viewing the map, so collaborators see each
/// other's edits in real time. Implemented over SignalR; command handlers call the relevant method
/// after persisting a change.
///
/// Events are typed and granular — additive/edit events carry the affected DTO so a client can patch
/// its local copy in place without refetching, while reorder/move/delete events carry the ids and
/// new positions needed to relocate or drop the affected node.
/// </summary>
public interface IStoryMapNotifier
{
    #region Map

    /// <summary>The map's header (name, description, owner) changed.</summary>
    Task NotifyMapUpdated(Guid storyMapId);

    /// <summary>The map was archived.</summary>
    Task NotifyMapArchived(Guid storyMapId);

    /// <summary>The map was deleted.</summary>
    Task NotifyMapDeleted(Guid storyMapId);

    #endregion Map

    #region Goals

    Task NotifyGoalAdded(Guid storyMapId, StoryMapGoalDto goal);
    Task NotifyGoalRenamed(Guid storyMapId, Guid goalId, string name);
    Task NotifyGoalReordered(Guid storyMapId, Guid goalId, int order);
    Task NotifyGoalDeleted(Guid storyMapId, Guid goalId);

    #endregion Goals

    #region Steps

    Task NotifyStepAdded(Guid storyMapId, StoryMapStepDto step);
    Task NotifyStepRenamed(Guid storyMapId, Guid stepId, string name);
    Task NotifyStepReordered(Guid storyMapId, Guid stepId, int order);
    Task NotifyStepMoved(Guid storyMapId, Guid stepId, Guid targetGoalId, int order);
    Task NotifyStepDeleted(Guid storyMapId, Guid stepId);

    #endregion Steps

    #region Tasks

    Task NotifyTaskAdded(Guid storyMapId, StoryMapTaskDto task);
    Task NotifyTaskUpdated(Guid storyMapId, StoryMapTaskDto task);
    Task NotifyTaskMoved(Guid storyMapId, Guid taskId, Guid targetStepId, Guid targetSwimLaneId, int order);
    Task NotifyTaskDeleted(Guid storyMapId, Guid taskId);
    Task NotifyTaskPersonasChanged(Guid storyMapId, Guid taskId, IReadOnlyList<Guid> personaIds);

    #endregion Tasks

    #region Checklist

    /// <summary>A checklist change — the full task DTO carries the updated checklist and counts.</summary>
    Task NotifyTaskChecklistChanged(Guid storyMapId, StoryMapTaskDto task);

    /// <summary>
    /// A checklist item was promoted to a task: the new task is added, and the source task's
    /// checklist changed. Both DTOs are carried so clients can apply the add and the edit together.
    /// </summary>
    Task NotifyChecklistItemPromoted(Guid storyMapId, StoryMapTaskDto newTask, StoryMapTaskDto sourceTask);

    #endregion Checklist

    #region Work item links

    Task NotifyTaskWorkItemLinked(Guid storyMapId, Guid taskId, int workItemId);
    Task NotifyTaskWorkItemUnlinked(Guid storyMapId, Guid taskId);

    #endregion Work item links

    #region SwimLanes

    Task NotifySwimLaneAdded(Guid storyMapId, StoryMapSwimLaneDto lane);
    Task NotifySwimLaneRenamed(Guid storyMapId, Guid swimLaneId, string name);
    Task NotifySwimLaneDatesChanged(Guid storyMapId, StoryMapSwimLaneDto lane);
    Task NotifySwimLaneReordered(Guid storyMapId, Guid swimLaneId, int order);

    /// <summary>
    /// A lane was removed; its tasks returned to the default lane. Clients should refetch the map
    /// to pick up the relocated tasks (a lane removal can move many tasks at once).
    /// </summary>
    Task NotifySwimLaneRemoved(Guid storyMapId, Guid swimLaneId, Guid defaultSwimLaneId, int movedTaskCount);

    #endregion SwimLanes

    #region Personas

    Task NotifyPersonaAdded(Guid storyMapId, StoryMapPersonaDto persona);
    Task NotifyPersonaUpdated(Guid storyMapId, StoryMapPersonaDto persona);

    /// <summary>
    /// A persona was deleted; its tag was stripped from every goal, step, and task. Clients should
    /// refetch the map to drop the tag wherever it appeared.
    /// </summary>
    Task NotifyPersonaDeleted(Guid storyMapId, Guid personaId, int untaggedNodeCount);

    Task NotifyPersonaReordered(Guid storyMapId, Guid personaId, int order);

    Task NotifyGoalPersonasChanged(Guid storyMapId, Guid goalId, IReadOnlyList<Guid> personaIds);
    Task NotifyStepPersonasChanged(Guid storyMapId, Guid stepId, IReadOnlyList<Guid> personaIds);

    #endregion Personas
}
