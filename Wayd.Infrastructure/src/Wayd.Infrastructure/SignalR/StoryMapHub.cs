using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Wayd.Infrastructure.SignalR;

/// <summary>
/// Real-time hub for a Story Map. Clients viewing a map join its group to receive change broadcasts
/// (see <see cref="Wayd.Planning.Application.StoryMaps.Interfaces.IStoryMapNotifier"/>) and to see
/// who else is present on the map. Presence is tracked per map, keyed by user, so a user open in
/// multiple tabs counts once.
/// </summary>
[Authorize]
public class StoryMapHub : Hub
{
    // storyMapId → (userId → PresenceEntry)
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, PresenceEntry>> _maps = new();

    // connectionId → MapConnection (for cleanup on disconnect)
    private static readonly ConcurrentDictionary<string, MapConnection> _connections = new();

    public async Task JoinMap(Guid storyMapId)
    {
        var mapKey = storyMapId.ToString();
        var connectionId = Context.ConnectionId;

        await Groups.AddToGroupAsync(connectionId, mapKey);

        var userId = Context.User?.GetUserId();
        // Display name: prefer the OIDC standard "name" claim, fall back to ClaimTypes.Name, then
        // email — matching the Planning Poker hub's whitespace-aware resolution.
        var name = FirstNonBlank(
            Context.User?.FindFirst("name")?.Value,
            Context.User?.FindFirst(ClaimTypes.Name)?.Value,
            Context.User?.GetEmail());

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(name))
            return;

        _connections[connectionId] = new MapConnection(mapKey, userId);

        var mapParticipants = _maps.GetOrAdd(mapKey, _ => new ConcurrentDictionary<string, PresenceEntry>());

        var isNewParticipant = false;
        mapParticipants.AddOrUpdate(
            userId,
            _ =>
            {
                isNewParticipant = true;
                var entry = new PresenceEntry(userId, name);
                lock (entry.ConnectionIds)
                {
                    entry.ConnectionIds.Add(connectionId);
                }
                return entry;
            },
            (_, existing) =>
            {
                lock (existing.ConnectionIds)
                {
                    if (existing.ConnectionIds.Count == 0)
                        isNewParticipant = true;
                    existing.ConnectionIds.Add(connectionId);
                }
                return existing;
            });

        // Send the current participant list to the caller.
        var participants = mapParticipants.Values
            .Select(e => new { Id = e.UserId, e.Name })
            .ToArray();
        await Clients.Caller.SendAsync("ParticipantList", participants);

        // Broadcast to others if this is a new participant.
        if (isNewParticipant)
        {
            await Clients.OthersInGroup(mapKey)
                .SendAsync("ParticipantJoined", new { Id = userId, Name = name });
        }
    }

    public async Task LeaveMap(Guid storyMapId)
    {
        var mapKey = storyMapId.ToString();
        var connectionId = Context.ConnectionId;

        await Groups.RemoveFromGroupAsync(connectionId, mapKey);
        await RemoveConnection(connectionId, mapKey);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connections.TryRemove(Context.ConnectionId, out var mapConnection))
        {
            await RemoveConnection(Context.ConnectionId, mapConnection.MapId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task RemoveConnection(string connectionId, string mapKey)
    {
        _connections.TryRemove(connectionId, out _);

        if (!_maps.TryGetValue(mapKey, out var mapParticipants))
            return;

        string? removedUserId = null;

        foreach (var (userId, entry) in mapParticipants)
        {
            bool shouldRemove;
            lock (entry.ConnectionIds)
            {
                if (!entry.ConnectionIds.Remove(connectionId))
                    continue;

                shouldRemove = entry.ConnectionIds.Count == 0;
            }

            if (shouldRemove)
            {
                mapParticipants.TryRemove(userId, out _);
                removedUserId = userId;
            }

            break;
        }

        if (removedUserId is not null)
        {
            await Clients.Group(mapKey)
                .SendAsync("ParticipantLeft", new { Id = removedUserId });
        }
    }

    private static string? FirstNonBlank(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v)) return v;
        }
        return null;
    }

    private record PresenceEntry(string UserId, string Name)
    {
        public HashSet<string> ConnectionIds { get; } = [];
    }

    private record MapConnection(string MapId, string UserId);
}
