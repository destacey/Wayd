using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Identity;
using Wayd.Common.Application.Interfaces;
using Wayd.Infrastructure.Auth.Local;
using Wayd.Infrastructure.Identity;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Auth.Local;

/// <summary>
/// Exercises refresh-token sessions against real SQL Server. The unit-level fake models these
/// semantics; this proves the actual implementation matches — salted hashing (so no equality
/// lookup exists), the filtered index, and Instant round-tripping.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class RefreshTokenStoreTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 3, 1, 12, 0, 0);

    private static readonly SessionContext TestSession = new(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36",
        "203.0.113.42");

    private readonly SqlServerDbContextFixture _fixture = fixture;

    private static IConfiguration Configuration => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "SecuritySettings:LocalJwt:RefreshTokenExpirationInDays", "7" },
        })
        .Build();

    private static RefreshTokenStore CreateStore(WaydDbContext context, Instant now)
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.Now).Returns(now);

        return new RefreshTokenStore(context, clock.Object, Configuration, NullLogger<RefreshTokenStore>.Instance);
    }

    private async Task<string> SeedUser(CancellationToken cancellationToken, string suffix = "1")
    {
        await using var context = _fixture.CreateContext();

        var user = new ApplicationUser
        {
            Id = $"user-{suffix}-{Guid.NewGuid():N}",
            UserName = $"user{suffix}@acme.example",
            NormalizedUserName = $"USER{suffix}@ACME.EXAMPLE",
            Email = $"user{suffix}@acme.example",
            NormalizedEmail = $"USER{suffix}@ACME.EXAMPLE",
            SecurityStamp = Guid.NewGuid().ToString(),
            IsActive = true,
            LoginProvider = LoginProviders.Wayd,
        };

        context.Set<ApplicationUser>().Add(user);
        await context.SaveChangesAsync(cancellationToken);

        return user.Id;
    }

    [Fact]
    public async Task Issue_ShouldStoreAHashRatherThanTheToken()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        // Act
        await using var context = _fixture.CreateContext();
        var token = await CreateStore(context, Now).Issue(userId, TestSession, ct);

        // Assert
        var stored = await context.UserRefreshTokens.SingleAsync(t => t.UserId == userId, ct);
        stored.TokenHash.Should().NotBe(token);
        stored.TokenHash.Should().NotContain(token, "the token must not be recoverable from the row");
        stored.ExpiresAt.Should().Be(Now.Plus(Duration.FromDays(7)));
        stored.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task Issue_ShouldOpenASeparateSessionPerCall()
    {
        // The point of the table: a second device does not displace the first.

        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var context = _fixture.CreateContext();
        var store = CreateStore(context, Now);

        // Act
        var first = await store.Issue(userId, TestSession, ct);
        var second = await store.Issue(userId, TestSession, ct);

        // Assert
        second.Should().NotBe(first);
        (await context.UserRefreshTokens.CountAsync(t => t.UserId == userId && t.RevokedAt == null, ct))
            .Should().Be(2);
    }

    [Fact]
    public async Task Rotate_ShouldReplaceTheTokenAndKeepTheSessionLive()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var context = _fixture.CreateContext();
        var store = CreateStore(context, Now);
        var original = await store.Issue(userId, TestSession, ct);

        // Act
        var result = await store.Rotate(userId, original, ct);

        // Assert
        result.Outcome.Should().Be(RefreshRotationOutcome.Rotated);
        result.Token.Should().NotBeNullOrWhiteSpace().And.NotBe(original);

        var stored = await context.UserRefreshTokens.SingleAsync(t => t.UserId == userId, ct);
        stored.RevokedAt.Should().BeNull();
        stored.PreviousTokenHash.Should().NotBeNull("the superseded hash is retained for reuse detection");
    }

    [Fact]
    public async Task Rotate_ShouldRotateOnlyTheSessionThatPresentedTheToken()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var context = _fixture.CreateContext();
        var store = CreateStore(context, Now);
        var laptop = await store.Issue(userId, TestSession, ct);
        var desktop = await store.Issue(userId, TestSession, ct);

        // Act
        await store.Rotate(userId, laptop, ct);

        // Assert - the desktop's token is untouched and still works
        var desktopResult = await store.Rotate(userId, desktop, ct);
        desktopResult.Outcome.Should().Be(RefreshRotationOutcome.Rotated);
    }

    [Fact]
    public async Task Rotate_ShouldReportNotFound_WhenTokenIsUnknown()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var context = _fixture.CreateContext();
        var store = CreateStore(context, Now);
        await store.Issue(userId, TestSession, ct);

        // Act
        var result = await store.Rotate(userId, "a-token-this-user-never-held", ct);

        // Assert
        result.Outcome.Should().Be(RefreshRotationOutcome.NotFound);
        (await context.UserRefreshTokens.CountAsync(t => t.UserId == userId && t.RevokedAt == null, ct))
            .Should().Be(1, "an unknown token is not evidence of compromise");
    }

    [Fact]
    public async Task Rotate_ShouldReportNotFound_WhenSessionHasExpired()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var context = _fixture.CreateContext();
        var token = await CreateStore(context, Now).Issue(userId, TestSession, ct);

        // Act - eight days later, past the seven-day lifetime
        var result = await CreateStore(context, Now.Plus(Duration.FromDays(8))).Rotate(userId, token, ct);

        // Assert
        result.Outcome.Should().Be(RefreshRotationOutcome.NotFound);
    }

    [Fact]
    public async Task Rotate_ShouldRevokeOnlyTheReplayedSession_WhenSupersededTokenIsPresented()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var context = _fixture.CreateContext();
        var store = CreateStore(context, Now);
        var laptop = await store.Issue(userId, TestSession, ct);
        await store.Issue(userId, TestSession, ct);
        await store.Rotate(userId, laptop, ct);

        // Act - replay the token that rotation just superseded
        var result = await store.Rotate(userId, laptop, ct);

        // Assert
        result.Outcome.Should().Be(RefreshRotationOutcome.ReuseDetected);

        var sessions = await context.UserRefreshTokens.Where(t => t.UserId == userId).ToListAsync(ct);
        sessions.Count(s => s.RevokedAt is null).Should().Be(1, "the other device keeps working");
        sessions.Single(s => s.RevokedAt is not null).RevokedReason
            .Should().Be(UserRefreshTokenRevokeReasons.ReuseDetected);
    }

    [Fact]
    public async Task Rotate_ShouldNotReportReuse_AfterTheDetectionWindowHasPassed()
    {
        // Past the window a superseded token is indistinguishable from any other stale value.

        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var context = _fixture.CreateContext();
        var token = await CreateStore(context, Now).Issue(userId, TestSession, ct);
        await CreateStore(context, Now).Rotate(userId, token, ct);

        // Act - ten minutes later, past the five-minute window
        var result = await CreateStore(context, Now.Plus(Duration.FromMinutes(10))).Rotate(userId, token, ct);

        // Assert
        result.Outcome.Should().Be(RefreshRotationOutcome.NotFound);
        (await context.UserRefreshTokens.CountAsync(t => t.UserId == userId && t.RevokedAt == null, ct))
            .Should().Be(1, "a stale token outside the window must not revoke a live session");
    }

    [Fact]
    public async Task RevokeAll_ShouldEndEverySessionForTheUserOnly()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);
        var otherUserId = await SeedUser(ct, suffix: "2");

        await using var context = _fixture.CreateContext();
        var store = CreateStore(context, Now);
        await store.Issue(userId, TestSession, ct);
        await store.Issue(userId, TestSession, ct);
        var otherToken = await store.Issue(otherUserId, TestSession, ct);

        // Act
        await store.RevokeAll(userId, UserRefreshTokenRevokeReasons.SignedOut, ct);

        // Assert
        (await context.UserRefreshTokens.CountAsync(t => t.UserId == userId && t.RevokedAt == null, ct))
            .Should().Be(0);
        (await store.Rotate(otherUserId, otherToken, ct)).Outcome
            .Should().Be(RefreshRotationOutcome.Rotated, "another user's sessions are untouched");
    }

    [Fact]
    public async Task RevokeAll_ShouldPreventReuseOfASupersededTokenAfterwards()
    {
        // Sign-out must clear both halves of the chain, or the reuse window would leave a
        // revoked session resurrectable.

        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var context = _fixture.CreateContext();
        var store = CreateStore(context, Now);
        var original = await store.Issue(userId, TestSession, ct);
        var rotated = await store.Rotate(userId, original, ct);

        // Act
        await store.RevokeAll(userId, UserRefreshTokenRevokeReasons.SignedOut, ct);

        // Assert
        (await store.Rotate(userId, original, ct)).Outcome.Should().Be(RefreshRotationOutcome.NotFound);
        (await store.Rotate(userId, rotated.Token!, ct)).Outcome.Should().Be(RefreshRotationOutcome.NotFound);
    }

    [Fact]
    public async Task RevokeAll_ShouldNotThrow_WhenUserHasNoSessions()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var context = _fixture.CreateContext();
        var store = CreateStore(context, Now);

        // Act
        var act = async () => await store.RevokeAll(userId, UserRefreshTokenRevokeReasons.SignedOut, ct);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Issue_ShouldRecordDeviceDetails()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        // Act
        await using var context = _fixture.CreateContext();
        await CreateStore(context, Now).Issue(userId, TestSession, ct);

        // Assert
        var stored = await context.UserRefreshTokens.SingleAsync(t => t.UserId == userId, ct);
        stored.DeviceLabel.Should().Be("Chrome on Windows");
        stored.IpAddress.Should().Be("203.0.113.42");
    }

    [Fact]
    public async Task Issue_ShouldStoreNullDeviceDetails_WhenRequestHasNone()
    {
        // Background and CLI callers have no user agent; the session must still be usable.

        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        // Act
        await using var context = _fixture.CreateContext();
        var token = await CreateStore(context, Now).Issue(userId, SessionContext.None, ct);

        // Assert
        var stored = await context.UserRefreshTokens.SingleAsync(t => t.UserId == userId, ct);
        stored.DeviceLabel.Should().BeNull();
        stored.IpAddress.Should().BeNull();
        (await CreateStore(context, Now).Rotate(userId, token, ct)).Outcome
            .Should().Be(RefreshRotationOutcome.Rotated);
    }

    [Fact]
    public async Task ListActive_ShouldReturnLiveSessionsNewestUsedFirst()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var context = _fixture.CreateContext();
        var older = await CreateStore(context, Now).Issue(userId, TestSession, ct);
        await CreateStore(context, Now.Plus(Duration.FromMinutes(5))).Issue(userId, TestSession, ct);

        // Touch the older session so it becomes the most recently used.
        await CreateStore(context, Now.Plus(Duration.FromMinutes(10))).Rotate(userId, older, ct);

        // Act
        var sessions = await CreateStore(context, Now.Plus(Duration.FromMinutes(11))).ListActive(userId, ct);

        // Assert
        sessions.Should().HaveCount(2);
        sessions[0].LastUsedAt.Should().BeGreaterThan(sessions[1].LastUsedAt);
    }

    [Fact]
    public async Task ListActive_ShouldOmitRevokedAndExpiredSessions()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var context = _fixture.CreateContext();
        var store = CreateStore(context, Now);
        var live = await store.Issue(userId, TestSession, ct);
        await store.Issue(userId, TestSession, ct);

        var revokedId = await store.FindSessionId(userId, live, ct);
        await store.Revoke(userId, revokedId!.Value, UserRefreshTokenRevokeReasons.SignedOut, ct);

        // Act
        var beforeExpiry = await CreateStore(context, Now).ListActive(userId, ct);
        var afterExpiry = await CreateStore(context, Now.Plus(Duration.FromDays(8))).ListActive(userId, ct);

        // Assert
        beforeExpiry.Should().HaveCount(1, "the revoked session is gone but the other is live");
        afterExpiry.Should().BeEmpty("expired sessions are not live either");
    }

    [Fact]
    public async Task FindSessionId_ShouldIdentifyTheSessionHoldingTheToken()
    {
        // This is what lets the UI mark "this device" and scope sign-out to one session.

        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var context = _fixture.CreateContext();
        var store = CreateStore(context, Now);
        var laptop = await store.Issue(userId, TestSession, ct);
        var desktop = await store.Issue(userId, TestSession, ct);

        // Act
        var laptopId = await store.FindSessionId(userId, laptop, ct);
        var desktopId = await store.FindSessionId(userId, desktop, ct);

        // Assert
        laptopId.Should().NotBeNull();
        desktopId.Should().NotBeNull();
        laptopId!.Value.Should().NotBe(desktopId!.Value);
    }

    [Fact]
    public async Task FindSessionId_ShouldReturnNull_WhenTokenIsNotTheirs()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);
        var otherUserId = await SeedUser(ct, suffix: "2");

        await using var context = _fixture.CreateContext();
        var store = CreateStore(context, Now);
        var othersToken = await store.Issue(otherUserId, TestSession, ct);

        // Act
        var found = await store.FindSessionId(userId, othersToken, ct);

        // Assert
        found.Should().BeNull();
    }

    [Fact]
    public async Task Revoke_ShouldEndOneSessionAndLeaveTheOthers()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var context = _fixture.CreateContext();
        var store = CreateStore(context, Now);
        var laptop = await store.Issue(userId, TestSession, ct);
        var desktop = await store.Issue(userId, TestSession, ct);
        var laptopId = await store.FindSessionId(userId, laptop, ct);

        // Act
        var revoked = await store.Revoke(userId, laptopId!.Value, UserRefreshTokenRevokeReasons.SignedOut, ct);

        // Assert
        revoked.Should().BeTrue();
        (await store.Rotate(userId, laptop, ct)).Outcome.Should().Be(RefreshRotationOutcome.NotFound);
        (await store.Rotate(userId, desktop, ct)).Outcome.Should().Be(RefreshRotationOutcome.Rotated);
    }

    [Fact]
    public async Task Revoke_ShouldReturnFalse_WhenSessionBelongsToAnotherUser()
    {
        // Must not become a way to end someone else's session by guessing an id.

        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);
        var otherUserId = await SeedUser(ct, suffix: "2");

        await using var context = _fixture.CreateContext();
        var store = CreateStore(context, Now);
        var othersToken = await store.Issue(otherUserId, TestSession, ct);
        var othersSessionId = await store.FindSessionId(otherUserId, othersToken, ct);

        // Act
        var revoked = await store.Revoke(userId, othersSessionId!.Value, UserRefreshTokenRevokeReasons.SignedOut, ct);

        // Assert
        revoked.Should().BeFalse();
        (await store.Rotate(otherUserId, othersToken, ct)).Outcome
            .Should().Be(RefreshRotationOutcome.Rotated, "the other user's session is untouched");
    }

    [Fact]
    public async Task Revoke_ShouldReturnFalse_WhenSessionIsAlreadyRevoked()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var context = _fixture.CreateContext();
        var store = CreateStore(context, Now);
        var token = await store.Issue(userId, TestSession, ct);
        var sessionId = await store.FindSessionId(userId, token, ct);
        await store.Revoke(userId, sessionId!.Value, UserRefreshTokenRevokeReasons.SignedOut, ct);

        // Act
        var again = await store.Revoke(userId, sessionId.Value, UserRefreshTokenRevokeReasons.SignedOut, ct);

        // Assert
        again.Should().BeFalse();
    }

    [Fact]
    public async Task FindSessionId_ShouldMatchAJustRotatedToken()
    {
        // Regression. A background refresh can rotate the stored token between the client
        // reading it and using it, so a seconds-old token must still identify its session.
        // When it did not, logout could not find the session and fell back to revoking
        // everything — one sign-out ended every device the user had.

        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var context = _fixture.CreateContext();
        var store = CreateStore(context, Now);
        var original = await store.Issue(userId, TestSession, ct);
        var sessionId = await store.FindSessionId(userId, original, ct);

        await store.Rotate(userId, original, ct);

        // Act - the client still holds the pre-rotation value
        var found = await store.FindSessionId(userId, original, ct);

        // Assert
        found.Should().Be(sessionId, "a just-superseded token still identifies its own session");
    }

    [Fact]
    public async Task FindSessionId_ShouldNotMatchASupersededTokenAfterTheWindow()
    {
        // The grace above is bounded by the same window Rotate uses for reuse detection.

        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var context = _fixture.CreateContext();
        var original = await CreateStore(context, Now).Issue(userId, TestSession, ct);
        await CreateStore(context, Now).Rotate(userId, original, ct);

        // Act - ten minutes later, past the five-minute window
        var found = await CreateStore(context, Now.Plus(Duration.FromMinutes(10)))
            .FindSessionId(userId, original, ct);

        // Assert
        found.Should().BeNull();
    }

    [Fact]
    public async Task FindSessionId_ShouldReturnNull_WhenSessionWasRevoked()
    {
        // A revoked session must not be resurrectable through the previous-token grace.

        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var context = _fixture.CreateContext();
        var store = CreateStore(context, Now);
        var original = await store.Issue(userId, TestSession, ct);
        var sessionId = await store.FindSessionId(userId, original, ct);
        await store.Rotate(userId, original, ct);
        await store.Revoke(userId, sessionId!.Value, UserRefreshTokenRevokeReasons.SignedOut, ct);

        // Act
        var found = await store.FindSessionId(userId, original, ct);

        // Assert
        found.Should().BeNull();
    }

    private const string ClientId = "https://client.acme.example/oauth/client-metadata.json";

    private static Task<bool> AnyUser(string userId, CancellationToken cancellationToken) => Task.FromResult(true);

    [Fact]
    public async Task RotateForClient_ShouldLeaveTheSessionUntouched_WhenItsUserMayNotRefresh()
    {
        // Arrange — a disabled user's token must work again once they are enabled
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);
        await using var context = _fixture.CreateContext();
        var session = await CreateStore(context, Now).IssueForClient(Guid.NewGuid(), userId, ClientId, "Acme Agent", TestSession, ct);
        string? askedAbout = null;

        // Act
        var refused = await CreateStore(context, Now.Plus(Duration.FromHours(1))).RotateForClient(
            ClientId, session.Token, (id, _) => { askedAbout = id; return Task.FromResult(false); }, ct);

        // Assert
        refused.Outcome.Should().Be(RefreshRotationOutcome.NotFound);
        askedAbout.Should().Be(userId);
        var stored = await context.UserRefreshTokens.AsNoTracking().SingleAsync(t => t.Id == session.SessionId, ct);
        stored.LastUsedAt.Should().Be(Now);
        stored.RevokedAt.Should().BeNull();
        (await CreateStore(context, Now).RotateForClient(ClientId, session.Token, AnyUser, ct)).Outcome.Should().Be(RefreshRotationOutcome.Rotated);
    }

    [Fact]
    public async Task RotateForClient_ShouldMatchTheClientExactly()
    {
        // Arrange — the column compares case-insensitively; a client_id is a URL whose path does not
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);
        await using var context = _fixture.CreateContext();
        var session = await CreateStore(context, Now).IssueForClient(Guid.NewGuid(), userId, ClientId, "Acme Agent", TestSession, ct);

        // Act
        var result = await CreateStore(context, Now).RotateForClient(ClientId.ToUpperInvariant(), session.Token, AnyUser, ct);

        // Assert
        result.Outcome.Should().Be(RefreshRotationOutcome.NotFound);
    }

    [Fact]
    public async Task RotateForClient_ShouldNameTheSessionItRotated()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);
        await using var context = _fixture.CreateContext();
        var sessionId = Guid.NewGuid();
        var session = await CreateStore(context, Now).IssueForClient(sessionId, userId, ClientId, "Acme Agent", TestSession, ct);

        // Act
        var result = await CreateStore(context, Now).RotateForClient(ClientId, session.Token, AnyUser, ct);

        // Assert
        session.SessionId.Should().Be(sessionId);
        result.SessionId.Should().Be(sessionId);
    }

    [Fact]
    public async Task IssueForClient_ShouldOpenASessionNamedForTheClient_WithATokenThatLocatesIt()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        // Act
        await using var context = _fixture.CreateContext();
        var session = await CreateStore(context, Now).IssueForClient(Guid.NewGuid(), userId, ClientId, "Acme Agent", TestSession, ct);

        // Assert
        var stored = await context.UserRefreshTokens.SingleAsync(t => t.UserId == userId, ct);
        stored.Id.Should().Be(session.SessionId);
        stored.ClientId.Should().Be(ClientId);
        stored.DeviceLabel.Should().Be("Acme Agent");
        session.Token.Should().StartWith($"{session.SessionId:N}.");
        stored.TokenHash.Should().NotContain(session.Token);
        RefreshTokenHasher.Verify(session.Token, stored.TokenHash).Should().BeTrue();
    }

    [Fact]
    public async Task IssueForClient_ShouldKeepEarlierSessionsForTheSameClient()
    {
        // Arrange — the same client connected from two machines
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);
        await using var context = _fixture.CreateContext();
        var first = await CreateStore(context, Now).IssueForClient(Guid.NewGuid(), userId, ClientId, "Acme Agent", TestSession, ct);

        // Act
        await CreateStore(context, Now).IssueForClient(Guid.NewGuid(), userId, ClientId, "Acme Agent", TestSession, ct);

        // Assert
        (await context.UserRefreshTokens.CountAsync(t => t.UserId == userId && t.RevokedAt == null, ct)).Should().Be(2);
        (await CreateStore(context, Now).RotateForClient(ClientId, first.Token, AnyUser, ct)).Outcome.Should().Be(RefreshRotationOutcome.Rotated);
    }

    [Fact]
    public async Task RotateForClient_ShouldReplaceTheTokenAndNameTheUser()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);
        await using var context = _fixture.CreateContext();
        var session = await CreateStore(context, Now).IssueForClient(Guid.NewGuid(), userId, ClientId, "Acme Agent", TestSession, ct);

        // Act
        var result = await CreateStore(context, Now.Plus(Duration.FromHours(1))).RotateForClient(ClientId, session.Token, AnyUser, ct);

        // Assert
        result.Outcome.Should().Be(RefreshRotationOutcome.Rotated);
        result.UserId.Should().Be(userId);
        result.Token.Should().StartWith($"{session.SessionId:N}.").And.NotBe(session.Token);
        var stored = await context.UserRefreshTokens.SingleAsync(t => t.Id == session.SessionId, ct);
        RefreshTokenHasher.Verify(result.Token, stored.TokenHash).Should().BeTrue();
        stored.LastUsedAt.Should().Be(Now.Plus(Duration.FromHours(1)));
    }

    [Fact]
    public async Task RotateForClient_ShouldRotateNothing_WhenAnotherClientPresentsTheToken()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);
        await using var context = _fixture.CreateContext();
        var session = await CreateStore(context, Now).IssueForClient(Guid.NewGuid(), userId, ClientId, "Acme Agent", TestSession, ct);

        // Act
        var result = await CreateStore(context, Now).RotateForClient("https://other.acme.example/client.json", session.Token, AnyUser, ct);

        // Assert
        result.Outcome.Should().Be(RefreshRotationOutcome.NotFound);
        (await CreateStore(context, Now).RotateForClient(ClientId, session.Token, AnyUser, ct)).Outcome
            .Should().Be(RefreshRotationOutcome.Rotated, "the rightful client's token must still work");
    }

    [Theory]
    [InlineData("not-a-session-token")]
    [InlineData("00000000000000000000000000000000.secret")]
    public async Task RotateForClient_ShouldReportNotFound_ForATokenThatNamesNoSession(string token)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        await using var context = _fixture.CreateContext();

        // Act
        var result = await CreateStore(context, Now).RotateForClient(ClientId, token, AnyUser, ct);

        // Assert
        result.Outcome.Should().Be(RefreshRotationOutcome.NotFound);
        result.UserId.Should().BeNull();
    }

    [Fact]
    public async Task RotateForClient_ShouldReportNotFound_WhenTheSecretIsWrong()
    {
        // Arrange — the session id is right, so only the hash check stands between a guess and the session
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);
        await using var context = _fixture.CreateContext();
        var session = await CreateStore(context, Now).IssueForClient(Guid.NewGuid(), userId, ClientId, "Acme Agent", TestSession, ct);

        // Act
        var result = await CreateStore(context, Now).RotateForClient(ClientId, $"{session.SessionId:N}.guessed", AnyUser, ct);

        // Assert
        result.Outcome.Should().Be(RefreshRotationOutcome.NotFound);
    }

    [Fact]
    public async Task RotateForClient_ShouldRevokeTheSession_WhenASupersededTokenIsPresented()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);
        await using var context = _fixture.CreateContext();
        var session = await CreateStore(context, Now).IssueForClient(Guid.NewGuid(), userId, ClientId, "Acme Agent", TestSession, ct);
        var rotated = await CreateStore(context, Now).RotateForClient(ClientId, session.Token, AnyUser, ct);

        // Act
        var result = await CreateStore(context, Now).RotateForClient(ClientId, session.Token, AnyUser, ct);

        // Assert
        result.Outcome.Should().Be(RefreshRotationOutcome.ReuseDetected);
        var stored = await context.UserRefreshTokens.SingleAsync(t => t.Id == session.SessionId, ct);
        stored.RevokedReason.Should().Be(UserRefreshTokenRevokeReasons.ReuseDetected);
        (await CreateStore(context, Now).RotateForClient(ClientId, rotated.Token!, AnyUser, ct)).Outcome
            .Should().Be(RefreshRotationOutcome.NotFound, "the replacement dies with the session");
    }

    [Fact]
    public async Task Rotate_ShouldNotMatchAClientsSession()
    {
        // Arrange — the web app's refresh, given a client's token for the same user
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);
        await using var context = _fixture.CreateContext();
        var session = await CreateStore(context, Now).IssueForClient(Guid.NewGuid(), userId, ClientId, "Acme Agent", TestSession, ct);

        // Act
        var result = await CreateStore(context, Now).Rotate(userId, session.Token, ct);

        // Assert
        result.Outcome.Should().Be(RefreshRotationOutcome.NotFound);
        (await CreateStore(context, Now).RotateForClient(ClientId, session.Token, AnyUser, ct)).Outcome.Should().Be(RefreshRotationOutcome.Rotated);
    }

    [Fact]
    public async Task ListActive_ShouldIncludeClientSessions()
    {
        // Arrange — a connector is revocable only if it is listed
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);
        await using var context = _fixture.CreateContext();
        await CreateStore(context, Now).Issue(userId, TestSession, ct);
        var session = await CreateStore(context, Now).IssueForClient(Guid.NewGuid(), userId, ClientId, "Acme Agent", TestSession, ct);

        // Act
        var sessions = await CreateStore(context, Now).ListActive(userId, ct);

        // Assert
        sessions.Should().HaveCount(2);
        sessions.Single(s => s.Id == session.SessionId).ClientId.Should().Be(ClientId);
    }
}
