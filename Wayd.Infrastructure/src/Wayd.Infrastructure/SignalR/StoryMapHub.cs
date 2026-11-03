using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.FeatureManagement;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Infrastructure.Auth.Permissions;
using Wayd.Planning.Application.StoryMaps;
using Wayd.Planning.Domain.Models.StoryMaps;

namespace Wayd.Infrastructure.SignalR;

/// <summary>
/// Real-time hub for a Story Map. Clients viewing a map join its group to receive change broadcasts
/// (see <see cref="Wayd.Planning.Application.StoryMaps.Interfaces.IStoryMapNotifier"/>) and to see
/// who else is present on the map.
/// </summary>
[Authorize]
public class StoryMapHub : PresenceHub
{
    /// <summary>The permission <see cref="JoinMap"/> requires, re-checked when a member's access changes.</summary>
    public static readonly string JoinPermission =
        ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.StoryMaps);

    /// <summary>
    /// The permission that admits its holder to any map, private ones included, re-checked when a member's access
    /// changes.
    /// </summary>
    public static readonly string AdministerPermission = StoryMapAccess.AdministratorPermission;

    private readonly IFeatureManager _featureManager;
    private readonly IStoryMapAccess _access;

    public StoryMapHub(IFeatureManager featureManager, IStoryMapAccess access)
    {
        _featureManager = featureManager;
        _access = access;
    }

    /// <summary>
    /// Joins the caller to a map's broadcast group.
    /// </summary>
    /// <remarks>
    /// Change broadcasts carry full goal, step, and task payloads, so joining a group is equivalent
    /// to reading the map — it requires the same View permission the REST endpoints demand, applied
    /// here by the policy on <see cref="MustHavePermissionAttribute"/>. The feature flag is checked
    /// in code rather than with <c>[FeatureGate]</c>, which is an MVC filter and does not run for
    /// hub methods. A private map admits only the people the map does, so the map is asked too; a
    /// refusal reads as not found, as the REST read does.
    /// </remarks>
    [MustHavePermission(ApplicationAction.View, ApplicationResource.StoryMaps)]
    public async Task JoinMap(Guid storyMapId)
    {
        if (!await _featureManager.IsEnabledAsync(FeatureFlags.Names.StoryMaps))
            return;

        // A connection with no user id is refused by presence itself.
        var userId = Context.User?.GetUserId();
        if (!string.IsNullOrEmpty(userId)
            && !await _access.CanUserAccess(storyMapId, userId, Context.ConnectionAborted))
            throw new HubException(StoryMap.NotFoundError);

        // A connection tracks one map at a time, so joining another must release the first or its
        // presence entry is orphaned.
        await JoinPresenceGroup(storyMapId.ToString(), leaveOtherGroups: true);

        // Asked again once in the group: an access change between the two would otherwise find this connection
        // not yet present, evict nothing, and leave it receiving a map it no longer sees.
        if (!string.IsNullOrEmpty(userId)
            && !await _access.CanUserAccess(storyMapId, userId, Context.ConnectionAborted))
        {
            await LeavePresenceGroup(storyMapId.ToString());
            throw new HubException(StoryMap.NotFoundError);
        }
    }

    public Task LeaveMap(Guid storyMapId) =>
        LeavePresenceGroup(storyMapId.ToString());
}
