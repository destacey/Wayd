using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.FeatureManagement;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Infrastructure.SignalR;

namespace Wayd.Infrastructure.Tests.Sut.SignalR;

/// <summary>
/// Covers <see cref="StoryMapHub"/> presence: the display-name resolution in
/// <see cref="StoryMapHub.JoinMap"/> (first name + surname composed, first name alone when the
/// surname is blank, then email — a standalone "name" claim is deliberately not consulted), that
/// anonymous/userless connections are not registered, and that a single user open on multiple
/// connections is tracked (and broadcast) as one participant.
///
/// Also covers the guards on group membership: broadcasts carry full map content, so a connection
/// must not join a map's group when the feature is off or the caller's identity cannot be resolved.
/// </summary>
public class StoryMapHubTests
{
    private const string TestUserId = "user-123";
    private const string WaydFirstName = "Jane";
    private const string WaydLastName = "Smith";
    private const string TestEmail = "jane@example.com";

    private static (StoryMapHub Hub, Mock<ISingleClientProxy> CallerProxy, Mock<IClientProxy> OthersProxy, Mock<IGroupManager> Groups) BuildHubWithGroups(
        ClaimsPrincipal user,
        string? connectionId = null,
        bool featureEnabled = true)
    {
        connectionId ??= Guid.NewGuid().ToString();

        var mockContext = new Mock<HubCallerContext>();
        mockContext.Setup(c => c.ConnectionId).Returns(connectionId);
        mockContext.Setup(c => c.User).Returns(user);

        var mockGroups = new Mock<IGroupManager>();
        mockGroups
            .Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockGroups
            .Setup(g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var callerProxy = new Mock<ISingleClientProxy>();
        callerProxy
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var othersProxy = new Mock<IClientProxy>();
        othersProxy
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockClients = new Mock<IHubCallerClients>();
        mockClients.Setup(c => c.Caller).Returns(callerProxy.Object);
        mockClients.Setup(c => c.OthersInGroup(It.IsAny<string>())).Returns(othersProxy.Object);
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(othersProxy.Object);

        var featureManager = new Mock<IFeatureManager>();
        featureManager
            .Setup(f => f.IsEnabledAsync(FeatureFlags.Names.StoryMaps))
            .ReturnsAsync(featureEnabled);

        var hub = new StoryMapHub(featureManager.Object)
        {
            Context = mockContext.Object,
            Groups = mockGroups.Object,
            Clients = mockClients.Object,
        };

        return (hub, callerProxy, othersProxy, mockGroups);
    }

    private static (StoryMapHub Hub, Mock<ISingleClientProxy> CallerProxy, Mock<IClientProxy> OthersProxy) BuildHub(
        ClaimsPrincipal user,
        string? connectionId = null)
    {
        var (hub, callerProxy, othersProxy, _) = BuildHubWithGroups(user, connectionId);
        return (hub, callerProxy, othersProxy);
    }

    private static ClaimsPrincipal Principal(params (string Type, string Value)[] claims)
    {
        var identity = new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    private static void AssertParticipantListBroadcastWithName(Mock<ISingleClientProxy> callerProxy, string expectedName)
    {
        callerProxy.Verify(
            p => p.SendCoreAsync(
                "ParticipantList",
                It.Is<object?[]>(args => args.Length == 1 && ContainsParticipantWithName(args[0], expectedName)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static bool ContainsParticipantWithName(object? participantsArg, string expectedName)
    {
        if (participantsArg is not System.Collections.IEnumerable enumerable) return false;
        foreach (var p in enumerable)
        {
            if (p is null) continue;
            var nameProp = p.GetType().GetProperty("Name");
            if (nameProp?.GetValue(p) as string == expectedName) return true;
        }
        return false;
    }

    private static int ParticipantCount(object? participantsArg)
    {
        if (participantsArg is not System.Collections.IEnumerable enumerable) return 0;
        return enumerable.Cast<object?>().Count(x => x is not null);
    }

    [Fact]
    public async Task JoinMap_IgnoresNameClaim_ComposesFromFirstNameAndSurname()
    {
        // Arrange — the "name" claim is not consulted; the display name is always composed from the
        // first-name and surname claims (both emitted by Entra and the Wayd JWT).
        var user = Principal(
            (ClaimTypes.NameIdentifier, TestUserId),
            ("name", "Should Be Ignored"),
            (ClaimTypes.Name, WaydFirstName),
            (ClaimTypes.Surname, WaydLastName));

        var (hub, callerProxy, _) = BuildHub(user);

        // Act
        await hub.JoinMap(Guid.NewGuid());

        // Assert
        AssertParticipantListBroadcastWithName(callerProxy, $"{WaydFirstName} {WaydLastName}");
    }

    [Fact]
    public async Task JoinMap_WithFirstNameAndSurname_ComposesFullName()
    {
        // Arrange — no "name" claim, but first name + surname compose into the full display name.
        var user = Principal(
            (ClaimTypes.NameIdentifier, TestUserId),
            (ClaimTypes.Name, WaydFirstName),
            (ClaimTypes.Surname, WaydLastName),
            (ClaimTypes.Email, TestEmail));

        var (hub, callerProxy, _) = BuildHub(user);

        // Act
        await hub.JoinMap(Guid.NewGuid());

        // Assert
        AssertParticipantListBroadcastWithName(callerProxy, $"{WaydFirstName} {WaydLastName}");
    }

    [Fact]
    public async Task JoinMap_WithFirstNameOnly_FallsBackToFirstName()
    {
        // Arrange — a missing surname must not blank the name; the first name stands alone.
        var user = Principal(
            (ClaimTypes.NameIdentifier, TestUserId),
            (ClaimTypes.Name, WaydFirstName),
            (ClaimTypes.Email, TestEmail));

        var (hub, callerProxy, _) = BuildHub(user);

        // Act
        await hub.JoinMap(Guid.NewGuid());

        // Assert
        AssertParticipantListBroadcastWithName(callerProxy, WaydFirstName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task JoinMap_WithBlankClaimTypesName_FallsBackToEmail(string blankFirstName)
    {
        // Arrange — a blank ClaimTypes.Name must be treated as absent so email is used.
        var user = Principal(
            (ClaimTypes.NameIdentifier, TestUserId),
            (ClaimTypes.Name, blankFirstName),
            (ClaimTypes.Email, TestEmail));

        var (hub, callerProxy, _) = BuildHub(user);

        // Act
        await hub.JoinMap(Guid.NewGuid());

        // Assert
        AssertParticipantListBroadcastWithName(callerProxy, TestEmail);
    }

    [Fact]
    public async Task JoinMap_WithOnlyEmailClaim_FallsBackToEmail()
    {
        // Arrange
        var user = Principal(
            (ClaimTypes.NameIdentifier, TestUserId),
            (ClaimTypes.Email, TestEmail));

        var (hub, callerProxy, _) = BuildHub(user);

        // Act
        await hub.JoinMap(Guid.NewGuid());

        // Assert
        AssertParticipantListBroadcastWithName(callerProxy, TestEmail);
    }

    [Fact]
    public async Task JoinMap_WithMissingUserId_DoesNotBroadcast()
    {
        // Arrange — no NameIdentifier → no userId → silent return without registering presence.
        var user = Principal(
            (ClaimTypes.Name, WaydFirstName),
            (ClaimTypes.Surname, WaydLastName),
            (ClaimTypes.Email, TestEmail));

        var (hub, callerProxy, _) = BuildHub(user);

        // Act
        await hub.JoinMap(Guid.NewGuid());

        // Assert
        callerProxy.Verify(
            p => p.SendCoreAsync("ParticipantList", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task JoinMap_WithNoNameOrEmailClaim_DoesNotBroadcast()
    {
        // Arrange — userId present but every display-name source is empty.
        var user = Principal((ClaimTypes.NameIdentifier, TestUserId));

        var (hub, callerProxy, _) = BuildHub(user);

        // Act
        await hub.JoinMap(Guid.NewGuid());

        // Assert
        callerProxy.Verify(
            p => p.SendCoreAsync("ParticipantList", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task JoinMap_WithFeatureDisabled_DoesNotJoinGroup()
    {
        // Arrange — the hub is mapped regardless of the flag, so the flag has to be checked here.
        // [FeatureGate] is an MVC filter and never runs for a hub method.
        var user = Principal(
            (ClaimTypes.NameIdentifier, TestUserId),
            (ClaimTypes.Name, WaydFirstName),
            (ClaimTypes.Surname, WaydLastName));

        var (hub, callerProxy, _, groups) = BuildHubWithGroups(user, featureEnabled: false);

        // Act
        await hub.JoinMap(Guid.NewGuid());

        // Assert — group membership is what delivers map content, so it must not happen at all.
        groups.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        callerProxy.Verify(
            p => p.SendCoreAsync("ParticipantList", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task JoinMap_WithUnresolvableIdentity_DoesNotJoinGroup()
    {
        // Arrange — joining before the identity check would leave a connection receiving every
        // change broadcast while being invisible to presence.
        var user = Principal((ClaimTypes.NameIdentifier, TestUserId));

        var (hub, _, _, groups) = BuildHubWithGroups(user);

        // Act
        await hub.JoinMap(Guid.NewGuid());

        // Assert
        groups.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task JoinMap_WithResolvedIdentity_JoinsGroup()
    {
        // Arrange
        var user = Principal(
            (ClaimTypes.NameIdentifier, TestUserId),
            (ClaimTypes.Name, WaydFirstName),
            (ClaimTypes.Surname, WaydLastName));

        var mapId = Guid.NewGuid();
        var (hub, _, _, groups) = BuildHubWithGroups(user);

        // Act
        await hub.JoinMap(mapId);

        // Assert
        groups.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), mapId.ToString(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task JoinMap_OnASecondMap_LeavesTheFirstGroup()
    {
        // Arrange — one connection tracks one map. Without leaving the first, its presence entry is
        // orphaned and the participant never departs it.
        var user = Principal(
            (ClaimTypes.NameIdentifier, TestUserId),
            (ClaimTypes.Name, WaydFirstName),
            (ClaimTypes.Surname, WaydLastName));

        var firstMap = Guid.NewGuid();
        var secondMap = Guid.NewGuid();
        var connectionId = Guid.NewGuid().ToString();
        var (hub, _, _, groups) = BuildHubWithGroups(user, connectionId);

        // Act
        await hub.JoinMap(firstMap);
        await hub.JoinMap(secondMap);

        // Assert
        groups.Verify(
            g => g.RemoveFromGroupAsync(connectionId, firstMap.ToString(), It.IsAny<CancellationToken>()),
            Times.Once);
        groups.Verify(
            g => g.AddToGroupAsync(connectionId, secondMap.ToString(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task JoinMap_NewParticipant_BroadcastsJoinToOthers()
    {
        // Arrange
        var user = Principal(
            (ClaimTypes.NameIdentifier, TestUserId),
            (ClaimTypes.Name, WaydFirstName),
            (ClaimTypes.Surname, WaydLastName));

        var (hub, _, othersProxy) = BuildHub(user);

        // Act
        await hub.JoinMap(Guid.NewGuid());

        // Assert — a first-time participant is announced to the rest of the map's group.
        othersProxy.Verify(
            p => p.SendCoreAsync("ParticipantJoined", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task JoinMap_SameUserSecondConnection_TrackedAsSingleParticipantAndNotReannounced()
    {
        // Arrange — the same user joins the same map twice (e.g. two browser tabs). Presence is
        // keyed by user, so the participant list must contain the user once, and the join must not
        // be re-announced to others on the second connection.
        var mapId = Guid.NewGuid();
        var user = Principal(
            (ClaimTypes.NameIdentifier, TestUserId),
            (ClaimTypes.Name, WaydFirstName),
            (ClaimTypes.Surname, WaydLastName));

        // Static presence state is shared across hub instances, so two BuildHub calls with the same
        // user id simulate two connections for that user. Use a map id unique to this test to avoid
        // cross-test contamination of the shared dictionaries.
        var (hub1, _, _) = BuildHub(user, connectionId: "conn-1");
        var (hub2, caller2, others2) = BuildHub(user, connectionId: "conn-2");

        // Act
        await hub1.JoinMap(mapId);
        await hub2.JoinMap(mapId);

        // Assert — the second connection's participant list still shows exactly one participant.
        caller2.Verify(
            p => p.SendCoreAsync(
                "ParticipantList",
                It.Is<object?[]>(args => args.Length == 1 && ParticipantCount(args[0]) == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // And the second connection does not re-announce the already-present user.
        others2.Verify(
            p => p.SendCoreAsync("ParticipantJoined", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
