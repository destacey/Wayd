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
[Trait("Category", "Docker")]
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
}
