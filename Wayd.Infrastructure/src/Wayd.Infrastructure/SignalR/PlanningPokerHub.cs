using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Wayd.Infrastructure.SignalR;

[Authorize]
public class PlanningPokerHub : Hub
{
    // sessionId → (userId → PresenceEntry)
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, PresenceEntry>> _sessions = new();

    // connectionId → SessionConnection (for cleanup on disconnect)
    private static readonly ConcurrentDictionary<string, SessionConnection> _connections = new();

    public async Task JoinSession(Guid sessionId)
    {
        var sessionKey = sessionId.ToString();
        var connectionId = Context.ConnectionId;

        await Groups.AddToGroupAsync(connectionId, sessionKey);

        var userId = Context.User?.GetUserId();
        // Display name: prefer the OIDC standard "name" claim (Entra tokens), fall back
        // to ClaimTypes.Name (Wayd-issued JWTs use the schemas.xmlsoap.org URI form for
        // first name), and finally to email. Use whitespace-aware fallback because the
        // Wayd JWT always emits ClaimTypes.Name (as user.FirstName ?? "") — a plain
        // ?? would latch onto the empty string and skip the email fallback for users
        // whose FirstName is unset.
        var name = FirstNonBlank(
            Context.User?.FindFirst("name")?.Value,
            Context.User?.FindFirst(ClaimTypes.Name)?.Value,
            Context.User?.GetEmail());

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(name))
            return;

        _connections[connectionId] = new SessionConnection(sessionKey, userId);

        var sessionParticipants = _sessions.GetOrAdd(sessionKey, _ => new ConcurrentDictionary<string, PresenceEntry>());

        var isNewParticipant = false;
        sessionParticipants.AddOrUpdate(
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

        // Send the current participant list to the caller
        var participants = sessionParticipants.Values
            .Select(e => new { Id = e.UserId, e.Name })
            .ToArray();
        await Clients.Caller.SendAsync("ParticipantList", participants);

        // Broadcast to others if this is a new participant
        if (isNewParticipant)
        {
            await Clients.OthersInGroup(sessionKey)
                .SendAsync("ParticipantJoined", new { Id = userId, Name = name });
        }
    }

    public async Task LeaveSession(Guid sessionId)
    {
        var sessionKey = sessionId.ToString();
        var connectionId = Context.ConnectionId;

        await Groups.RemoveFromGroupAsync(connectionId, sessionKey);
        await RemoveConnection(connectionId, sessionKey);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connections.TryRemove(Context.ConnectionId, out var sessionConnection))
        {
            await RemoveConnection(Context.ConnectionId, sessionConnection.SessionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task RemoveConnection(string connectionId, string sessionKey)
    {
        _connections.TryRemove(connectionId, out _);

        if (!_sessions.TryGetValue(sessionKey, out var sessionParticipants))
            return;

        string? removedUserId = null;

        foreach (var (userId, entry) in sessionParticipants)
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
                sessionParticipants.TryRemove(userId, out _);
                removedUserId = userId;
            }

            break;
        }

        if (removedUserId is not null)
        {
            await Clients.Group(sessionKey)
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

    private record SessionConnection(string SessionId, string UserId);
}
