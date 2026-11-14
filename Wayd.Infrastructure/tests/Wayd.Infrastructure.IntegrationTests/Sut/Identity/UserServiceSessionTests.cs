using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Identity;
using Wayd.Common.Application.Identity.OidcProviders;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Authorization;
using Wayd.Infrastructure.Auth.Local;
using Wayd.Infrastructure.Identity;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Identity;

/// <summary>
/// A credential change and the sessions it ends are written in one transaction. The unit tests' stores apply each
/// change as it is made, so only a real database shows the two committing, or rolling back, together.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class UserServiceSessionTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 14, 12, 0);
    private const string UserId = "00000000-0000-0000-0000-000000000011";
    private const string Password = "Session-Test-Passw0rd";
    private const string NewPassword = "Session-Test-Passw0rd-2";

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task DeactivateUserAsync_CommitsTheDeactivationAndTheEndedSessionsTogether()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await Seed(ct);
        await using var scope = provider.CreateAsyncScope();
        var sut = CreateSut(scope.ServiceProvider, store => store);

        // Act
        var result = await sut.DeactivateUserAsync(new DeactivateUserCommand(UserId), ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await using var check = _fixture.CreateContext();
        (await check.Users.AsNoTracking().SingleAsync(u => u.Id == UserId, ct)).IsActive.Should().BeFalse();
        (await check.UserRefreshTokens.AsNoTracking().SingleAsync(t => t.UserId == UserId, ct)).RevokedReason
            .Should().Be(UserRefreshTokenRevokeReasons.Deactivated);
    }

    [Fact]
    public async Task DeactivateUserAsync_LeavesTheUserActive_WhenEndingTheirSessionsFails()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await Seed(ct);
        await using var scope = provider.CreateAsyncScope();
        var sut = CreateSut(scope.ServiceProvider, store => new FailingRevocation(store));

        // Act
        var act = () => sut.DeactivateUserAsync(new DeactivateUserCommand(UserId), ct);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await using var check = _fixture.CreateContext();
        (await check.Users.AsNoTracking().SingleAsync(u => u.Id == UserId, ct)).IsActive.Should().BeTrue();
        (await check.UserRefreshTokens.AsNoTracking().SingleAsync(t => t.UserId == UserId, ct)).RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task ChangePasswordAsync_CommitsThePasswordTheClearedFlagAndTheEndedSessionsTogether()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await Seed(ct, mustChangePassword: true);
        await using var scope = provider.CreateAsyncScope();
        var sut = CreateSut(scope.ServiceProvider, store => store);

        // Act
        var result = await sut.ChangePasswordAsync(UserId, new ChangePasswordCommand(Password, NewPassword), ct);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await using var after = await Services();
        await using var afterScope = after.CreateAsyncScope();
        var users = afterScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(UserId);
        (await users.CheckPasswordAsync(user!, NewPassword)).Should().BeTrue();
        user!.MustChangePassword.Should().BeFalse();
        await using var check = _fixture.CreateContext();
        (await check.UserRefreshTokens.AsNoTracking().SingleAsync(t => t.UserId == UserId, ct)).RevokedReason
            .Should().Be(UserRefreshTokenRevokeReasons.PasswordChanged);
    }

    [Fact]
    public async Task ChangePasswordAsync_KeepsTheOldPassword_WhenEndingTheSessionsFails()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await Seed(ct);
        await using var scope = provider.CreateAsyncScope();
        var sut = CreateSut(scope.ServiceProvider, store => new FailingRevocation(store));

        // Act
        var act = () => sut.ChangePasswordAsync(UserId, new ChangePasswordCommand(Password, NewPassword), ct);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await using var after = await Services();
        await using var afterScope = after.CreateAsyncScope();
        var users = afterScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        (await users.CheckPasswordAsync((await users.FindByIdAsync(UserId))!, Password)).Should().BeTrue();
        await using var check = _fixture.CreateContext();
        (await check.UserRefreshTokens.AsNoTracking().SingleAsync(t => t.UserId == UserId, ct)).RevokedAt.Should().BeNull();
    }

    /// <summary>
    /// Clears the identity tables, creates a local user with <see cref="Password"/> and one live session, and answers a
    /// provider for the service.
    /// </summary>
    private async Task<ServiceProvider> Seed(CancellationToken cancellationToken, bool mustChangePassword = false)
    {
        await _fixture.ResetIdentityData(cancellationToken);

        var provider = await Services();
        await using (var scope = provider.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var created = await users.CreateAsync(new ApplicationUser
            {
                Id = UserId,
                UserName = "session@acme.example",
                Email = "session@acme.example",
                FirstName = "Sam",
                LastName = "Session",
                IsActive = true,
                LoginProvider = LoginProviders.Wayd,
                MustChangePassword = mustChangePassword,
            }, Password);
            created.Succeeded.Should().BeTrue(string.Join(", ", created.Errors.Select(e => e.Description)));
        }

        await using var context = _fixture.CreateContext();
        await new RefreshTokenStore(context, Clock(), new ConfigurationBuilder().Build(), NullLogger<RefreshTokenStore>.Instance)
            .Issue(UserId, "credential-version-1", SessionContext.None, cancellationToken);

        return provider;
    }

    /// <summary>A provider whose identity stores use a context of the fixture's database.</summary>
    private Task<ServiceProvider> Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => _fixture.CreateContext());
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<WaydDbContext>();
        return Task.FromResult(services.BuildServiceProvider());
    }

    /// <summary>
    /// A service over the scope's context with the real identity and session stores, the latter passed through
    /// <paramref name="wrap"/>.
    /// </summary>
    private static UserService CreateSut(IServiceProvider provider, Func<IRefreshTokenStore, IRefreshTokenStore> wrap)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns("integration-test-admin");
        var db = provider.GetRequiredService<WaydDbContext>();

        return new UserService(
            NullLogger<UserService>.Instance,
            null!, // SignInManager - not used here
            provider.GetRequiredService<UserManager<ApplicationUser>>(),
            provider.GetRequiredService<RoleManager<ApplicationRole>>(),
            db,
            new Mock<IDispatcher>().Object,
            Clock(),
            currentUser.Object,
            new UserIdentityStore(db, NullLogger<UserIdentityStore>.Instance),
            new Mock<IOidcProviderRegistry>().Object,
            wrap(new RefreshTokenStore(db, Clock(), new ConfigurationBuilder().Build(), NullLogger<RefreshTokenStore>.Instance)));
    }

    private static IDateTimeProvider Clock()
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(d => d.Now).Returns(Now);
        return clock.Object;
    }

    /// <summary>A session store whose <see cref="IRefreshTokenStore.RevokeAll"/> fails, as a lost connection would.</summary>
    private sealed class FailingRevocation(IRefreshTokenStore inner) : IRefreshTokenStore
    {
        public Task RevokeAll(string userId, string reason, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Revocation failed.");

        public Task<string> Issue(string userId, string credentialVersion, SessionContext context, CancellationToken cancellationToken) => inner.Issue(userId, credentialVersion, context, cancellationToken);
        public Task<RefreshRotationResult> Rotate(string userId, string presentedToken, string credentialVersion, SessionContext context, CancellationToken cancellationToken) => inner.Rotate(userId, presentedToken, credentialVersion, context, cancellationToken);
        public Task<ClientSession> IssueForClient(Guid sessionId, string userId, string clientId, string clientName, TokenScope scope, string credentialVersion, SessionContext context, CancellationToken cancellationToken) =>
            inner.IssueForClient(sessionId, userId, clientId, clientName, scope, credentialVersion, context, cancellationToken);
        public Task<ClientRotationResult> RotateForClient(string clientId, string presentedToken, Func<string, CancellationToken, Task<string?>> credentialVersionOf, SessionContext context, CancellationToken cancellationToken) =>
            inner.RotateForClient(clientId, presentedToken, credentialVersionOf, context, cancellationToken);
        public Task<IReadOnlyList<UserRefreshToken>> ListActive(string userId, CancellationToken cancellationToken) => inner.ListActive(userId, cancellationToken);
        public Task<Guid?> FindSessionId(string userId, string presentedToken, CancellationToken cancellationToken) => inner.FindSessionId(userId, presentedToken, cancellationToken);
        public Task<bool> Revoke(string userId, Guid sessionId, string reason, CancellationToken cancellationToken) => inner.Revoke(userId, sessionId, reason, cancellationToken);
    }
}
