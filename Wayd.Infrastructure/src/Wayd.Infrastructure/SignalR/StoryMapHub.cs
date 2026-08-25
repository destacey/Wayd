using Microsoft.AspNetCore.Authorization;
using Microsoft.FeatureManagement;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Infrastructure.Auth.Permissions;

namespace Wayd.Infrastructure.SignalR;

/// <summary>
/// Real-time hub for a Story Map. Clients viewing a map join its group to receive change broadcasts
/// (see <see cref="Wayd.Planning.Application.StoryMaps.Interfaces.IStoryMapNotifier"/>) and to see
/// who else is present on the map.
/// </summary>
[Authorize]
public class StoryMapHub : PresenceHub
{
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

        // A connection tracks one map at a time, so joining another must release the first or its
        // presence entry is orphaned.
        await JoinPresenceGroup(storyMapId.ToString(), leaveOtherGroups: true);
    }

    public Task LeaveMap(Guid storyMapId) =>
        LeavePresenceGroup(storyMapId.ToString());
}
