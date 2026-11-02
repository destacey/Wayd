using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.FeatureManagement;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Infrastructure.Auth.Permissions;
using Wayd.Planning.Application.PokerSessions;
using Wayd.Planning.Domain.Models.PlanningPoker;

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

    /// <summary>
    /// The permission that admits its holder to any session, private ones included, re-checked when a member's
    /// access changes.
    /// </summary>
    public static readonly string AdministerPermission = PokerSessionAccess.AdministratorPermission;

    private readonly IFeatureManager _featureManager;
    private readonly IPokerSessionAccess _access;

    public PlanningPokerHub(IFeatureManager featureManager, IPokerSessionAccess access)
    {
        _featureManager = featureManager;
        _access = access;
    }

    /// <summary>
    /// Joins the caller to a session's broadcast group.
    /// </summary>
    /// <remarks>
    /// Broadcasts carry round labels, descriptions and revealed votes, so joining a group is equivalent
    /// to reading the session — it requires the same View permission the REST endpoints demand, applied
    /// here by the policy on <see cref="MustHavePermissionAttribute"/>. The feature flag is checked in
    /// code rather than with <c>[FeatureGate]</c>, which is an MVC filter and does not run for hub methods.
    /// A private session admits only the people the session does, so the session is asked too; a refusal reads
    /// as not found, as the REST read does.
    /// </remarks>
    [MustHavePermission(ApplicationAction.View, ApplicationResource.PokerSessions)]
    public async Task JoinSession(Guid sessionId)
    {
        if (!await _featureManager.IsEnabledAsync(FeatureFlags.Names.PlanningPoker))
            return;

        // A connection with no user id is refused by presence itself.
        var userId = Context.User?.GetUserId();
        if (!string.IsNullOrEmpty(userId)
            && !await _access.CanUserAccess(sessionId, userId, Context.ConnectionAborted))
            throw new HubException(PokerSession.NotFoundError);

        await JoinPresenceGroup(sessionId.ToString());
    }

    public Task LeaveSession(Guid sessionId) =>
        LeavePresenceGroup(sessionId.ToString());
}
