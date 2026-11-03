using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.FeatureManagement;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Infrastructure.SignalR;
using Wayd.Planning.Application.PokerSessions;
using Wayd.Planning.Application.StoryMaps;

namespace Wayd.Infrastructure.Tests.Sut.SignalR;

/// <summary>
/// Covers <see cref="RealtimeAccessRevoker"/>: a connection already in a group is taken out of it once its user
/// no longer holds the hub's join permission, or the poker session or story map no longer admits them, and is left
/// alone while they do.
/// </summary>
/// <remarks>
/// Presence is static and shared by every hub instance, so each test joins a user id of its own.
/// </remarks>
public class RealtimeAccessRevokerTests
{
    private readonly Mock<IUserService> _userService = new();
    private readonly Mock<IGroupManager> _pokerGroups = new();
    private readonly Mock<IClientProxy> _pokerGroupProxy = new();
    private readonly Mock<ISingleClientProxy> _pokerClientProxy = new();
    private readonly Mock<IHubClients> _pokerClients = new();
    private readonly Mock<IHubContext<PlanningPokerHub>> _pokerHub = new();
    private readonly Mock<IHubContext<StoryMapHub>> _storyMapHub = new();
    private readonly Mock<IGroupManager> _storyMapGroups = new();
    private readonly Mock<IHubClients> _storyMapClients = new();
    private readonly Mock<IPokerSessionAccess> _pokerSessionAccess = new();
    private readonly Mock<IStoryMapAccess> _storyMapAccess = new();

    public RealtimeAccessRevokerTests()
    {
        _pokerClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_pokerGroupProxy.Object);
        _pokerClients.Setup(c => c.Client(It.IsAny<string>())).Returns(_pokerClientProxy.Object);
        _pokerHub.Setup(h => h.Groups).Returns(_pokerGroups.Object);
        _pokerHub.Setup(h => h.Clients).Returns(_pokerClients.Object);

        _storyMapClients.Setup(c => c.Group(It.IsAny<string>())).Returns(new Mock<IClientProxy>().Object);
        _storyMapClients.Setup(c => c.Client(It.IsAny<string>())).Returns(new Mock<ISingleClientProxy>().Object);
        _storyMapHub.Setup(h => h.Groups).Returns(_storyMapGroups.Object);
        _storyMapHub.Setup(h => h.Clients).Returns(_storyMapClients.Object);

        // A session admits whoever holds the join permission, unless a test says otherwise.
        _pokerSessionAccess
            .Setup(a => a.CanUserAccess(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, string userId, CancellationToken ct) =>
                _userService.Object.HasPermissionAsync(userId, PlanningPokerHub.JoinPermission, ct));
    }

    private RealtimeAccessRevoker CreateSut() =>
        new(_pokerHub.Object, _storyMapHub.Object, _userService.Object, _pokerSessionAccess.Object, _storyMapAccess.Object, NullLogger<RealtimeAccessRevoker>.Instance);

    private static async Task<string> JoinStoryMap(string userId, Guid storyMapId)
    {
        var connectionId = Guid.NewGuid().ToString();

        var context = new Mock<HubCallerContext>();
        context.Setup(c => c.ConnectionId).Returns(connectionId);
        context.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Name, "Jane")], "TestAuth")));

        var callerClients = new Mock<IHubCallerClients>();
        callerClients.Setup(c => c.Caller).Returns(new Mock<ISingleClientProxy>().Object);
        callerClients.Setup(c => c.OthersInGroup(It.IsAny<string>())).Returns(new Mock<IClientProxy>().Object);

        var featureManager = new Mock<IFeatureManager>();
        featureManager.Setup(f => f.IsEnabledAsync(FeatureFlags.Names.StoryMaps)).ReturnsAsync(true);

        var access = new Mock<IStoryMapAccess>();
        access.Setup(a => a.CanUserAccess(storyMapId, userId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var hub = new StoryMapHub(featureManager.Object, access.Object)
        {
            Context = context.Object,
            Groups = new Mock<IGroupManager>().Object,
            Clients = callerClients.Object,
        };

        await hub.JoinMap(storyMapId);
        return connectionId;
    }

    private static async Task<string> JoinPokerSession(string userId, Guid sessionId)
    {
        var connectionId = Guid.NewGuid().ToString();

        var context = new Mock<HubCallerContext>();
        context.Setup(c => c.ConnectionId).Returns(connectionId);
        context.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Name, "Jane")], "TestAuth")));

        var callerClients = new Mock<IHubCallerClients>();
        callerClients.Setup(c => c.Caller).Returns(new Mock<ISingleClientProxy>().Object);
        callerClients.Setup(c => c.OthersInGroup(It.IsAny<string>())).Returns(new Mock<IClientProxy>().Object);

        var featureManager = new Mock<IFeatureManager>();
        featureManager.Setup(f => f.IsEnabledAsync(FeatureFlags.Names.PlanningPoker)).ReturnsAsync(true);

        var access = new Mock<IPokerSessionAccess>();
        access.Setup(a => a.CanUserAccess(sessionId, userId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var hub = new PlanningPokerHub(featureManager.Object, access.Object)
        {
            Context = context.Object,
            Groups = new Mock<IGroupManager>().Object,
            Clients = callerClients.Object,
        };

        await hub.JoinSession(sessionId);
        return connectionId;
    }

    private void SetPokerPermission(string userId, bool holds) =>
        _userService
            .Setup(s => s.HasPermissionAsync(userId, PlanningPokerHub.JoinPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(holds);

    private void VerifyEvicted(string connectionId, Guid sessionId, Times times)
    {
        _pokerGroups.Verify(
            g => g.RemoveFromGroupAsync(connectionId, sessionId.ToString(), It.IsAny<CancellationToken>()),
            times);
        _pokerClientProxy.Verify(
            p => p.SendCoreAsync("AccessRevoked", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            times);
    }

    [Fact]
    public async Task RecheckUsers_WhenTheUserLostThePermission_EvictsTheirConnection()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var sessionId = Guid.NewGuid();
        var connectionId = await JoinPokerSession(userId, sessionId);
        SetPokerPermission(userId, holds: false);

        // Act
        await CreateSut().RecheckUsers([userId], TestContext.Current.CancellationToken);

        // Assert
        VerifyEvicted(connectionId, sessionId, Times.Once());
        _pokerGroupProxy.Verify(
            p => p.SendCoreAsync("ParticipantLeft", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
        PresenceHub.ConnectionsOf<PlanningPokerHub>(userId).Should().BeEmpty();
    }

    [Fact]
    public async Task RecheckUsers_WhenTheUserStillHoldsThePermission_LeavesTheirConnection()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var sessionId = Guid.NewGuid();
        var connectionId = await JoinPokerSession(userId, sessionId);
        SetPokerPermission(userId, holds: true);

        // Act
        await CreateSut().RecheckUsers([userId], TestContext.Current.CancellationToken);

        // Assert
        VerifyEvicted(connectionId, sessionId, Times.Never());
        PresenceHub.ConnectionsOf<PlanningPokerHub>(userId).Should().ContainSingle();
    }

    [Fact]
    public async Task RecheckUsers_WhenTheUserHasNothingOpen_ReadsNoPermissions()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        // Act
        await CreateSut().RecheckUsers([userId], TestContext.Current.CancellationToken);

        // Assert
        _userService.Verify(
            s => s.HasPermissionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RecheckUsers_WhenTheUserNoLongerExists_EvictsTheirConnection()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var sessionId = Guid.NewGuid();
        var connectionId = await JoinPokerSession(userId, sessionId);
        _userService
            .Setup(s => s.HasPermissionAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("User Not Found."));

        // Act
        await CreateSut().RecheckUsers([userId], TestContext.Current.CancellationToken);

        // Assert
        VerifyEvicted(connectionId, sessionId, Times.Once());
    }

    [Fact]
    public async Task RecheckRoleMembers_EvictsTheMembersWhoLostThePermission()
    {
        // Arrange
        var roleId = Guid.NewGuid().ToString();
        var lostIt = Guid.NewGuid().ToString();
        var keptIt = Guid.NewGuid().ToString();
        var sessionId = Guid.NewGuid();
        var lostConnection = await JoinPokerSession(lostIt, sessionId);
        var keptConnection = await JoinPokerSession(keptIt, sessionId);
        SetPokerPermission(lostIt, holds: false);
        SetPokerPermission(keptIt, holds: true);
        _userService
            .Setup(s => s.GetUsersWithRole(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new UserDetailsDto { Id = lostIt }, new UserDetailsDto { Id = keptIt }]);

        // Act
        await CreateSut().RecheckRoleMembers(roleId, TestContext.Current.CancellationToken);

        // Assert
        _pokerGroups.Verify(
            g => g.RemoveFromGroupAsync(lostConnection, sessionId.ToString(), It.IsAny<CancellationToken>()),
            Times.Once);
        _pokerGroups.Verify(
            g => g.RemoveFromGroupAsync(keptConnection, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RecheckUsers_WhenAPrivateSessionNoLongerAdmitsThem_EvictsOnlyThatSession()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var closedSession = Guid.NewGuid();
        var openSession = Guid.NewGuid();
        var closedConnection = await JoinPokerSession(userId, closedSession);
        var openConnection = await JoinPokerSession(userId, openSession);
        _pokerSessionAccess.Setup(a => a.CanUserAccess(closedSession, userId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _pokerSessionAccess.Setup(a => a.CanUserAccess(openSession, userId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        await CreateSut().RecheckUsers([userId], TestContext.Current.CancellationToken);

        // Assert
        _pokerGroups.Verify(g => g.RemoveFromGroupAsync(closedConnection, closedSession.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        _pokerGroups.Verify(g => g.RemoveFromGroupAsync(openConnection, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecheckPokerSession_EvictsEveryoneTheSessionNoLongerAdmits()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var removed = Guid.NewGuid().ToString();
        var kept = Guid.NewGuid().ToString();
        var removedConnection = await JoinPokerSession(removed, sessionId);
        var keptConnection = await JoinPokerSession(kept, sessionId);
        _pokerSessionAccess.Setup(a => a.CanUserAccess(sessionId, removed, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _pokerSessionAccess.Setup(a => a.CanUserAccess(sessionId, kept, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        await CreateSut().RecheckPokerSession(sessionId, TestContext.Current.CancellationToken);

        // Assert
        VerifyEvicted(removedConnection, sessionId, Times.Once());
        _pokerGroups.Verify(g => g.RemoveFromGroupAsync(keptConnection, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        PresenceHub.PresentIn<PlanningPokerHub>(sessionId.ToString()).Select(p => p.UserId).Should().Equal(kept);
    }

    [Fact]
    public async Task RecheckStoryMap_EvictsEveryoneTheMapNoLongerAdmits()
    {
        // Arrange
        var mapId = Guid.NewGuid();
        var removed = Guid.NewGuid().ToString();
        var kept = Guid.NewGuid().ToString();
        var removedConnection = await JoinStoryMap(removed, mapId);
        var keptConnection = await JoinStoryMap(kept, mapId);
        _storyMapAccess.Setup(a => a.CanUserAccess(mapId, removed, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _storyMapAccess.Setup(a => a.CanUserAccess(mapId, kept, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        await CreateSut().RecheckStoryMap(mapId, TestContext.Current.CancellationToken);

        // Assert
        _storyMapGroups.Verify(g => g.RemoveFromGroupAsync(removedConnection, mapId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        _storyMapGroups.Verify(g => g.RemoveFromGroupAsync(keptConnection, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        PresenceHub.PresentIn<StoryMapHub>(mapId.ToString()).Select(p => p.UserId).Should().Equal(kept);
    }

    [Fact]
    public async Task RecheckUsers_WhenAStoryMapNoLongerAdmitsThem_EvictsTheirConnection()
    {
        // Arrange — losing the View permission, or a private map's invite, both read as the map no longer admitting them.
        var userId = Guid.NewGuid().ToString();
        var mapId = Guid.NewGuid();
        var connectionId = await JoinStoryMap(userId, mapId);
        _storyMapAccess.Setup(a => a.CanUserAccess(mapId, userId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        await CreateSut().RecheckUsers([userId], TestContext.Current.CancellationToken);

        // Assert
        _storyMapGroups.Verify(g => g.RemoveFromGroupAsync(connectionId, mapId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        PresenceHub.ConnectionsOf<StoryMapHub>(userId).Should().BeEmpty();
    }

    [Fact]
    public async Task RecheckUsers_WhenAStoryMapStillAdmitsThem_LeavesTheirConnection()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var mapId = Guid.NewGuid();
        var connectionId = await JoinStoryMap(userId, mapId);
        _storyMapAccess.Setup(a => a.CanUserAccess(mapId, userId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        await CreateSut().RecheckUsers([userId], TestContext.Current.CancellationToken);

        // Assert
        _storyMapGroups.Verify(g => g.RemoveFromGroupAsync(connectionId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        PresenceHub.ConnectionsOf<StoryMapHub>(userId).Should().ContainSingle();
    }

    [Fact]
    public async Task EvictUser_EvictsTheirConnectionWithoutReadingPermissions()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var sessionId = Guid.NewGuid();
        var connectionId = await JoinPokerSession(userId, sessionId);

        // Act
        await CreateSut().EvictUser(userId);

        // Assert
        VerifyEvicted(connectionId, sessionId, Times.Once());
        _userService.Verify(
            s => s.HasPermissionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
