using Microsoft.AspNetCore.SignalR;
using Wayd.Planning.Application.StoryMaps.Interfaces;

namespace Wayd.Infrastructure.SignalR;

/// <summary>
/// Broadcasts story map changes to the map's SignalR group. Each map is its own group, keyed by the
/// map id, so a broadcast reaches exactly the clients viewing that map.
/// </summary>
internal sealed class StoryMapNotifier(IHubContext<StoryMapHub> hubContext) : IStoryMapNotifier
{
    private readonly IHubContext<StoryMapHub> _hubContext = hubContext;

    public async Task NotifyMapUpdated(Guid storyMapId) =>
        await _hubContext.Clients.Group(storyMapId.ToString())
            .SendAsync("MapUpdated", storyMapId);

    public async Task NotifyMapArchived(Guid storyMapId) =>
        await _hubContext.Clients.Group(storyMapId.ToString())
            .SendAsync("MapArchived", storyMapId);

    public async Task NotifyMapDeleted(Guid storyMapId) =>
        await _hubContext.Clients.Group(storyMapId.ToString())
            .SendAsync("MapDeleted", storyMapId);
}
