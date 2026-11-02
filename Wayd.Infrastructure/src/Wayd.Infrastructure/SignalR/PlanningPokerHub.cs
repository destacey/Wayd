using Microsoft.AspNetCore.Authorization;
using Microsoft.FeatureManagement;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Infrastructure.Auth.Permissions;

namespace Wayd.Infrastructure.SignalR;

/// <summary>
/// Real-time hub for a planning poker session. Clients in a session join its group to receive
/// round and vote broadcasts and to see who else is present.
/// </summary>
[Authorize]
public class PlanningPokerHub : PresenceHub
{
    /// <summary>The permission <see cref="JoinSession"/> requires, re-checked when a member's access changes.</summary>
    public static readonly string JoinPermission =
        ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.PokerSessions);

    private readonly IFeatureManager _featureManager;

    public PlanningPokerHub(IFeatureManager featureManager) => _featureManager = featureManager;

    /// <summary>
    /// Joins the caller to a session's broadcast group.
    /// </summary>
    /// <remarks>
    /// Broadcasts carry round labels, descriptions and revealed votes, so joining a group is equivalent
    /// to reading the session — it requires the same View permission the REST endpoints demand, applied
    /// here by the policy on <see cref="MustHavePermissionAttribute"/>. The feature flag is checked in
    /// code rather than with <c>[FeatureGate]</c>, which is an MVC filter and does not run for hub methods.
    /// </remarks>
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
    public async Task JoinSession(Guid sessionId)
    {
        if (!await _featureManager.IsEnabledAsync(FeatureFlags.Names.PlanningPoker))
            return;

        await JoinPresenceGroup(sessionId.ToString());
    }

    public Task LeaveSession(Guid sessionId) =>
        LeavePresenceGroup(sessionId.ToString());
}
