namespace Wayd.Planning.Application.StoryMaps.Interfaces;

/// <summary>
/// Broadcasts story map changes to everyone currently viewing the map, so collaborators see each
/// other's edits in real time. Implemented over SignalR; command handlers call the relevant method
/// after persisting a change.
///
/// Events are typed and granular — each carries enough for a client to patch its local copy in
/// place rather than refetch the whole map. As the structural commands (goals, steps, tasks, lanes,
/// personas) are built, their notify methods are added here alongside the handler that raises them.
/// </summary>
public interface IStoryMapNotifier
{
    /// <summary>
    /// The map's name, description, owner, or status changed. Carries the map id; clients refetch
    /// the lightweight map header. (Header-level fields only — structural changes have their own
    /// events.)
    /// </summary>
    Task NotifyMapUpdated(Guid storyMapId);

    /// <summary>
    /// The map was archived. Clients viewing it should reflect the archived state.
    /// </summary>
    Task NotifyMapArchived(Guid storyMapId);

    /// <summary>
    /// The map was deleted. Clients viewing it should be shown that it no longer exists.
    /// </summary>
    Task NotifyMapDeleted(Guid storyMapId);
}
