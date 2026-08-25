using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Wayd.Infrastructure.SignalR;

/// <summary>
/// A hub that tracks who is present in a group, keyed by user so one person open in several tabs
/// counts once.
/// </summary>
/// <remarks>
/// Presence was written twice — once for planning poker, once for story maps — and the two copies
/// had to be edited in lockstep for every change to the participant shape. What actually differed
/// between them was authorization and a little ordering, so those are the extension points:
/// derived hubs decide who may join and what a group key means, and inherit the bookkeeping.
/// <para>
/// Presence is in-memory and per-instance. It is a live view of who is connected here, not a
/// record — a scaled-out deployment needs a backplane for it to be complete.
/// </para>
/// </remarks>
public abstract class PresenceHub : Hub
{
    // Qualified by hub type: the dictionaries are static and shared by every derived hub, so an
    // unqualified key would let a poker session and a story map with the same Guid collide, and
    // would let one hub's disconnect evict the other's presence.
    // qualifiedKey → (userId → PresenceEntry)
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, PresenceEntry>> _groups = new();

    // connectionId → qualifiedKey (for cleanup on disconnect)
    private static readonly ConcurrentDictionary<string, string> _connections = new();

    private string Qualify(string groupKey) => $"{GetType().Name}:{groupKey}";

    // SignalR groups are named with the raw key, so broadcasts strip the qualifier back off.
    private static string Unqualify(string presenceKey) =>
        presenceKey[(presenceKey.IndexOf(':') + 1)..];

    /// <summary>
    /// Adds the caller to a group and announces their presence.
    /// </summary>
    /// <remarks>
    /// Callers are expected to have authorized the join already — this does not check, because what
    /// counts as permission differs per hub (a permission claim, a feature flag, or both).
    /// </remarks>
    /// <param name="groupKey">Identifies the group; opaque to presence.</param>
    /// <param name="leaveOtherGroups">
    /// Moves the connection out of any group it is already in. Set where one connection may only be
    /// in one group at a time; without it a stale entry is left behind on the previous group.
    /// </param>
    protected async Task JoinPresenceGroup(
        string groupKey,
        bool leaveOtherGroups = false)
    {
        var connectionId = Context.ConnectionId;
        var presenceKey = Qualify(groupKey);

        if (leaveOtherGroups
            && _connections.TryGetValue(connectionId, out var existingKey)
            && existingKey != presenceKey)
        {
            await Groups.RemoveFromGroupAsync(connectionId, Unqualify(existingKey));
            await RemovePresence(connectionId, existingKey);
        }

        var userId = Context.User?.GetUserId();
        // Fall back to email: GetComposedName returns null when there is no first-name claim.
        var name = FirstNonBlank(
            Context.User.GetComposedName(),
            Context.User?.GetEmail());
        // Nullable: an account need not be linked to an employee. Clients use it to open the
        // person's employee record, and omit that affordance when it is absent.
        var employeeId = Context.User?.GetEmployeeId();

        // Refused the group, not merely the roster: group membership is what delivers a record's
        // content, and a participant nobody else can see is its own problem in a live session.
        // Reachable only for an authenticated account with no name and no email.
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(name))
            return;

        await Groups.AddToGroupAsync(connectionId, groupKey);

        _connections[connectionId] = presenceKey;

        var participants = _groups.GetOrAdd(presenceKey, _ => new ConcurrentDictionary<string, PresenceEntry>());

        var isNewParticipant = false;
        participants.AddOrUpdate(
            userId,
            _ =>
            {
                isNewParticipant = true;
                var entry = new PresenceEntry(userId, name, employeeId);
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
                    // Count zero means the entry is being reused after its last connection went
                    // away but before it was removed, so this is a fresh arrival.
                    if (existing.ConnectionIds.Count == 0)
                        isNewParticipant = true;
                    existing.ConnectionIds.Add(connectionId);
                }
                return existing;
            });

        await Clients.Caller.SendAsync(
            "ParticipantList",
            participants.Values
                .Select(e => new { Id = e.UserId, e.Name, e.EmployeeId })
                .ToArray());

        if (isNewParticipant)
        {
            await Clients.OthersInGroup(groupKey).SendAsync(
                "ParticipantJoined",
                new { Id = userId, Name = name, EmployeeId = employeeId });
        }
    }

    /// <summary>Removes the caller from a group and announces their departure.</summary>
    protected async Task LeavePresenceGroup(string groupKey)
    {
        var connectionId = Context.ConnectionId;

        await Groups.RemoveFromGroupAsync(connectionId, groupKey);
        await RemovePresence(connectionId, Qualify(groupKey));
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_connections.TryRemove(Context.ConnectionId, out var presenceKey))
        {
            await RemovePresence(Context.ConnectionId, presenceKey);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task RemovePresence(string connectionId, string presenceKey)
    {
        _connections.TryRemove(connectionId, out _);

        if (!_groups.TryGetValue(presenceKey, out var participants))
            return;

        string? removedUserId = null;

        foreach (var (userId, entry) in participants)
        {
            bool shouldRemove;
            lock (entry.ConnectionIds)
            {
                if (!entry.ConnectionIds.Remove(connectionId))
                    continue;

                // Only when the last tab closes — a user open twice stays present.
                shouldRemove = entry.ConnectionIds.Count == 0;
            }

            if (shouldRemove)
            {
                participants.TryRemove(userId, out _);
                removedUserId = userId;
            }

            break;
        }

        if (removedUserId is not null)
        {
            await Clients.Group(Unqualify(presenceKey))
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

    private record PresenceEntry(string UserId, string Name, string? EmployeeId)
    {
        public HashSet<string> ConnectionIds { get; } = [];
    }
}
