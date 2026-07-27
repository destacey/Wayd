using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.FeatureManagement;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Infrastructure.Auth.Permissions;

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

    private readonly IFeatureManager _featureManager;

    public StoryMapHub(IFeatureManager featureManager) => _featureManager = featureManager;

    /// <summary>
    /// Joins the caller to a map's broadcast group.
    /// </summary>
    /// <remarks>
    /// Change broadcasts carry full goal, step, and task payloads, so joining a group is equivalent
    /// to reading the map — it requires the same View permission the REST endpoints demand, applied
    /// here by the policy on <see cref="MustHavePermissionAttribute"/>. The feature flag is checked
    /// in code rather than with <c>[FeatureGate]</c>, which is an MVC filter and does not run for
    /// hub methods.
    /// </remarks>
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StoryMaps)]
    public async Task JoinMap(Guid storyMapId)
    {
        if (!await _featureManager.IsEnabledAsync(FeatureFlags.Names.StoryMaps))
            return;

        var mapKey = storyMapId.ToString();
        var connectionId = Context.ConnectionId;

        var userId = Context.User?.GetUserId();
        // Display name: compose the name from the first-name (ClaimTypes.Name) and surname
        // (ClaimTypes.Surname) claims — both are emitted by Entra and the Wayd JWT. FullName already
        // returns the first name alone when the surname is blank; fall back to email if neither is
        // present.
        var name = FirstNonBlank(
            FullName(Context.User),
            Context.User?.GetEmail());

        // Before joining the group: a connection added and then abandoned here would keep receiving
        // broadcasts while being invisible to presence.
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(name))
            return;

        // One connection tracks one map, so leave the previous one or its presence entry is orphaned.
        if (_connections.TryGetValue(connectionId, out var existingConnection)
            && existingConnection.MapId != mapKey)
        {
            await Groups.RemoveFromGroupAsync(connectionId, existingConnection.MapId);
            await RemoveConnection(connectionId, existingConnection.MapId);
        }

        await Groups.AddToGroupAsync(connectionId, mapKey);

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

    // Composes "First Last" from the first-name and surname claims. Returns null if the first name
    // is missing (the caller then falls back to first name alone or email); a lone surname is
    // ignored so we never render a name that looks like it is missing its start.
    private static string? FullName(ClaimsPrincipal? user)
    {
        var firstName = user?.FindFirst(ClaimTypes.Name)?.Value;
        if (string.IsNullOrWhiteSpace(firstName))
            return null;

        var surname = user?.FindFirst(ClaimTypes.Surname)?.Value;
        return string.IsNullOrWhiteSpace(surname)
            ? firstName.Trim()
            : $"{firstName.Trim()} {surname.Trim()}";
    }

    private record PresenceEntry(string UserId, string Name)
    {
        public HashSet<string> ConnectionIds { get; } = [];
    }

    private record MapConnection(string MapId, string UserId);
}
