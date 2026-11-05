using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Wayd.Planning.Application.PokerSessions;
using Wayd.Planning.Application.StoryMaps;

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
        new HashSet<string>([
            PlanningPokerHub.JoinPermission,
            PlanningPokerHub.AdministerPermission,
            StoryMapHub.JoinPermission,
            StoryMapHub.AdministerPermission]);

    /// <summary>
    /// Evicts each user's connections from every poker session and story map that no longer admits them, by
    /// permission, visibility or invitation.
    /// </summary>
    Task RecheckUsers(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken);

    /// <summary>Evicts everyone connected to a poker session who it no longer admits, after its access changes.</summary>
    Task RecheckPokerSession(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Evicts everyone connected to a story map who it no longer admits, after its access changes.</summary>
    Task RecheckStoryMap(Guid storyMapId, CancellationToken cancellationToken);

    /// <summary><see cref="RecheckUsers"/> for everyone holding the role.</summary>
    Task RecheckRoleMembers(string roleId, CancellationToken cancellationToken);

    /// <summary>Evicts every connection the user has, whatever they hold.</summary>
    Task EvictUser(string userId);
}

internal sealed class RealtimeAccessRevoker(
    IHubContext<PlanningPokerHub> pokerHub,
    IHubContext<StoryMapHub> storyMapHub,
    IUserService userService,
    IPokerSessionAccess pokerSessionAccess,
    IStoryMapAccess storyMapAccess,
    ILogger<RealtimeAccessRevoker> logger) : IRealtimeAccessRevoker
{
    private readonly IHubContext<PlanningPokerHub> _pokerHub = pokerHub;
    private readonly IHubContext<StoryMapHub> _storyMapHub = storyMapHub;
    private readonly IUserService _userService = userService;
    private readonly IPokerSessionAccess _pokerSessionAccess = pokerSessionAccess;
    private readonly IStoryMapAccess _storyMapAccess = storyMapAccess;
    private readonly ILogger<RealtimeAccessRevoker> _logger = logger;

    public async Task RecheckUsers(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken)
    {
        foreach (var userId in userIds)
        {
            await RecheckRecordsOf(_pokerHub, "poker session", _pokerSessionAccess.CanUserAccess, userId, cancellationToken);
            await RecheckRecordsOf(_storyMapHub, "story map", _storyMapAccess.CanUserAccess, userId, cancellationToken);
        }
    }

    public Task RecheckPokerSession(Guid sessionId, CancellationToken cancellationToken) =>
        RecheckRecord(_pokerHub, "poker session", _pokerSessionAccess.CanUserAccess, sessionId, cancellationToken);

    public Task RecheckStoryMap(Guid storyMapId, CancellationToken cancellationToken) =>
        RecheckRecord(_storyMapHub, "story map", _storyMapAccess.CanUserAccess, storyMapId, cancellationToken);

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

    /// <summary>
    /// Evicts everyone present in a record's group whom the record no longer admits.
    /// </summary>
    private async Task RecheckRecord<THub>(
        IHubContext<THub> hubContext,
        string recordName,
        Func<Guid, string, CancellationToken, Task<bool>> canUserAccess,
        Guid recordId,
        CancellationToken cancellationToken)
        where THub : PresenceHub
    {
        var groupKey = recordId.ToString();

        foreach (var (userId, connectionIds) in PresenceHub.PresentIn<THub>(groupKey))
        {
            if (await CanAccess(canUserAccess, recordId, userId, cancellationToken))
                continue;

            _logger.LogInformation("Evicting user {UserId} from {RecordName} {RecordId}: it no longer admits them.", userId, recordName, recordId);
            foreach (var connectionId in connectionIds)
                await PresenceHub.Evict(hubContext, connectionId, groupKey);
        }
    }

    /// <summary>
    /// A record admits by its visibility and invites as well as by permission, so each record the user is connected
    /// to is asked in full. Presence first: most users have nothing open, and that costs no lookup.
    /// </summary>
    private async Task RecheckRecordsOf<THub>(
        IHubContext<THub> hubContext,
        string recordName,
        Func<Guid, string, CancellationToken, Task<bool>> canUserAccess,
        string userId,
        CancellationToken cancellationToken)
        where THub : PresenceHub
    {
        foreach (var record in PresenceHub.ConnectionsOf<THub>(userId).GroupBy(c => c.GroupKey))
        {
            if (Guid.TryParse(record.Key, out var recordId)
                && await CanAccess(canUserAccess, recordId, userId, cancellationToken))
                continue;

            _logger.LogInformation("Evicting user {UserId} from {RecordName} {RecordId}: they can no longer see it.", userId, recordName, record.Key);
            foreach (var (connectionId, groupKey) in record)
                await PresenceHub.Evict(hubContext, connectionId, groupKey);
        }
    }

    private static async Task<bool> CanAccess(
        Func<Guid, string, CancellationToken, Task<bool>> canUserAccess,
        Guid recordId,
        string userId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await canUserAccess(recordId, userId, cancellationToken);
        }
        catch (NotFoundException)
        {
            // A deleted user holds nothing.
            return false;
        }
    }

    private static async Task EvictAll<THub>(IHubContext<THub> hubContext, string userId)
        where THub : PresenceHub
    {
        foreach (var (connectionId, groupKey) in PresenceHub.ConnectionsOf<THub>(userId))
            await PresenceHub.Evict(hubContext, connectionId, groupKey);
    }
}
