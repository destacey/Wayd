using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq.AutoMock;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Identity.Users;
using Wayd.Infrastructure.RateLimiting;

namespace Wayd.Infrastructure.Tests.Sut.RateLimiting;

/// <summary>
/// Asserts how a signed-in user's rate-limit tier is read from their permissions and remembered, and that reading it
/// can never fail the request it is read for.
/// </summary>
public sealed class RateLimitTierResolverTests
{
    private const string UserId = "user-1";
    private const string ElevatedPermission = "Permissions.RateLimits.Elevated";

    private readonly AutoMocker _mocker = new();
    private readonly RateLimitSettings _settings = new();

    public RateLimitTierResolverTests()
    {
        _mocker.Use<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));
        _mocker.GetMock<IOptionsMonitor<RateLimitSettings>>().Setup(m => m.CurrentValue).Returns(() => _settings);
    }

    [Fact]
    public async Task Resolve_IsStandardWithoutReadingPermissions_ForAnAnonymousRequest()
    {
        // Arrange
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var tier = await CreateResolver().Resolve(anonymous, TestContext.Current.CancellationToken);

        // Assert
        tier.Should().Be(RateLimitTier.Standard);
        _mocker.GetMock<IUserService>().Verify(
            s => s.HasPermissionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Resolve_ReadsTheTierFromTheUsersPermissions_OnACacheMiss(bool hasPermission)
    {
        // Arrange
        HasElevatedPermission(hasPermission);
        var expected = hasPermission ? RateLimitTier.Elevated : RateLimitTier.Standard;

        // Act
        var tier = await CreateResolver().Resolve(SignedIn(), TestContext.Current.CancellationToken);

        // Assert
        tier.Should().Be(expected);
        _mocker.GetMock<IUserService>().Verify(
            s => s.HasPermissionAsync(UserId, ElevatedPermission, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Resolve_RemembersTheTier_OnACacheHit()
    {
        // Arrange — elevated when first read, then revoked, which the remembered tier hides until it expires
        HasElevatedPermission(true);
        var resolver = CreateResolver();
        await resolver.Resolve(SignedIn(), TestContext.Current.CancellationToken);
        HasElevatedPermission(false);

        // Act
        var tier = await resolver.Resolve(SignedIn(), TestContext.Current.CancellationToken);

        // Assert
        tier.Should().Be(RateLimitTier.Elevated);
        _mocker.GetMock<IUserService>().Verify(
            s => s.HasPermissionAsync(UserId, ElevatedPermission, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Resolve_RemembersEachUserSeparately()
    {
        // Arrange
        _mocker.GetMock<IUserService>()
            .Setup(s => s.HasPermissionAsync(UserId, ElevatedPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var resolver = CreateResolver();
        await resolver.Resolve(SignedIn(), TestContext.Current.CancellationToken);

        // Act
        var other = await resolver.Resolve(SignedIn("user-2"), TestContext.Current.CancellationToken);

        // Assert
        other.Should().Be(RateLimitTier.Standard);
    }

    [Fact]
    public async Task Resolve_ReadsEveryTime_WhenTheCacheLifetimeIsZero()
    {
        // Arrange
        _settings.TierCacheSeconds = 0;
        HasElevatedPermission(true);
        var resolver = CreateResolver();
        await resolver.Resolve(SignedIn(), TestContext.Current.CancellationToken);
        HasElevatedPermission(false);

        // Act
        var tier = await resolver.Resolve(SignedIn(), TestContext.Current.CancellationToken);

        // Assert
        tier.Should().Be(RateLimitTier.Standard);
        _mocker.GetMock<IUserService>().Verify(
            s => s.HasPermissionAsync(UserId, ElevatedPermission, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Resolve_IsStandard_ForAUserWhoNoLongerExists()
    {
        // Arrange
        _mocker.GetMock<IUserService>()
            .Setup(s => s.HasPermissionAsync(UserId, ElevatedPermission, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("User Not Found."));

        // Act
        var tier = await CreateResolver().Resolve(SignedIn(), TestContext.Current.CancellationToken);

        // Assert
        tier.Should().Be(RateLimitTier.Standard);
    }

    [Fact]
    public async Task Resolve_IsStandardAndNotRemembered_WhenThePermissionsCannotBeRead()
    {
        // Arrange — the first read fails, as it would with the database down, and the second succeeds
        _mocker.GetMock<IUserService>()
            .SetupSequence(s => s.HasPermissionAsync(UserId, ElevatedPermission, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The database is unavailable."))
            .ReturnsAsync(true);
        var resolver = CreateResolver();
        var failed = await resolver.Resolve(SignedIn(), TestContext.Current.CancellationToken);

        // Act
        var next = await resolver.Resolve(SignedIn(), TestContext.Current.CancellationToken);

        // Assert
        failed.Should().Be(RateLimitTier.Standard);
        next.Should().Be(RateLimitTier.Elevated, "a failed read must not be remembered");
    }

    [Fact]
    public async Task Resolve_LetsACancelledRequestFailAsCancelled()
    {
        // Arrange
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        _mocker.GetMock<IUserService>()
            .Setup(s => s.HasPermissionAsync(UserId, ElevatedPermission, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cancelled.Token));

        // Act
        var resolve = async () => await CreateResolver().Resolve(SignedIn(), cancelled.Token);

        // Assert
        await resolve.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void TierOf_IsTheStoredTier()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Items[RateLimitTierResolver.ItemKey] = RateLimitTier.Elevated;

        // Act
        var tier = RateLimitTierResolver.TierOf(context);

        // Assert
        tier.Should().Be(RateLimitTier.Elevated);
    }

    [Fact]
    public void TierOf_IsStandard_WhenNoTierWasResolved()
    {
        // Arrange
        var context = new DefaultHttpContext();

        // Act
        var tier = RateLimitTierResolver.TierOf(context);

        // Assert
        tier.Should().Be(RateLimitTier.Standard);
    }

    private RateLimitTierResolver CreateResolver() => _mocker.CreateInstance<RateLimitTierResolver>();

    private void HasElevatedPermission(bool granted) =>
        _mocker.GetMock<IUserService>()
            .Setup(s => s.HasPermissionAsync(It.IsAny<string>(), ElevatedPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(granted);

    private static ClaimsPrincipal SignedIn(string userId = UserId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], authenticationType: "Test"));
}
