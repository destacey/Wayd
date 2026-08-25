using Microsoft.AspNetCore.Authorization;

namespace Wayd.Infrastructure.SignalR;

/// <summary>
/// Real-time hub for a planning poker session. Clients in a session join its group to receive
/// round and vote broadcasts and to see who else is present.
/// </summary>
[Authorize]
public class PlanningPokerHub : PresenceHub
{
    public Task JoinSession(Guid sessionId) =>
        JoinPresenceGroup(sessionId.ToString());

    public Task LeaveSession(Guid sessionId) =>
        LeavePresenceGroup(sessionId.ToString());
}
