using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Wayd.Infrastructure.SignalR;

namespace Wayd.Infrastructure.Tests.Sut.SignalR;

/// <summary>
/// Covers <see cref="StoryMapHub"/> presence: the display-name claim-resolution fallback in
/// <see cref="StoryMapHub.JoinMap"/> (Entra "name" → composed first-name + surname → Wayd-JWT
/// ClaimTypes.Name → email, treating a blank name as absent), that anonymous/userless connections
/// are not registered, and that a single user open on multiple connections is tracked (and
/// broadcast) as one participant.
/// </summary>
public class StoryMapHubTests
{
    private const string TestUserId = "user-123";
    private const string WaydFirstName = "Jane";
    private const string WaydLastName = "Smith";
    private const string TestEmail = "jane@example.com";

    private static (StoryMapHub Hub, Mock<ISingleClientProxy> CallerProxy, Mock<IClientProxy> OthersProxy) BuildHub(
        ClaimsPrincipal user,
        string? connectionId = null)
    {
        connectionId ??= Guid.NewGuid().ToString();

        var mockContext = new Mock<HubCallerContext>();
        mockContext.Setup(c => c.ConnectionId).Returns(connectionId);
        mockContext.Setup(c => c.User).Returns(user);

        var mockGroups = new Mock<IGroupManager>();
        mockGroups
            .Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
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

        var hub = new StoryMapHub
        {
            Context = mockContext.Object,
            Groups = mockGroups.Object,
            Clients = mockClients.Object,
        };

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
