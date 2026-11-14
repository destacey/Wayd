using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Identity;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Authorization;
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
    /// <summary>The credential version every session in these tests is opened and refreshed under, unless a test says otherwise.</summary>
    private const string CredentialVersionUnderTest = "credential-version-1";

    private static readonly Instant Now = Instant.FromUtc(2026, 3, 1, 12, 0, 0);

    private static readonly SessionContext TestSession = new(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36",
        "203.0.113.42");

    private readonly SqlServerDbContextFixture _fixture = fixture;

    private static IConfiguration Configuration => ConfigurationWith(sessionLifetimeInDays: 30);

    /// <summary>The store's configuration, with sessions living <paramref name="sessionLifetimeInDays"/>.</summary>
    private static IConfiguration ConfigurationWith(int sessionLifetimeInDays) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "SecuritySettings:LocalJwt:RefreshTokenExpirationInDays", "7" },
            { "SecuritySettings:LocalJwt:SessionLifetimeInDays", sessionLifetimeInDays.ToString(System.Globalization.CultureInfo.InvariantCulture) },
        })
        .Build();

    /// <summary>A request from somewhere other than <see cref="TestSession"/>.</summary>
    private static readonly SessionContext ElsewhereSession = new("curl/8.0", "198.51.100.7");

    private static RefreshTokenStore CreateStore(WaydDbContext context, Instant now, IConfiguration? configuration = null)
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.Now).Returns(now);

        return new RefreshTokenStore(context, clock.Object, configuration ?? Configuration, NullLogger<RefreshTokenStore>.Instance);
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
        var token = await CreateStore(context, Now).Issue(userId, CredentialVersionUnderTest, TestSession, ct);

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
        var first = await store.Issue(userId, CredentialVersionUnderTest, TestSession, ct);
        var second = await store.Issue(userId, CredentialVersionUnderTest, TestSession, ct);

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
        var original = await store.Issue(userId, CredentialVersionUnderTest, TestSession, ct);

        // Act
        var result = await store.Rotate(userId, original, CredentialVersionUnderTest, TestSession, ct);

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
        var laptop = await store.Issue(userId, CredentialVersionUnderTest, TestSession, ct);
        var desktop = await store.Issue(userId, CredentialVersionUnderTest, TestSession, ct);

        // Act
        await store.Rotate(userId, laptop, CredentialVersionUnderTest, TestSession, ct);

        // Assert - the desktop's token is untouched and still works
        var desktopResult = await store.Rotate(userId, desktop, CredentialVersionUnderTest, TestSession, ct);
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
        await store.Issue(userId, CredentialVersionUnderTest, TestSession, ct);

        // Act
        var result = await store.Rotate(userId, "a-token-this-user-never-held", CredentialVersionUnderTest, TestSession, ct);

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
        var token = await CreateStore(context, Now).Issue(userId, CredentialVersionUnderTest, TestSession, ct);

        // Act - eight days later, past the seven-day lifetime
        var result = await CreateStore(context, Now.Plus(Duration.FromDays(8))).Rotate(userId, token, CredentialVersionUnderTest, TestSession, ct);

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
        var laptop = await store.Issue(userId, CredentialVersionUnderTest, TestSession, ct);
        await store.Issue(userId, CredentialVersionUnderTest, TestSession, ct);
        await store.Rotate(userId, laptop, CredentialVersionUnderTest, TestSession, ct);

        // Act - replay the token that rotation superseded, after the retry grace
        var result = await CreateStore(context, Now.Plus(Duration.FromMinutes(1))).Rotate(userId, laptop, CredentialVersionUnderTest, TestSession, ct);

        // Assert
        result.Outcome.Should().Be(RefreshRotationOutcome.ReuseDetected);

        var sessions = await context.UserRefreshTokens.Where(t => t.UserId == userId).ToListAsync(ct);
        sessions.Count(s => s.RevokedAt is null).Should().Be(1, "the other device keeps working");
        sessions.Single(s => s.RevokedAt is not null).RevokedReason
            .Should().Be(UserRefreshTokenRevokeReasons.ReuseDetected);
    }

    [Fact]
    public async Task Rotate_ShouldRevokeTheSession_WhenTheSupersededTokenIsReplayedLongAfter()
    {
        // Arrange — the second holder of a token is caught however long it waits
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var context = _fixture.CreateContext();
        var token = await CreateStore(context, Now).Issue(userId, CredentialVersionUnderTest, TestSession, ct);
        await CreateStore(context, Now).Rotate(userId, token, CredentialVersionUnderTest, TestSession, ct);

        // Act — well past the replacement token's own seven-day expiry
        var result = await CreateStore(context, Now.Plus(Duration.FromDays(20))).Rotate(userId, token, CredentialVersionUnderTest, TestSession, ct);

        // Assert
        result.Outcome.Should().Be(RefreshRotationOutcome.ReuseDetected);
        (await context.UserRefreshTokens.AsNoTracking().SingleAsync(t => t.UserId == userId, ct)).RevokedReason
            .Should().Be(UserRefreshTokenRevokeReasons.ReuseDetected);
    }

    [Fact]
    public async Task Rotate_ShouldRefuseWithoutRevoking_WhenTheSupersededTokenIsRetriedWithinTheGrace()
    {
        // Arrange — two tabs refreshing with the same token at once
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var context = _fixture.CreateContext();
        var token = await CreateStore(context, Now).Issue(userId, CredentialVersionUnderTest, TestSession, ct);
        var rotated = await CreateStore(context, Now).Rotate(userId, token, CredentialVersionUnderTest, TestSession, ct);

        // Act
        var retry = await CreateStore(context, Now.Plus(Duration.FromSeconds(5))).Rotate(userId, token, CredentialVersionUnderTest, TestSession, ct);

        // Assert
        retry.Outcome.Should().Be(RefreshRotationOutcome.NotFound);
        (await CreateStore(context, Now.Plus(Duration.FromSeconds(6))).Rotate(userId, rotated.Token!, CredentialVersionUnderTest, TestSession, ct)).Outcome
            .Should().Be(RefreshRotationOutcome.Rotated, "the winning tab's token still works");
    }

    [Fact]
    public async Task Rotate_ShouldRevokeTheSession_WhenTheSupersededTokenComesFromElsewhereWithinTheGrace()
    {
        // Arrange — a thief rotating just before the client's own refresh must not have the client's presentation read as a retry
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var context = _fixture.CreateContext();
        var token = await CreateStore(context, Now).Issue(userId, CredentialVersionUnderTest, TestSession, ct);
        await CreateStore(context, Now).Rotate(userId, token, CredentialVersionUnderTest, ElsewhereSession, ct);

        // Act
        var result = await CreateStore(context, Now.Plus(Duration.FromSeconds(5))).Rotate(userId, token, CredentialVersionUnderTest, TestSession, ct);

        // Assert
        result.Outcome.Should().Be(RefreshRotationOutcome.ReuseDetected);
    }

    [Fact]
    public async Task Rotate_ShouldRefuse_ASessionPastAShortenedLifetime()
    {
        // Arrange — opened under a 30-day lifetime that has since been lowered to 10 days
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var context = _fixture.CreateContext();
        var token = await CreateStore(context, Now).Issue(userId, CredentialVersionUnderTest, TestSession, ct);
        token = (await CreateStore(context, Now.Plus(Duration.FromDays(6))).Rotate(userId, token, CredentialVersionUnderTest, TestSession, ct)).Token!;

        // Act
        var result = await CreateStore(context, Now.Plus(Duration.FromDays(11)), ConfigurationWith(sessionLifetimeInDays: 10))
            .Rotate(userId, token, CredentialVersionUnderTest, TestSession, ct);
        var listed = await CreateStore(context, Now.Plus(Duration.FromDays(11)), ConfigurationWith(sessionLifetimeInDays: 10)).ListActive(userId, ct);

        // Assert — the token's own expiry, day 13, has not passed: only the session's end refuses it
        result.Outcome.Should().Be(RefreshRotationOutcome.NotFound);
        listed.Should().BeEmpty();
    }

    [Fact]
    public async Task Rotate_ShouldRefuseTheLosingRotation_WhenTwoRotateTheSameTokenAtOnce()
    {
        // Arrange — the second request read the session before the first committed its rotation
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var first = _fixture.CreateContext();
        await using var second = _fixture.CreateContext();
        var token = await CreateStore(first, Now).Issue(userId, CredentialVersionUnderTest, TestSession, ct);
        await second.UserRefreshTokens.Where(t => t.UserId == userId).ToListAsync(ct);
        var winner = await CreateStore(first, Now).Rotate(userId, token, CredentialVersionUnderTest, TestSession, ct);

        // Act
        var loser = await CreateStore(second, Now).Rotate(userId, token, CredentialVersionUnderTest, TestSession, ct);

        // Assert
        loser.Outcome.Should().Be(RefreshRotationOutcome.NotFound);
        await using var check = _fixture.CreateContext();
        (await check.UserRefreshTokens.AsNoTracking().SingleAsync(t => t.UserId == userId, ct)).RevokedAt.Should().BeNull();
        (await CreateStore(check, Now.Plus(Duration.FromMinutes(1))).Rotate(userId, winner.Token!, CredentialVersionUnderTest, TestSession, ct)).Outcome
            .Should().Be(RefreshRotationOutcome.Rotated, "the winner's token is the one that counts");
    }

    [Fact]
    public async Task RevokeAll_ShouldRevokeASessionThatWasRotatedAfterItWasRead()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var first = _fixture.CreateContext();
        await using var second = _fixture.CreateContext();
        var token = await CreateStore(first, Now).Issue(userId, CredentialVersionUnderTest, TestSession, ct);
        await second.UserRefreshTokens.Where(t => t.UserId == userId).ToListAsync(ct);
        await CreateStore(first, Now).Rotate(userId, token, CredentialVersionUnderTest, TestSession, ct);

        // Act
        await CreateStore(second, Now).RevokeAll(userId, UserRefreshTokenRevokeReasons.PasswordChanged, ct);

        // Assert
        await using var check = _fixture.CreateContext();
        (await check.UserRefreshTokens.AsNoTracking().SingleAsync(t => t.UserId == userId, ct)).RevokedReason
            .Should().Be(UserRefreshTokenRevokeReasons.PasswordChanged);
    }

    [Fact]
    public async Task Revoke_ShouldRevokeASessionThatWasRotatedAfterItWasRead()
    {
        // Arrange — signing out on one tab while another refreshes
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var first = _fixture.CreateContext();
        await using var second = _fixture.CreateContext();
        var token = await CreateStore(first, Now).Issue(userId, CredentialVersionUnderTest, TestSession, ct);
        var sessionId = (await second.UserRefreshTokens.Where(t => t.UserId == userId).ToListAsync(ct)).Single().Id;
        await CreateStore(first, Now).Rotate(userId, token, CredentialVersionUnderTest, TestSession, ct);

        // Act
        var revoked = await CreateStore(second, Now).Revoke(userId, sessionId, UserRefreshTokenRevokeReasons.SignedOut, ct);

        // Assert
        revoked.Should().BeTrue();
        await using var check = _fixture.CreateContext();
        (await check.UserRefreshTokens.AsNoTracking().SingleAsync(t => t.Id == sessionId, ct)).RevokedReason
            .Should().Be(UserRefreshTokenRevokeReasons.SignedOut);
    }

    [Fact]
    public async Task Rotate_ShouldRefuseWithoutRevoking_ATokenTwoRotationsOld()
    {
        // Arrange — only the immediately superseded token is recognised; an older one gains nothing
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var context = _fixture.CreateContext();
        var first = await CreateStore(context, Now).Issue(userId, CredentialVersionUnderTest, TestSession, ct);
        var second = await CreateStore(context, Now).Rotate(userId, first, CredentialVersionUnderTest, TestSession, ct);
        await CreateStore(context, Now.Plus(Duration.FromHours(1))).Rotate(userId, second.Token!, CredentialVersionUnderTest, TestSession, ct);

        // Act
        var result = await CreateStore(context, Now.Plus(Duration.FromHours(2))).Rotate(userId, first, CredentialVersionUnderTest, TestSession, ct);

        // Assert
        result.Outcome.Should().Be(RefreshRotationOutcome.NotFound);
        (await context.UserRefreshTokens.CountAsync(t => t.UserId == userId && t.RevokedAt == null, ct)).Should().Be(1);
    }

    [Fact]
    public async Task Rotate_ShouldNotExtendASessionPastItsLifetime()
    {
        // Arrange — refreshed every six days, which would otherwise slide it forever
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var context = _fixture.CreateContext();
        var token = await CreateStore(context, Now).Issue(userId, CredentialVersionUnderTest, TestSession, ct);
        foreach (var day in new[] { 6, 12, 18, 24 })
            token = (await CreateStore(context, Now.Plus(Duration.FromDays(day))).Rotate(userId, token, CredentialVersionUnderTest, TestSession, ct)).Token!;

        // Act
        var lastDay = await CreateStore(context, Now.Plus(Duration.FromDays(29))).Rotate(userId, token, CredentialVersionUnderTest, TestSession, ct);
        var pastTheEnd = await CreateStore(context, Now.Plus(Duration.FromDays(30)).Plus(Duration.FromMinutes(1))).Rotate(userId, lastDay.Token!, CredentialVersionUnderTest, TestSession, ct);

        // Assert
        lastDay.Outcome.Should().Be(RefreshRotationOutcome.Rotated);
        (await context.UserRefreshTokens.AsNoTracking().SingleAsync(t => t.UserId == userId, ct)).ExpiresAt
            .Should().Be(Now.Plus(Duration.FromDays(30)), "a refresh extends the token only up to the session's end");
        pastTheEnd.Outcome.Should().Be(RefreshRotationOutcome.NotFound);
    }

    [Fact]
    public async Task Rotate_ShouldRevokeTheSession_WhenItWasOpenedUnderCredentialsThatHaveChanged()
    {
        // Arrange — a sign-in that checked a password the user has since replaced
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var context = _fixture.CreateContext();
        var token = await CreateStore(context, Now).Issue(userId, "credential-version-before", TestSession, ct);

        // Act
        var result = await CreateStore(context, Now).Rotate(userId, token, "credential-version-after", TestSession, ct);

        // Assert
        result.Outcome.Should().Be(RefreshRotationOutcome.NotFound);
        (await context.UserRefreshTokens.AsNoTracking().SingleAsync(t => t.UserId == userId, ct)).RevokedReason
            .Should().Be(UserRefreshTokenRevokeReasons.CredentialChanged);
    }

    [Fact]
    public async Task Rotate_ShouldRotateASessionThatRecordsNoCredentialVersion()
    {
        // Arrange — opened before sessions recorded one
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var context = _fixture.CreateContext();
        var token = await CreateStore(context, Now).Issue(userId, CredentialVersionUnderTest, TestSession, ct);
        await context.UserRefreshTokens.Where(t => t.UserId == userId)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.CredentialVersion, (string?)null), ct);
        context.ChangeTracker.Clear();

        // Act
        var result = await CreateStore(context, Now).Rotate(userId, token, "any-credential-version", TestSession, ct);

        // Assert
        result.Outcome.Should().Be(RefreshRotationOutcome.Rotated);
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
        await store.Issue(userId, CredentialVersionUnderTest, TestSession, ct);
        await store.Issue(userId, CredentialVersionUnderTest, TestSession, ct);
        var otherToken = await store.Issue(otherUserId, CredentialVersionUnderTest, TestSession, ct);

        // Act
        await store.RevokeAll(userId, UserRefreshTokenRevokeReasons.SignedOut, ct);

        // Assert
        (await context.UserRefreshTokens.CountAsync(t => t.UserId == userId && t.RevokedAt == null, ct))
            .Should().Be(0);
        (await store.Rotate(otherUserId, otherToken, CredentialVersionUnderTest, TestSession, ct)).Outcome
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
        var original = await store.Issue(userId, CredentialVersionUnderTest, TestSession, ct);
        var rotated = await store.Rotate(userId, original, CredentialVersionUnderTest, TestSession, ct);

        // Act
        await store.RevokeAll(userId, UserRefreshTokenRevokeReasons.SignedOut, ct);

        // Assert
        (await store.Rotate(userId, original, CredentialVersionUnderTest, TestSession, ct)).Outcome.Should().Be(RefreshRotationOutcome.NotFound);
        (await store.Rotate(userId, rotated.Token!, CredentialVersionUnderTest, TestSession, ct)).Outcome.Should().Be(RefreshRotationOutcome.NotFound);
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
        await CreateStore(context, Now).Issue(userId, CredentialVersionUnderTest, TestSession, ct);

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
        var token = await CreateStore(context, Now).Issue(userId, CredentialVersionUnderTest, SessionContext.None, ct);

        // Assert
        var stored = await context.UserRefreshTokens.SingleAsync(t => t.UserId == userId, ct);
        stored.DeviceLabel.Should().BeNull();
        stored.IpAddress.Should().BeNull();
        (await CreateStore(context, Now).Rotate(userId, token, CredentialVersionUnderTest, TestSession, ct)).Outcome
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
        var older = await CreateStore(context, Now).Issue(userId, CredentialVersionUnderTest, TestSession, ct);
        await CreateStore(context, Now.Plus(Duration.FromMinutes(5))).Issue(userId, CredentialVersionUnderTest, TestSession, ct);

        // Touch the older session so it becomes the most recently used.
        await CreateStore(context, Now.Plus(Duration.FromMinutes(10))).Rotate(userId, older, CredentialVersionUnderTest, TestSession, ct);

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
        var live = await store.Issue(userId, CredentialVersionUnderTest, TestSession, ct);
        await store.Issue(userId, CredentialVersionUnderTest, TestSession, ct);

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
        var laptop = await store.Issue(userId, CredentialVersionUnderTest, TestSession, ct);
        var desktop = await store.Issue(userId, CredentialVersionUnderTest, TestSession, ct);

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
        var othersToken = await store.Issue(otherUserId, CredentialVersionUnderTest, TestSession, ct);

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
        var laptop = await store.Issue(userId, CredentialVersionUnderTest, TestSession, ct);
        var desktop = await store.Issue(userId, CredentialVersionUnderTest, TestSession, ct);
        var laptopId = await store.FindSessionId(userId, laptop, ct);

        // Act
        var revoked = await store.Revoke(userId, laptopId!.Value, UserRefreshTokenRevokeReasons.SignedOut, ct);

        // Assert
        revoked.Should().BeTrue();
        (await store.Rotate(userId, laptop, CredentialVersionUnderTest, TestSession, ct)).Outcome.Should().Be(RefreshRotationOutcome.NotFound);
        (await store.Rotate(userId, desktop, CredentialVersionUnderTest, TestSession, ct)).Outcome.Should().Be(RefreshRotationOutcome.Rotated);
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
        var othersToken = await store.Issue(otherUserId, CredentialVersionUnderTest, TestSession, ct);
        var othersSessionId = await store.FindSessionId(otherUserId, othersToken, ct);

        // Act
        var revoked = await store.Revoke(userId, othersSessionId!.Value, UserRefreshTokenRevokeReasons.SignedOut, ct);

        // Assert
        revoked.Should().BeFalse();
        (await store.Rotate(otherUserId, othersToken, CredentialVersionUnderTest, TestSession, ct)).Outcome
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
        var token = await store.Issue(userId, CredentialVersionUnderTest, TestSession, ct);
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
        var original = await store.Issue(userId, CredentialVersionUnderTest, TestSession, ct);
        var sessionId = await store.FindSessionId(userId, original, ct);

        await store.Rotate(userId, original, CredentialVersionUnderTest, TestSession, ct);

        // Act - the client still holds the pre-rotation value
        var found = await store.FindSessionId(userId, original, ct);

        // Assert
        found.Should().Be(sessionId, "a just-superseded token still identifies its own session");
    }

    [Fact]
    public async Task FindSessionId_ShouldNotMatchASupersededTokenAfterTheWindow()
    {
        // The grace above is bounded by the same retry grace Rotate uses.

        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);

        await using var context = _fixture.CreateContext();
        var original = await CreateStore(context, Now).Issue(userId, CredentialVersionUnderTest, TestSession, ct);
        await CreateStore(context, Now).Rotate(userId, original, CredentialVersionUnderTest, TestSession, ct);

        // Act - just past the ten-second retry grace
        var found = await CreateStore(context, Now.Plus(Duration.FromSeconds(11)))
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
        var original = await store.Issue(userId, CredentialVersionUnderTest, TestSession, ct);
        var sessionId = await store.FindSessionId(userId, original, ct);
        await store.Rotate(userId, original, CredentialVersionUnderTest, TestSession, ct);
        await store.Revoke(userId, sessionId!.Value, UserRefreshTokenRevokeReasons.SignedOut, ct);

        // Act
        var found = await store.FindSessionId(userId, original, ct);

        // Assert
        found.Should().BeNull();
    }

    private const string ClientId = "https://client.acme.example/oauth/client-metadata.json";

    private static Task<string?> AnyUser(string userId, CancellationToken cancellationToken) => Task.FromResult<string?>(CredentialVersionUnderTest);

    [Fact]
    public async Task RotateForClient_ShouldLeaveTheSessionUntouched_WhenItsUserMayNotRefresh()
    {
        // Arrange — a disabled user's token must work again once they are enabled
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);
        await using var context = _fixture.CreateContext();
        var session = await CreateStore(context, Now).IssueForClient(Guid.NewGuid(), userId, ClientId, "Acme Agent", TokenScope.Write, CredentialVersionUnderTest, TestSession, ct);
        string? askedAbout = null;

        // Act
        var refused = await CreateStore(context, Now.Plus(Duration.FromHours(1))).RotateForClient(
            ClientId, session.Token, (id, _) => { askedAbout = id; return Task.FromResult<string?>(null); }, TestSession, ct);

        // Assert
        refused.Outcome.Should().Be(RefreshRotationOutcome.NotFound);
        askedAbout.Should().Be(userId);
        var stored = await context.UserRefreshTokens.AsNoTracking().SingleAsync(t => t.Id == session.SessionId, ct);
        stored.LastUsedAt.Should().Be(Now);
        stored.RevokedAt.Should().BeNull();
        (await CreateStore(context, Now).RotateForClient(ClientId, session.Token, AnyUser, TestSession, ct)).Outcome.Should().Be(RefreshRotationOutcome.Rotated);
    }

    [Theory]
    [InlineData(TokenScope.Read)]
    [InlineData(TokenScope.Write)]
    public async Task RotateForClient_ShouldAnswerTheScopeTheSessionWasGranted(TokenScope scope)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);
        await using var context = _fixture.CreateContext();
        var session = await CreateStore(context, Now).IssueForClient(Guid.NewGuid(), userId, ClientId, "Acme Agent", scope, CredentialVersionUnderTest, TestSession, ct);

        // Act
        await using var later = _fixture.CreateContext();
        var rotated = await CreateStore(later, Now.Plus(Duration.FromHours(1))).RotateForClient(ClientId, session.Token, AnyUser, TestSession, ct);

        // Assert
        rotated.Outcome.Should().Be(RefreshRotationOutcome.Rotated);
        rotated.Scope.Should().Be(scope);
        (await later.UserRefreshTokens.AsNoTracking().SingleAsync(t => t.Id == session.SessionId, ct)).Scope.Should().Be(scope);
    }

    [Fact]
    public async Task RotateForClient_ShouldMatchTheClientExactly()
    {
        // Arrange — the column compares case-insensitively; a client_id is a URL whose path does not
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);
        await using var context = _fixture.CreateContext();
        var session = await CreateStore(context, Now).IssueForClient(Guid.NewGuid(), userId, ClientId, "Acme Agent", TokenScope.Write, CredentialVersionUnderTest, TestSession, ct);

        // Act
        var result = await CreateStore(context, Now).RotateForClient(ClientId.ToUpperInvariant(), session.Token, AnyUser, TestSession, ct);

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
        var session = await CreateStore(context, Now).IssueForClient(sessionId, userId, ClientId, "Acme Agent", TokenScope.Write, CredentialVersionUnderTest, TestSession, ct);

        // Act
        var result = await CreateStore(context, Now).RotateForClient(ClientId, session.Token, AnyUser, TestSession, ct);

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
        var session = await CreateStore(context, Now).IssueForClient(Guid.NewGuid(), userId, ClientId, "Acme Agent", TokenScope.Write, CredentialVersionUnderTest, TestSession, ct);

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
        var first = await CreateStore(context, Now).IssueForClient(Guid.NewGuid(), userId, ClientId, "Acme Agent", TokenScope.Write, CredentialVersionUnderTest, TestSession, ct);

        // Act
        await CreateStore(context, Now).IssueForClient(Guid.NewGuid(), userId, ClientId, "Acme Agent", TokenScope.Write, CredentialVersionUnderTest, TestSession, ct);

        // Assert
        (await context.UserRefreshTokens.CountAsync(t => t.UserId == userId && t.RevokedAt == null, ct)).Should().Be(2);
        (await CreateStore(context, Now).RotateForClient(ClientId, first.Token, AnyUser, TestSession, ct)).Outcome.Should().Be(RefreshRotationOutcome.Rotated);
    }

    [Fact]
    public async Task RotateForClient_ShouldReplaceTheTokenAndNameTheUser()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);
        await using var context = _fixture.CreateContext();
        var session = await CreateStore(context, Now).IssueForClient(Guid.NewGuid(), userId, ClientId, "Acme Agent", TokenScope.Write, CredentialVersionUnderTest, TestSession, ct);

        // Act
        var result = await CreateStore(context, Now.Plus(Duration.FromHours(1))).RotateForClient(ClientId, session.Token, AnyUser, TestSession, ct);

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
        var session = await CreateStore(context, Now).IssueForClient(Guid.NewGuid(), userId, ClientId, "Acme Agent", TokenScope.Write, CredentialVersionUnderTest, TestSession, ct);

        // Act
        var result = await CreateStore(context, Now).RotateForClient("https://other.acme.example/client.json", session.Token, AnyUser, TestSession, ct);

        // Assert
        result.Outcome.Should().Be(RefreshRotationOutcome.NotFound);
        (await CreateStore(context, Now).RotateForClient(ClientId, session.Token, AnyUser, TestSession, ct)).Outcome
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
        var result = await CreateStore(context, Now).RotateForClient(ClientId, token, AnyUser, TestSession, ct);

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
        var session = await CreateStore(context, Now).IssueForClient(Guid.NewGuid(), userId, ClientId, "Acme Agent", TokenScope.Write, CredentialVersionUnderTest, TestSession, ct);

        // Act
        var result = await CreateStore(context, Now).RotateForClient(ClientId, $"{session.SessionId:N}.guessed", AnyUser, TestSession, ct);

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
        var session = await CreateStore(context, Now).IssueForClient(Guid.NewGuid(), userId, ClientId, "Acme Agent", TokenScope.Write, CredentialVersionUnderTest, TestSession, ct);
        var rotated = await CreateStore(context, Now).RotateForClient(ClientId, session.Token, AnyUser, TestSession, ct);

        // Act — after the retry grace
        var result = await CreateStore(context, Now.Plus(Duration.FromMinutes(1))).RotateForClient(ClientId, session.Token, AnyUser, TestSession, ct);

        // Assert
        result.Outcome.Should().Be(RefreshRotationOutcome.ReuseDetected);
        var stored = await context.UserRefreshTokens.SingleAsync(t => t.Id == session.SessionId, ct);
        stored.RevokedReason.Should().Be(UserRefreshTokenRevokeReasons.ReuseDetected);
        (await CreateStore(context, Now).RotateForClient(ClientId, rotated.Token!, AnyUser, TestSession, ct)).Outcome
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
        var session = await CreateStore(context, Now).IssueForClient(Guid.NewGuid(), userId, ClientId, "Acme Agent", TokenScope.Write, CredentialVersionUnderTest, TestSession, ct);

        // Act
        var result = await CreateStore(context, Now).Rotate(userId, session.Token, CredentialVersionUnderTest, TestSession, ct);

        // Assert
        result.Outcome.Should().Be(RefreshRotationOutcome.NotFound);
        (await CreateStore(context, Now).RotateForClient(ClientId, session.Token, AnyUser, TestSession, ct)).Outcome.Should().Be(RefreshRotationOutcome.Rotated);
    }

    [Fact]
    public async Task ListActive_ShouldIncludeClientSessions()
    {
        // Arrange — a connector is revocable only if it is listed
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetIdentityData(ct);
        var userId = await SeedUser(ct);
        await using var context = _fixture.CreateContext();
        await CreateStore(context, Now).Issue(userId, CredentialVersionUnderTest, TestSession, ct);
        var session = await CreateStore(context, Now).IssueForClient(Guid.NewGuid(), userId, ClientId, "Acme Agent", TokenScope.Write, CredentialVersionUnderTest, TestSession, ct);

        // Act
        var sessions = await CreateStore(context, Now).ListActive(userId, ct);

        // Assert
        sessions.Should().HaveCount(2);
        sessions.Single(s => s.Id == session.SessionId).ClientId.Should().Be(ClientId);
    }
}
