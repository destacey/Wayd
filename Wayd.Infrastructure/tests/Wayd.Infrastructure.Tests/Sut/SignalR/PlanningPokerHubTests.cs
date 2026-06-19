using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Wayd.Infrastructure.SignalR;

namespace Wayd.Infrastructure.Tests.Sut.SignalR;

/// <summary>
/// Covers the display-name claim-resolution fallback in <see cref="PlanningPokerHub.JoinSession"/>.
/// The hub supports both Entra-shaped tokens (lowercase "name" claim) and Wayd-JWT-shaped tokens
/// (ClaimTypes.Name URI form), with email as a final fallback. Anonymous/userless connections
/// must not be registered as participants.
/// </summary>
public class PlanningPokerHubTests
{
    private const string TestUserId = "user-123";
    private const string EntraDisplayName = "Jane Smith";
    private const string WaydFirstName = "Jane";
    private const string TestEmail = "jane@example.com";

    private static (PlanningPokerHub Hub, Mock<ISingleClientProxy> CallerProxy) BuildHub(
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

        // IHubCallerClients.Caller returns ISingleClientProxy (a refinement of IClientProxy
        // that also supports invoke-with-result). The hub only uses SendAsync, but we must
        // satisfy the property's return type, hence the more specific mock.
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

        var hub = new PlanningPokerHub
        {
            Context = mockContext.Object,
            Groups = mockGroups.Object,
            Clients = mockClients.Object,
        };

        return (hub, callerProxy);
    }

    private static ClaimsPrincipal Principal(params (string Type, string Value)[] claims)
    {
        var identity = new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Verifies that JoinSession sent the participant list to the caller with the expected
    /// display name. SignalR's SendAsync is an extension method over SendCoreAsync, so the
    /// participant array arrives as args[1] (args[0] is the participants payload, args[1] would
    /// be cancellation — actually it's: SendCoreAsync(method, object?[] args, CT). The single
    /// "ParticipantList" arg is the participants array itself.
    /// </summary>
    private static void AssertParticipantBroadcastWithName(Mock<ISingleClientProxy> callerProxy, string expectedName)
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

    [Fact]
    public async Task JoinSession_WithEntraStyleNameClaim_UsesLowercaseNameClaim()
    {
        // Entra tokens carry the OIDC standard "name" claim with the full display name.
        var user = Principal(
            (ClaimTypes.NameIdentifier, TestUserId),
            ("name", EntraDisplayName),
            (ClaimTypes.Name, WaydFirstName)); // both present — "name" must win
        var (hub, callerProxy) = BuildHub(user);

        await hub.JoinSession(Guid.NewGuid());

        AssertParticipantBroadcastWithName(callerProxy, EntraDisplayName);
    }

    [Fact]
    public async Task JoinSession_WithWaydJwtNameClaim_FallsBackToClaimTypesName()
    {
        // Wayd-issued JWTs put the user's first name in the schemas.xmlsoap.org URI form,
        // not lowercase "name". Without the ClaimTypes.Name fallback in the hub, local-auth
        // users would be displayed by email.
        var user = Principal(
            (ClaimTypes.NameIdentifier, TestUserId),
            (ClaimTypes.Name, WaydFirstName),
            (ClaimTypes.Email, TestEmail));
        var (hub, callerProxy) = BuildHub(user);

        await hub.JoinSession(Guid.NewGuid());

        AssertParticipantBroadcastWithName(callerProxy, WaydFirstName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task JoinSession_WithBlankClaimTypesName_FallsBackToEmail(string blankFirstName)
    {
        // TokenService emits ClaimTypes.Name as (user.FirstName ?? string.Empty). For a
        // Wayd user with no first name set, that claim is present but blank. A plain
        // null-coalescing fallback would latch onto the empty string and skip email,
        // making the user undisplayable. The hub must treat blank as "absent".
        var user = Principal(
            (ClaimTypes.NameIdentifier, TestUserId),
            (ClaimTypes.Name, blankFirstName),
            (ClaimTypes.Email, TestEmail));
        var (hub, callerProxy) = BuildHub(user);

        await hub.JoinSession(Guid.NewGuid());

        AssertParticipantBroadcastWithName(callerProxy, TestEmail);
    }

    [Fact]
    public async Task JoinSession_WithOnlyEmailClaim_FallsBackToEmail()
    {
        // Last-resort fallback: a token that has neither "name" nor ClaimTypes.Name still
        // yields a usable display value rather than dropping the participant entirely.
        var user = Principal(
            (ClaimTypes.NameIdentifier, TestUserId),
            (ClaimTypes.Email, TestEmail));
        var (hub, callerProxy) = BuildHub(user);

        await hub.JoinSession(Guid.NewGuid());

        AssertParticipantBroadcastWithName(callerProxy, TestEmail);
    }

    [Fact]
    public async Task JoinSession_WithMissingUserId_DoesNotBroadcast()
    {
        // No NameIdentifier claim → no userId → silent return without registering presence.
        // Asserting the SendCoreAsync for "ParticipantList" was never called proves we
        // bailed before the participant-tracking and broadcast block.
        var user = Principal(
            ("name", EntraDisplayName),
            (ClaimTypes.Email, TestEmail));
        var (hub, callerProxy) = BuildHub(user);

        await hub.JoinSession(Guid.NewGuid());

        callerProxy.Verify(
            p => p.SendCoreAsync("ParticipantList", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task JoinSession_WithNoNameOrEmailClaim_DoesNotBroadcast()
    {
        // userId present but every display-name source is empty → bail before participant
        // tracking. The hub treats this as an unauthenticated-ish edge case.
        var user = Principal((ClaimTypes.NameIdentifier, TestUserId));
        var (hub, callerProxy) = BuildHub(user);

        await hub.JoinSession(Guid.NewGuid());

        callerProxy.Verify(
            p => p.SendCoreAsync("ParticipantList", It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
