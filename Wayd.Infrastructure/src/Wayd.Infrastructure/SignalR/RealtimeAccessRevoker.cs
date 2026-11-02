using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Wayd.Infrastructure.SignalR;

/// <summary>
/// Takes people out of the real-time groups they may no longer read. A hub checks access only when a connection
/// joins, so without this a connection keeps receiving a record's broadcasts after its user loses access.
/// </summary>
/// <remarks>
/// Works from presence, which is per instance: on a scaled-out deployment it evicts only the connections this
/// instance holds. A reconnect always joins again through the hub's check.
/// </remarks>
public interface IRealtimeAccessRevoker
{
    /// <summary>Every permission some hub requires to join; a role losing one of these is worth a recheck.</summary>
    static IReadOnlySet<string> JoinPermissions { get; } =
        new HashSet<string>([PlanningPokerHub.JoinPermission, StoryMapHub.JoinPermission]);

    /// <summary>Evicts each user's connections from every hub whose join permission they no longer hold.</summary>
    Task RecheckUsers(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken);

    /// <summary><see cref="RecheckUsers"/> for everyone holding the role.</summary>
    Task RecheckRoleMembers(string roleId, CancellationToken cancellationToken);

    /// <summary>Evicts every connection the user has, whatever they hold.</summary>
    Task EvictUser(string userId);
}

internal sealed class RealtimeAccessRevoker(
    IHubContext<PlanningPokerHub> pokerHub,
    IHubContext<StoryMapHub> storyMapHub,
    IUserService userService,
    ILogger<RealtimeAccessRevoker> logger) : IRealtimeAccessRevoker
{
    private readonly IHubContext<PlanningPokerHub> _pokerHub = pokerHub;
    private readonly IHubContext<StoryMapHub> _storyMapHub = storyMapHub;
    private readonly IUserService _userService = userService;
    private readonly ILogger<RealtimeAccessRevoker> _logger = logger;

    public async Task RecheckUsers(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken)
    {
        foreach (var userId in userIds)
        {
            await Recheck(_pokerHub, PlanningPokerHub.JoinPermission, userId, cancellationToken);
            await Recheck(_storyMapHub, StoryMapHub.JoinPermission, userId, cancellationToken);
        }
    }

    public async Task RecheckRoleMembers(string roleId, CancellationToken cancellationToken)
    {
        var members = await _userService.GetUsersWithRole(roleId, cancellationToken);

        await RecheckUsers([.. members.Select(u => u.Id)], cancellationToken);
    }

    public async Task EvictUser(string userId)
    {
        await EvictAll(_pokerHub, userId);
        await EvictAll(_storyMapHub, userId);
    }

    private async Task Recheck<THub>(IHubContext<THub> hubContext, string permission, string userId, CancellationToken cancellationToken)
        where THub : PresenceHub
    {
        // Presence first: most users have nothing open, and that costs no permission read.
        var connections = PresenceHub.ConnectionsOf<THub>(userId);
        if (connections.Count == 0)
            return;

        if (await StillHolds(userId, permission, cancellationToken))
            return;

        _logger.LogInformation("Evicting user {UserId} from {Hub}: they no longer hold {Permission}.", userId, typeof(THub).Name, permission);
        foreach (var (connectionId, groupKey) in connections)
            await PresenceHub.Evict(hubContext, connectionId, groupKey);
    }

    private static async Task EvictAll<THub>(IHubContext<THub> hubContext, string userId)
        where THub : PresenceHub
    {
        foreach (var (connectionId, groupKey) in PresenceHub.ConnectionsOf<THub>(userId))
            await PresenceHub.Evict(hubContext, connectionId, groupKey);
    }

    private async Task<bool> StillHolds(string userId, string permission, CancellationToken cancellationToken)
    {
        try
        {
            return await _userService.HasPermissionAsync(userId, permission, cancellationToken);
        }
        catch (NotFoundException)
        {
            // A deleted user holds nothing.
            return false;
        }
    }
}
