using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Identity;
using Wayd.Common.Application.Identity.OidcProviders;
using Wayd.Common.Application.Identity.Tokens;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Identity;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Interfaces;
using Wayd.Common.Domain.Authorization;
using Wayd.Infrastructure.Auth;
using Wayd.Infrastructure.Auth.Local;
using Wayd.Infrastructure.Auth.Permissions;
using Wayd.Infrastructure.Auth.Oidc;
using Wayd.Infrastructure.Identity;
using Wayd.Infrastructure.Tests.Sut.Identity;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Data;

namespace Wayd.Infrastructure.Tests.Sut.Auth.Local;

public class TokenServiceTests
{
    private const string TestSecret = "ThisIsATestSecretKeyThatIsLongEnoughForHmacSha256!";
    private const string TestIssuer = "https://test.wayd.dev";
    private const string TestAudience = "https://test.api.wayd.dev";

    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
    private readonly Mock<RoleManager<ApplicationRole>> _mockRoleManager;
    private readonly IConfiguration _configuration;
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly Mock<ILogger<TokenService>> _mockLogger;
    private readonly Mock<IUserIdentityStore> _mockUserIdentityStore;
    private readonly Mock<IUserService> _mockUserService;
    private readonly Mock<IOidcTokenValidator> _mockOidcTokenValidator;
    private readonly Mock<IOidcProviderRegistry> _mockOidcProviderRegistry;
    private readonly FakeRefreshTokenStore _refreshTokenStore;
    private readonly FakeSessionContextAccessor _sessionContext = new();
    private readonly TokenService _sut;

    /// <summary>The one role, granting every permission, that makes its holders administrators.</summary>
    private readonly ApplicationRole _adminRole = new(ApplicationRoles.Admin) { Id = "role-admin" };

    public TokenServiceTests()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _mockSignInManager = new Mock<SignInManager<ApplicationUser>>(
            _mockUserManager.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>().Object,
            null!, null!, null!, null!);

        _mockRoleManager = new Mock<RoleManager<ApplicationRole>>(
            new Mock<IRoleStore<ApplicationRole>>().Object, null!, null!, null!, null!);
        _mockRoleManager.Setup(x => x.Roles).Returns(new[] { _adminRole }.AsQueryable().BuildMockDbSet().Object);
        _mockRoleManager.Setup(x => x.GetClaimsAsync(_adminRole))
            .ReturnsAsync([.. ApplicationPermissions.Admin.Select(p => new Claim(ApplicationClaims.Permission, p.Name))]);

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "SecuritySettings:LocalJwt:Secret", TestSecret },
                { "SecuritySettings:LocalJwt:Issuer", TestIssuer },
                { "SecuritySettings:LocalJwt:Audience", TestAudience },
                { "SecuritySettings:LocalJwt:TokenExpirationInMinutes", "60" },
                { "SecuritySettings:LocalJwt:RefreshTokenExpirationInDays", "7" },
            })
            .Build();

        _dateTimeProvider = new TestingDateTimeProvider(DateTime.UtcNow);
        _mockLogger = new Mock<ILogger<TokenService>>();
        _mockUserIdentityStore = new Mock<IUserIdentityStore>();
        _mockUserService = new Mock<IUserService>();
        _mockOidcTokenValidator = new Mock<IOidcTokenValidator>();
        _mockOidcProviderRegistry = new Mock<IOidcProviderRegistry>();

        // Default: user has no permissions. Individual tests can override.
        _mockUserService
            .Setup(s => s.GetPermissionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        // Default: registry returns no enabled OIDC providers so the local-only
        // shape is the baseline. Tests that care about the OIDC list shape mutate
        // this directly.
        _mockOidcProviderRegistry
            .Setup(r => r.GetEnabled(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<OidcProvider>)Array.Empty<OidcProvider>());

        _refreshTokenStore = new FakeRefreshTokenStore();
        _mockUserManager.Object.PasswordHasher = new PasswordHasher<ApplicationUser>();
        _mockUserManager.Object.Options = new IdentityOptions();

        _sut = new TokenService(
            _mockUserManager.Object,
            _mockSignInManager.Object,
            _mockRoleManager.Object,
            _configuration,
            _dateTimeProvider,
            _mockUserIdentityStore.Object,
            _mockUserService.Object,
            _mockOidcTokenValidator.Object,
            _mockOidcProviderRegistry.Object,
            _refreshTokenStore,
            _sessionContext,
            _mockLogger.Object);
    }

    private static ApplicationUser CreateLocalUser(string id = "user-1", string userName = "testuser", bool isActive = true)
    {
        return new ApplicationUser
        {
            Id = id,
            UserName = userName,
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            IsActive = isActive,
            LoginProvider = LoginProviders.Wayd,
        };
    }

    private void SeedActiveWaydIdentity(string userId)
    {
        _mockUserIdentityStore
            .Setup(s => s.ExistsActive(userId, LoginProviders.Wayd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    #region GetTokenAsync

    [Fact]
    public async Task GetTokenAsync_ShouldReturnTokenResponse_WhenCredentialsAreValid()
    {
        // Arrange
        var user = CreateLocalUser();
        SeedActiveWaydIdentity(user.Id);
        var command = new LoginCommand("testuser", "Password123!");

        _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Password123!", true))
            .ReturnsAsync(SignInResult.Success);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _sut.GetTokenAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Token.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.TokenExpiresAt.Should().BeAfter(DateTime.UtcNow);

        // Verify the JWT contains expected claims
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.Token);
        jwt.Issuer.Should().Be(TestIssuer);
        jwt.Audiences.Should().Contain(TestAudience);
        jwt.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value.Should().Be("user-1");
        jwt.Claims.First(c => c.Type == ClaimTypes.Email).Value.Should().Be("test@example.com");
    }

    [Fact]
    public async Task GetTokenAsync_ShouldIncludeEmployeeIdClaim_WhenUserHasEmployee()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var user = CreateLocalUser();
        user.EmployeeId = employeeId;
        SeedActiveWaydIdentity(user.Id);
        var command = new LoginCommand("testuser", "Password123!");

        _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Password123!", true))
            .ReturnsAsync(SignInResult.Success);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _sut.GetTokenAsync(command, TestContext.Current.CancellationToken);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.Token);
        jwt.Claims.First(c => c.Type == "EmployeeId").Value.Should().Be(employeeId.ToString());
    }

    [Fact]
    public async Task GetTokenAsync_ShouldThrowUnauthorized_WhenUserNotFound()
    {
        // Arrange
        _mockUserManager.Setup(x => x.FindByNameAsync("unknown")).ReturnsAsync((ApplicationUser?)null);
        var command = new LoginCommand("unknown", "Password123!");

        // Act
        var act = () => _sut.GetTokenAsync(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage(TokenService.SignInRefused);
    }

    [Fact]
    public async Task GetTokenAsync_ShouldThrowUnauthorized_WhenUserIsInactive()
    {
        // Arrange
        var user = CreateLocalUser(isActive: false);
        _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Password123!", true))
            .ReturnsAsync(SignInResult.Success);
        var command = new LoginCommand("testuser", "Password123!");

        // Act
        var act = () => _sut.GetTokenAsync(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage(TokenService.SignInRefused);
    }

    [Fact]
    public async Task GetTokenAsync_ShouldThrowUnauthorized_WhenUserIsNotLocalProvider()
    {
        // Arrange
        var user = CreateLocalUser();
        user.LoginProvider = LoginProviders.MicrosoftEntraId;
        _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        var command = new LoginCommand("testuser", "Password123!");

        // Act
        var act = () => _sut.GetTokenAsync(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage(TokenService.SignInRefused);
    }

    [Fact]
    public async Task GetTokenAsync_ShouldThrowUnauthorized_WhenPasswordIsInvalid()
    {
        // Arrange
        var user = CreateLocalUser();
        _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "wrong", true))
            .ReturnsAsync(SignInResult.Failed);
        var command = new LoginCommand("testuser", "wrong");

        // Act
        var act = () => _sut.GetTokenAsync(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage(TokenService.SignInRefused);
    }

    [Fact]
    public async Task GetTokenAsync_ShouldThrowUnauthorized_WhenUserIsLockedOut()
    {
        // Arrange
        var user = CreateLocalUser();
        _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Password123!", true))
            .ReturnsAsync(SignInResult.LockedOut);
        var command = new LoginCommand("testuser", "Password123!");

        // Act
        var act = () => _sut.GetTokenAsync(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*locked*");
    }

    [Fact]
    public async Task GetTokenAsync_ShouldRecordTheLockout_WhenThisAttemptTriggersIt()
    {
        // Arrange - the sign-in manager applies the lockout and saves it; the service records it.
        var user = CreateLocalUser();
        var lockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15);
        _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);
        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "wrong", true))
            .Callback(() => user.LockoutEnd = lockoutEnd)
            .ReturnsAsync(SignInResult.LockedOut);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        // Act
        var act = () => _sut.GetTokenAsync(new LoginCommand("testuser", "wrong"), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>().WithMessage("*locked*");
        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
        var lockedOut = ((IEntity)user).DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ApplicationUserLockedOutEvent>().Subject;
        lockedOut.LockedUntil.Should().Be(Instant.FromDateTimeOffset(lockoutEnd));
        lockedOut.Actor.Should().Be(EventActor.System);
    }

    [Fact]
    public async Task GetTokenAsync_ShouldNotRecordTheLockoutAgain_WhenAlreadyLockedOut()
    {
        // Arrange
        var user = CreateLocalUser();
        user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15);
        _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(true);
        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Password123!", true))
            .ReturnsAsync(SignInResult.LockedOut);

        // Act
        var act = () => _sut.GetTokenAsync(new LoginCommand("testuser", "Password123!"), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>().WithMessage("*locked*");
        _mockUserManager.Verify(x => x.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        ((IEntity)user).DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTokenAsync_ShouldThrowUnauthorized_WhenNoActiveWaydIdentity()
    {
        // Credentials valid, user active, but the UserIdentity row has been deactivated
        // (admin-revoked local login). Must fail without issuing a token.
        var user = CreateLocalUser();
        _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Password123!", true))
            .ReturnsAsync(SignInResult.Success);
        _mockUserIdentityStore
            .Setup(s => s.ExistsActive(user.Id, LoginProviders.Wayd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new LoginCommand("testuser", "Password123!");

        var act = () => _sut.GetTokenAsync(command, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid credentials.");
        _mockUserManager.Verify(x => x.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task GetTokenAsync_ShouldUpdateRefreshToken_WhenLoginSucceeds()
    {
        // Arrange
        var user = CreateLocalUser();
        SeedActiveWaydIdentity(user.Id);
        var command = new LoginCommand("testuser", "Password123!");

        _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Password123!", true))
            .ReturnsAsync(SignInResult.Success);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _sut.GetTokenAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        _refreshTokenStore.ActiveSessionCount(user.Id).Should().Be(1, "signing in opens a session");
    }

    [Fact]
    public async Task GetTokenAsync_IssuesUnderTheCredentialVersionOfTheUserItChecked()
    {
        // Arrange — the stamp the sign-in saw, so a password changed while it ran leaves what it issues unusable
        var user = CreateLocalUser();
        user.SecurityStamp = "stamp-when-checked";
        SeedActiveWaydIdentity(user.Id);
        _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Password123!", true))
            .ReturnsAsync(SignInResult.Success);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _sut.GetTokenAsync(new LoginCommand("testuser", "Password123!"), TestContext.Current.CancellationToken);

        // Assert
        var expected = CredentialVersion.Of("stamp-when-checked");
        new JwtSecurityTokenHandler().ReadJwtToken(result.Token).Claims.Single(c => c.Type == CredentialVersion.ClaimType).Value
            .Should().Be(expected);
        _refreshTokenStore.CredentialVersionFor(result.RefreshToken).Should().Be(expected);
    }

    #endregion

    #region Temporary passwords, lockout and the last administrator

    /// <summary>Arranges a successful password check for <paramref name="user"/>, by sign-in manager and user manager alike.</summary>
    private void PasswordIsRight(ApplicationUser user)
    {
        _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Password123!", true)).ReturnsAsync(SignInResult.Success);
        _mockUserManager.Setup(x => x.CheckPasswordAsync(user, "Password123!")).ReturnsAsync(true);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
    }

    /// <summary>Makes <paramref name="user"/> the only active administrator.</summary>
    private void IsTheLastAdministrator(ApplicationUser user) => AdministratorsAre(user, [user]);

    /// <summary>Makes <paramref name="user"/> an administrator among <paramref name="administrators"/>.</summary>
    private void AdministratorsAre(ApplicationUser user, ApplicationUser[] administrators)
    {
        _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync([ApplicationRoles.Admin]);
        _mockUserManager.Setup(x => x.GetUsersInRoleAsync(ApplicationRoles.Admin)).ReturnsAsync(administrators);
    }

    [Fact]
    public async Task GetTokenAsync_RestrictsTheToken_WhenTheUserMustChangeTheirPassword()
    {
        // Arrange
        var user = CreateLocalUser();
        user.MustChangePassword = true;
        user.TemporaryPasswordSetAt = _dateTimeProvider.Now;
        SeedActiveWaydIdentity(user.Id);
        PasswordIsRight(user);

        // Act
        var result = await _sut.GetTokenAsync(new LoginCommand("testuser", "Password123!"), TestContext.Current.CancellationToken);

        // Assert
        result.MustChangePassword.Should().BeTrue();
        new JwtSecurityTokenHandler().ReadJwtToken(result.Token).Claims
            .Should().Contain(c => c.Type == PasswordChangeRequirement.ClaimType && c.Value == "true");
    }

    [Fact]
    public async Task GetTokenAsync_DoesNotRestrictTheToken_OfAUserWithAPasswordOfTheirOwn()
    {
        // Arrange
        var user = CreateLocalUser();
        SeedActiveWaydIdentity(user.Id);
        PasswordIsRight(user);

        // Act
        var result = await _sut.GetTokenAsync(new LoginCommand("testuser", "Password123!"), TestContext.Current.CancellationToken);

        // Assert
        new JwtSecurityTokenHandler().ReadJwtToken(result.Token).Claims.Should().NotContain(c => c.Type == PasswordChangeRequirement.ClaimType);
    }

    [Theory]
    [InlineData(6, false)]
    [InlineData(7, true)]
    public async Task GetTokenAsync_RefusesATemporaryPassword_OnceItIsAWeekOld(int daysOld, bool refused)
    {
        // Arrange
        var user = CreateLocalUser();
        user.MustChangePassword = true;
        user.TemporaryPasswordSetAt = _dateTimeProvider.Now.Minus(Duration.FromDays(daysOld));
        SeedActiveWaydIdentity(user.Id);
        PasswordIsRight(user);

        // Act
        var act = () => _sut.GetTokenAsync(new LoginCommand("testuser", "Password123!"), TestContext.Current.CancellationToken);

        // Assert
        if (refused)
            await act.Should().ThrowAsync<UnauthorizedException>().WithMessage(TokenService.TemporaryPasswordExpired);
        else
            await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetTokenAsync_AcceptsATemporaryPasswordWithNoRecordedAge()
    {
        // Arrange — set before the time was recorded
        var user = CreateLocalUser();
        user.MustChangePassword = true;
        SeedActiveWaydIdentity(user.Id);
        PasswordIsRight(user);

        // Act
        var act = () => _sut.GetTokenAsync(new LoginCommand("testuser", "Password123!"), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RefreshTokenAsync_RefusesAUserWhoseTemporaryPasswordHasExpired()
    {
        // Arrange — signed in on day six, refreshing on day eight
        var user = CreateLocalUser();
        user.MustChangePassword = true;
        user.TemporaryPasswordSetAt = _dateTimeProvider.Now.Minus(Duration.FromDays(6));
        SeedActiveWaydIdentity(user.Id);
        PasswordIsRight(user);
        var signedIn = await _sut.GetTokenAsync(new LoginCommand("testuser", "Password123!"), TestContext.Current.CancellationToken);
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _dateTimeProvider.Advance(Duration.FromDays(2));

        // Act
        var act = () => _sut.RefreshTokenAsync(new RefreshTokenCommand(signedIn.Token, signedIn.RefreshToken!), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>().WithMessage(TokenService.TemporaryPasswordExpired);
    }

    [Fact]
    public async Task GetTokenAsync_RefusesTheRightPasswordDuringALockout_InTheWordsOfAWrongGuess()
    {
        // Arrange — the sign-in manager refuses a locked-out account whatever the password; a reply that differed for
        // the right one would make the lockout an uncounted guessing oracle
        var user = CreateLocalUser();
        SeedActiveWaydIdentity(user.Id);
        _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(true);
        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, It.IsAny<string>(), true)).ReturnsAsync(SignInResult.LockedOut);

        // Act
        var right = () => _sut.GetTokenAsync(new LoginCommand("testuser", "Password123!"), TestContext.Current.CancellationToken);
        var wrong = () => _sut.GetTokenAsync(new LoginCommand("testuser", "WrongPassword1!"), TestContext.Current.CancellationToken);

        // Assert
        await right.Should().ThrowAsync<UnauthorizedException>().WithMessage(TokenService.SignInRefused);
        await wrong.Should().ThrowAsync<UnauthorizedException>().WithMessage(TokenService.SignInRefused);
    }

    [Fact]
    public async Task GetTokenAsync_NeverLocksOutTheLastAdministrator_ButThrottlesTheirFailures()
    {
        // Arrange
        var user = CreateLocalUser();
        SeedActiveWaydIdentity(user.Id);
        IsTheLastAdministrator(user);
        _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.CheckPasswordAsync(user, It.IsAny<string>())).ReturnsAsync(false);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        var attempt = () => _sut.GetTokenAsync(new LoginCommand("testuser", "WrongPassword1!"), TestContext.Current.CancellationToken);
        for (var failure = 0; failure < 5; failure++)
            await attempt.Should().ThrowAsync<UnauthorizedException>().WithMessage(TokenService.SignInRefused);

        // Act — straight after the fifth failure, before the one-second hold has passed
        var tooSoon = await attempt.Should().ThrowAsync<UnauthorizedException>();

        // Assert
        tooSoon.WithMessage(TokenService.SignInRefused);
        user.AccessFailedCount.Should().Be(5);
        user.LockoutEnd.Should().Be(_dateTimeProvider.Now.ToDateTimeOffset().AddSeconds(1), "a hold of seconds, not a lockout");
        _mockUserManager.Verify(x => x.CheckPasswordAsync(user, It.IsAny<string>()), Times.Exactly(5), "an attempt before the hold ends is not checked");
        _mockSignInManager.Verify(x => x.CheckPasswordSignInAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task GetTokenAsync_ChecksTheLastAdministratorsPasswordAgain_OnceTheHoldEnds()
    {
        // Arrange
        var user = CreateLocalUser();
        user.AccessFailedCount = 5;
        user.LockoutEnd = _dateTimeProvider.Now.ToDateTimeOffset().AddSeconds(1);
        SeedActiveWaydIdentity(user.Id);
        IsTheLastAdministrator(user);
        PasswordIsRight(user);
        _dateTimeProvider.Advance(Duration.FromSeconds(2));

        // Act
        var result = await _sut.GetTokenAsync(new LoginCommand("testuser", "Password123!"), TestContext.Current.CancellationToken);

        // Assert
        result.Token.Should().NotBeNullOrWhiteSpace();
        user.AccessFailedCount.Should().Be(0);
        user.LockoutEnd.Should().BeNull();
    }

    [Fact]
    public async Task GetTokenAsync_RefusesTheLastAdministratorsAttempt_WhenAnotherRequestTookIt()
    {
        // Arrange — past the threshold an attempt is taken by a save, which loses to a parallel guess
        var user = CreateLocalUser();
        user.AccessFailedCount = 6;
        SeedActiveWaydIdentity(user.Id);
        IsTheLastAdministrator(user);
        _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Failed(new IdentityErrorDescriber().ConcurrencyFailure()));

        // Act
        var act = () => _sut.GetTokenAsync(new LoginCommand("testuser", "Password123!"), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>().WithMessage(TokenService.SignInRefused);
        _mockUserManager.Verify(x => x.CheckPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetTokenAsync_ProtectsTheOnlyAdministrator_WhenTheirFullAccessComesFromARoleOtherThanAdmin()
    {
        // Arrange — an administrator is whoever holds every permission, whichever role grants it.
        var fullAccess = new ApplicationRole("Full Access") { Id = "role-full" };
        _mockRoleManager.Setup(x => x.Roles).Returns(new[] { _adminRole, fullAccess }.AsQueryable().BuildMockDbSet().Object);
        _mockRoleManager.Setup(x => x.GetClaimsAsync(fullAccess))
            .ReturnsAsync([.. ApplicationPermissions.Admin.Select(p => new Claim(ApplicationClaims.Permission, p.Name))]);
        var user = CreateLocalUser();
        SeedActiveWaydIdentity(user.Id);
        _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(["Full Access"]);
        _mockUserManager.Setup(x => x.GetUsersInRoleAsync("Full Access")).ReturnsAsync([user]);
        PasswordIsRight(user);

        // Act
        await _sut.GetTokenAsync(new LoginCommand("testuser", "Password123!"), TestContext.Current.CancellationToken);

        // Assert — a protected administrator is checked directly, never through the sign-in manager's lockout
        _mockSignInManager.Verify(x => x.CheckPasswordSignInAsync(user, "Password123!", true), Times.Never);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    public async Task GetTokenAsync_ProtectsAnAdministrator_OnlyWhenNoOtherCouldUnlockThem(bool otherIsActive, bool otherIsLockedOut, bool isProtected)
    {
        // Arrange — another administrator who is inactive or locked out cannot unlock anyone
        var user = CreateLocalUser();
        SeedActiveWaydIdentity(user.Id);
        var other = CreateLocalUser(id: "admin-2", userName: "other-admin", isActive: otherIsActive);
        if (otherIsLockedOut)
            other.LockoutEnd = _dateTimeProvider.Now.ToDateTimeOffset().AddMinutes(10);
        AdministratorsAre(user, [user, other]);
        PasswordIsRight(user);

        // Act
        await _sut.GetTokenAsync(new LoginCommand("testuser", "Password123!"), TestContext.Current.CancellationToken);

        // Assert — a protected administrator is checked directly, anyone else through the sign-in manager's lockout
        _mockSignInManager.Verify(x => x.CheckPasswordSignInAsync(user, "Password123!", true), isProtected ? Times.Never() : Times.Once());
    }

    [Fact]
    public async Task GetTokenAsync_SignsInTheLastAdministrator_ThroughALockoutLeftFromBefore()
    {
        // Arrange — locked out while another administrator still existed
        var user = CreateLocalUser();
        user.AccessFailedCount = 2;
        user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(10);
        SeedActiveWaydIdentity(user.Id);
        IsTheLastAdministrator(user);
        PasswordIsRight(user);

        // Act
        var result = await _sut.GetTokenAsync(new LoginCommand("testuser", "Password123!"), TestContext.Current.CancellationToken);

        // Assert
        result.Token.Should().NotBeNullOrWhiteSpace();
        user.AccessFailedCount.Should().Be(0);
        user.LockoutEnd.Should().BeNull();
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 0)]
    [InlineData(5, 1)]
    [InlineData(6, 2)]
    [InlineData(9, 16)]
    [InlineData(10, 30)]
    [InlineData(50, 30)]
    public void LastAdministratorDelay_DoublesPastTheThreshold_UpToThirtySeconds(int failures, int seconds)
    {
        // Arrange — nothing beyond the arguments

        // Act
        var delay = LastAdministrator.Delay(failures, lockoutThreshold: 5);

        // Assert
        delay.Should().Be(TimeSpan.FromSeconds(seconds));
    }

    [Fact]
    public async Task RefreshTokenAsync_KeepsTheTokenRestricted_UntilThePasswordIsChanged()
    {
        // Arrange
        var user = CreateLocalUser();
        user.MustChangePassword = true;
        user.TemporaryPasswordSetAt = _dateTimeProvider.Now;
        SeedActiveWaydIdentity(user.Id);
        PasswordIsRight(user);
        var signedIn = await _sut.GetTokenAsync(new LoginCommand("testuser", "Password123!"), TestContext.Current.CancellationToken);
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);

        // Act
        var refreshed = await _sut.RefreshTokenAsync(new RefreshTokenCommand(signedIn.Token, signedIn.RefreshToken!), TestContext.Current.CancellationToken);

        // Assert
        new JwtSecurityTokenHandler().ReadJwtToken(refreshed.Token).Claims
            .Should().Contain(c => c.Type == PasswordChangeRequirement.ClaimType && c.Value == "true");
    }

    [Fact]
    public async Task GetTokenAsync_ChecksAPasswordHash_ForALockedOutAccount()
    {
        // Arrange — the sign-in manager refuses a locked-out account before hashing, which would make it the fastest refusal
        var hasher = new Mock<IPasswordHasher<ApplicationUser>>();
        hasher.Setup(h => h.HashPassword(It.IsAny<ApplicationUser>(), It.IsAny<string>())).Returns("unknown-user-hash");
        _mockUserManager.Object.PasswordHasher = hasher.Object;
        var user = CreateLocalUser();
        _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(true);
        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, It.IsAny<string>(), true)).ReturnsAsync(SignInResult.LockedOut);

        // Act
        var act = () => _sut.GetTokenAsync(new LoginCommand("testuser", "Password123!"), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>().WithMessage(TokenService.SignInRefused);
        hasher.Verify(h => h.VerifyHashedPassword(It.IsAny<ApplicationUser>(), It.IsAny<string>(), "Password123!"), Times.Once);
    }

    [Fact]
    public async Task GetTokenAsync_ChecksAPasswordHash_ForAnUnknownUser()
    {
        // Arrange — so an unknown user takes as long to refuse as a wrong password
        var hasher = new Mock<IPasswordHasher<ApplicationUser>>();
        hasher.Setup(h => h.HashPassword(It.IsAny<ApplicationUser>(), It.IsAny<string>())).Returns("unknown-user-hash");
        _mockUserManager.Object.PasswordHasher = hasher.Object;
        _mockUserManager.Setup(x => x.FindByNameAsync("nobody")).ReturnsAsync((ApplicationUser?)null);

        // Act
        var act = () => _sut.GetTokenAsync(new LoginCommand("nobody", "Password123!"), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>().WithMessage(TokenService.SignInRefused);
        hasher.Verify(h => h.VerifyHashedPassword(It.IsAny<ApplicationUser>(), It.IsAny<string>(), "Password123!"), Times.Once);
    }

    #endregion

    #region RefreshTokenAsync


    [Fact]
    public async Task RefreshTokenAsync_ShouldReturnNewTokens_WhenRefreshTokenIsValid()
    {
        // Arrange - first get a valid token
        var user = CreateLocalUser();
        SeedActiveWaydIdentity(user.Id);
        var command = new LoginCommand("testuser", "Password123!");
        _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Password123!", true))
            .ReturnsAsync(SignInResult.Success);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var initialTokenResponse = await _sut.GetTokenAsync(command, TestContext.Current.CancellationToken);

        // The plaintext token exists only in the response — user.RefreshToken holds its hash.
        var currentRefreshToken = initialTokenResponse.RefreshToken;
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);

        // Advance time so the new token has a different expiry
        _dateTimeProvider.Advance(Duration.FromMinutes(5));

        var refreshCommand = new RefreshTokenCommand(initialTokenResponse.Token, currentRefreshToken!);

        // Act
        var result = await _sut.RefreshTokenAsync(refreshCommand, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Token.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Should().NotBe(currentRefreshToken, "a new refresh token should be generated");
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldThrowUnauthorized_WhenTheUsersCredentialsChangedSinceSignIn()
    {
        // Arrange
        var user = CreateLocalUser();
        user.SecurityStamp = "stamp-at-sign-in";
        SeedActiveWaydIdentity(user.Id);
        _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Password123!", true))
            .ReturnsAsync(SignInResult.Success);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        var signedIn = await _sut.GetTokenAsync(new LoginCommand("testuser", "Password123!"), TestContext.Current.CancellationToken);
        user.SecurityStamp = "stamp-after-a-password-change";
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);

        // Act
        var act = () => _sut.RefreshTokenAsync(new RefreshTokenCommand(signedIn.Token, signedIn.RefreshToken!), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>();
        _refreshTokenStore.RevokeReasons(user.Id).Should().Equal(UserRefreshTokenRevokeReasons.CredentialChanged);
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldThrowUnauthorized_WhenRefreshTokenDoesNotMatch()
    {
        // Arrange - get a valid JWT first
        var user = CreateLocalUser();
        SeedActiveWaydIdentity(user.Id);
        _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Password123!", true))
            .ReturnsAsync(SignInResult.Success);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var tokenResponse = await _sut.GetTokenAsync(new LoginCommand("testuser", "Password123!"), TestContext.Current.CancellationToken);
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);

        var refreshCommand = new RefreshTokenCommand(tokenResponse.Token, "wrong-refresh-token");

        // Act
        var act = () => _sut.RefreshTokenAsync(refreshCommand, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid or expired refresh token.");
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldThrowUnauthorized_WhenRefreshTokenIsExpired()
    {
        // Arrange
        var user = CreateLocalUser();
        SeedActiveWaydIdentity(user.Id);

        _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Password123!", true))
            .ReturnsAsync(SignInResult.Success);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var tokenResponse = await _sut.GetTokenAsync(new LoginCommand("testuser", "Password123!"), TestContext.Current.CancellationToken);

        // Must be the real token, not the stored hash: passing the hash would fail on
        // mismatch and the assertion would pass without the expiry check ever running.
        var currentRefreshToken = tokenResponse.RefreshToken;

        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);

        // The store owns lifetime now; expire the session's token there.
        _refreshTokenStore.ExpireToken(currentRefreshToken);

        var refreshCommand = new RefreshTokenCommand(tokenResponse.Token, currentRefreshToken!);

        // Act
        var act = () => _sut.RefreshTokenAsync(refreshCommand, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid or expired refresh token.");
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldThrowUnauthorized_WhenUserIsInactive()
    {
        // Arrange
        var user = CreateLocalUser();
        SeedActiveWaydIdentity(user.Id);

        _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Password123!", true))
            .ReturnsAsync(SignInResult.Success);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var tokenResponse = await _sut.GetTokenAsync(new LoginCommand("testuser", "Password123!"), TestContext.Current.CancellationToken);
        var currentRefreshToken = tokenResponse.RefreshToken;

        // Deactivate user after login
        user.IsActive = false;
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);

        var refreshCommand = new RefreshTokenCommand(tokenResponse.Token, currentRefreshToken!);

        // Act
        var act = () => _sut.RefreshTokenAsync(refreshCommand, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("User account is inactive.");
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldThrowUnauthorized_WhenWaydIdentityDeactivatedAfterLogin()
    {
        // Simulates admin revocation of the user's local login between issuing the
        // initial token and the refresh. The refresh must fail — otherwise a revoked
        // user could keep minting access tokens until the refresh-token TTL elapses.
        var user = CreateLocalUser();
        SeedActiveWaydIdentity(user.Id);

        _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Password123!", true))
            .ReturnsAsync(SignInResult.Success);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var tokenResponse = await _sut.GetTokenAsync(new LoginCommand("testuser", "Password123!"), TestContext.Current.CancellationToken);

        // The plaintext token exists only in the response — user.RefreshToken holds its hash.
        var currentRefreshToken = tokenResponse.RefreshToken;

        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);

        // Deactivate the Wayd identity after the initial token was issued.
        _mockUserIdentityStore
            .Setup(s => s.ExistsActive(user.Id, LoginProviders.Wayd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var refreshCommand = new RefreshTokenCommand(tokenResponse.Token, currentRefreshToken!);

        var act = () => _sut.RefreshTokenAsync(refreshCommand, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid credentials.");
    }

    #endregion

    #region Permission claims

    [Fact]
    public async Task GetTokenAsync_ShouldEmbedPermissionClaims_WhenUserHasPermissions()
    {
        var user = CreateLocalUser();
        SeedActiveWaydIdentity(user.Id);
        _mockUserService
            .Setup(s => s.GetPermissionsAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>
            {
                "Permissions.Projects.View",
                "Permissions.Projects.Manage",
                "Permissions.Teams.View",
            });

        _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Password123!", true))
            .ReturnsAsync(SignInResult.Success);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var result = await _sut.GetTokenAsync(new LoginCommand("testuser", "Password123!"), TestContext.Current.CancellationToken);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.Token);
        var permissionClaims = jwt.Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToList();
        permissionClaims.Should().BeEquivalentTo(
            "Permissions.Projects.View",
            "Permissions.Projects.Manage",
            "Permissions.Teams.View");
    }

    [Fact]
    public async Task GetTokenAsync_ShouldEmbedNoPermissionClaims_WhenUserHasNoPermissions()
    {
        // Default mock returns empty list — verify no permission claims are added.
        var user = CreateLocalUser();
        SeedActiveWaydIdentity(user.Id);

        _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Password123!", true))
            .ReturnsAsync(SignInResult.Success);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var result = await _sut.GetTokenAsync(new LoginCommand("testuser", "Password123!"), TestContext.Current.CancellationToken);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.Token);
        jwt.Claims.Where(c => c.Type == "permission").Should().BeEmpty();
    }

    [Fact]
    public async Task GetTokenAsync_ShouldEmbedLoginProviderClaim()
    {
        // The frontend reads loginProvider to drive provider-specific UX
        // (Change Password button, forced-password-change gate). A missing
        // claim silently disables those features.
        var user = CreateLocalUser();
        SeedActiveWaydIdentity(user.Id);

        _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Password123!", true))
            .ReturnsAsync(SignInResult.Success);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var result = await _sut.GetTokenAsync(new LoginCommand("testuser", "Password123!"), TestContext.Current.CancellationToken);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.Token);
        jwt.Claims.First(c => c.Type == "loginProvider").Value.Should().Be(LoginProviders.Wayd);
    }

    #endregion

    #region ExchangeTokenAsync

    private void SeedActiveEntraIdentity(string userId) =>
        _mockUserIdentityStore
            .Setup(s => s.ExistsActive(userId, LoginProviders.MicrosoftEntraId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

    private static ClaimsPrincipal CreateEntraPrincipal(string objectId = "entra-oid-1", string tenantId = "tenant-1", string email = "entra@example.com", string firstName = "Entra", string lastName = "User")
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", objectId),
            new Claim("tid", tenantId),
            new Claim(ClaimTypes.Upn, email),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, firstName + " " + lastName),
            new Claim(ClaimTypes.GivenName, firstName),
            new Claim(ClaimTypes.Surname, lastName),
        }, authenticationType: "Test");
        return new ClaimsPrincipal(identity);
    }

    private static ValidatedOidcToken EntraToken(ClaimsPrincipal principal) =>
        new(principal, new OidcProviderFaker().AsMicrosoftEntraId("tenant-1").WithName(LoginProviders.MicrosoftEntraId).Generate());

    private static ValidatedOidcToken GenericToken(ClaimsPrincipal principal, string name) =>
        new(principal, new OidcProviderFaker().AsGenericOidc().WithName(name).Generate());

    private static ApplicationUser CreateEntraUser(string id = "entra-user-1") =>
        new()
        {
            Id = id,
            UserName = "entra-user",
            Email = "entra@example.com",
            FirstName = "Entra",
            LastName = "User",
            IsActive = true,
            LoginProvider = LoginProviders.MicrosoftEntraId,
        };

    [Fact]
    public async Task ExchangeTokenAsync_ShouldReturnTokenResponse_WhenTokenIsValid()
    {
        var user = CreateEntraUser();
        var principal = CreateEntraPrincipal();

        _mockOidcTokenValidator
            .Setup(v => v.Validate(LoginProviders.MicrosoftEntraId, "entra-id-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(EntraToken(principal));
        _mockUserService
            .Setup(s => s.GetOrCreateFromPrincipalAsync(principal))
            .ReturnsAsync((user.Id, (string?)null));
        _mockUserManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        SeedActiveEntraIdentity(user.Id);

        var result = await _sut.ExchangeTokenAsync(
            new ExchangeTokenCommand(LoginProviders.MicrosoftEntraId, "entra-id-token"),
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result.Token.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.Token);
        jwt.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value.Should().Be(user.Id);
    }

    [Fact]
    public async Task ExchangeTokenAsync_ShouldEmbedPermissionClaims()
    {
        var user = CreateEntraUser();
        var principal = CreateEntraPrincipal();

        _mockOidcTokenValidator
            .Setup(v => v.Validate(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EntraToken(principal));
        _mockUserService
            .Setup(s => s.GetOrCreateFromPrincipalAsync(principal))
            .ReturnsAsync((user.Id, (string?)null));
        _mockUserService
            .Setup(s => s.GetPermissionsAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Permissions.Projects.View" });
        _mockUserManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        SeedActiveEntraIdentity(user.Id);

        var result = await _sut.ExchangeTokenAsync(
            new ExchangeTokenCommand(LoginProviders.MicrosoftEntraId, "token"),
            TestContext.Current.CancellationToken);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.Token);
        jwt.Claims.Where(c => c.Type == "permission").Select(c => c.Value).Should().Contain("Permissions.Projects.View");
    }

    [Fact]
    public async Task ExchangeTokenAsync_ShouldEmbedLoginProviderClaim()
    {
        // Same provider-aware UX hooks as the local-login path — verifying
        // the claim is also set on Entra-exchanged tokens.
        var user = CreateEntraUser();
        var principal = CreateEntraPrincipal();

        _mockOidcTokenValidator
            .Setup(v => v.Validate(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EntraToken(principal));
        _mockUserService
            .Setup(s => s.GetOrCreateFromPrincipalAsync(principal))
            .ReturnsAsync((user.Id, (string?)null));
        _mockUserManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        SeedActiveEntraIdentity(user.Id);

        var result = await _sut.ExchangeTokenAsync(
            new ExchangeTokenCommand(LoginProviders.MicrosoftEntraId, "token"),
            TestContext.Current.CancellationToken);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.Token);
        jwt.Claims.First(c => c.Type == "loginProvider").Value.Should().Be(LoginProviders.MicrosoftEntraId);
    }

    [Fact]
    public async Task ExchangeTokenAsync_ShouldThrowUnauthorized_WhenProviderRejectedByValidator()
    {
        // Unknown provider: the validator (backed by the registry) throws
        // UnauthorizedException for any name that doesn't map to an enabled row.
        // TokenService surfaces that as-is — the previous behavior of
        // short-circuiting before calling the validator is gone now that the
        // registry is the single source of truth for which providers exist.
        _mockOidcTokenValidator
            .Setup(v => v.Validate("Auth0", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedException("Invalid token."));

        var act = () => _sut.ExchangeTokenAsync(
            new ExchangeTokenCommand("Auth0", "some-token"),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UnauthorizedException>().WithMessage("Invalid token.");
        _mockUserService.Verify(s => s.GetOrCreateFromPrincipalAsync(It.IsAny<ClaimsPrincipal>()), Times.Never);
    }

    [Fact]
    public async Task ExchangeTokenAsync_ShouldThrowUnauthorized_WhenGenericOidcValidatesButNoMatchingUser()
    {
        // GenericOidc tokens are now fully validated and routed to
        // ResolveFromGenericOidcPrincipalAsync. When that method returns null
        // (no existing identity, no pending migration), TokenService rejects
        // with a user-facing message rather than a generic "Invalid token."
        var principal = CreateEntraPrincipal(); // claim shape doesn't matter for this path
        _mockOidcTokenValidator
            .Setup(v => v.Validate("Acme-Google", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GenericToken(principal, "Acme-Google"));
        _mockUserService
            .Setup(s => s.ResolveFromGenericOidcPrincipalAsync("Acme-Google", principal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((string Id, string? EmployeeId)?)null);

        var act = () => _sut.ExchangeTokenAsync(
            new ExchangeTokenCommand("Acme-Google", "valid-google-token"),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("No account found for this identity. Contact an administrator.");
        _mockUserService.Verify(s => s.GetOrCreateFromPrincipalAsync(It.IsAny<ClaimsPrincipal>()), Times.Never);
    }

    [Fact]
    public async Task ExchangeTokenAsync_ShouldThrowUnauthorized_WhenValidatorRejectsToken()
    {
        _mockOidcTokenValidator
            .Setup(v => v.Validate(LoginProviders.MicrosoftEntraId, "bad-token", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedException("Invalid token."));

        var act = () => _sut.ExchangeTokenAsync(
            new ExchangeTokenCommand(LoginProviders.MicrosoftEntraId, "bad-token"),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UnauthorizedException>().WithMessage("Invalid token.");
        _mockUserService.Verify(s => s.GetOrCreateFromPrincipalAsync(It.IsAny<ClaimsPrincipal>()), Times.Never);
    }

    [Fact]
    public async Task ExchangeTokenAsync_ShouldThrowUnauthorized_WhenUserIsInactive()
    {
        var user = CreateEntraUser();
        user.IsActive = false;
        var principal = CreateEntraPrincipal();

        _mockOidcTokenValidator
            .Setup(v => v.Validate(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EntraToken(principal));
        _mockUserService
            .Setup(s => s.GetOrCreateFromPrincipalAsync(principal))
            .ReturnsAsync((user.Id, (string?)null));
        _mockUserManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);

        var act = () => _sut.ExchangeTokenAsync(
            new ExchangeTokenCommand(LoginProviders.MicrosoftEntraId, "token"),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UnauthorizedException>().WithMessage("*deactivated*");
    }

    [Fact]
    public async Task ExchangeTokenAsync_ShouldThrowUnauthorized_WhenNoActiveEntraIdentity()
    {
        var user = CreateEntraUser();
        var principal = CreateEntraPrincipal();

        _mockOidcTokenValidator
            .Setup(v => v.Validate(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EntraToken(principal));
        _mockUserService
            .Setup(s => s.GetOrCreateFromPrincipalAsync(principal))
            .ReturnsAsync((user.Id, (string?)null));
        _mockUserManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
        // Note: SeedActiveEntraIdentity NOT called → ExistsActive returns default (false).

        var act = () => _sut.ExchangeTokenAsync(
            new ExchangeTokenCommand(LoginProviders.MicrosoftEntraId, "token"),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<UnauthorizedException>().WithMessage("Invalid credentials.");
    }

    [Fact]
    public async Task ExchangeTokenAsync_TakesTheEntraPath_WhenTheRequestSpellsTheEntraProviderInAnotherCase()
    {
        // Arrange
        var user = CreateEntraUser();
        var principal = CreateEntraPrincipal();
        _mockOidcTokenValidator
            .Setup(v => v.Validate("microsoftentraid", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EntraToken(principal));
        _mockUserService
            .Setup(s => s.GetOrCreateFromPrincipalAsync(principal))
            .ReturnsAsync((user.Id, (string?)null));
        _mockUserManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        SeedActiveEntraIdentity(user.Id);

        // Act
        var result = await _sut.ExchangeTokenAsync(
            new ExchangeTokenCommand("microsoftentraid", "token"),
            TestContext.Current.CancellationToken);

        // Assert
        result.Token.Should().NotBeNullOrWhiteSpace();
        _mockUserService.Verify(s => s.ResolveFromGenericOidcPrincipalAsync(
            It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExchangeTokenAsync_ResolvesUnderTheStoredName_WhenTheRequestSpellsAGenericProviderInAnotherCase()
    {
        // Arrange
        var principal = CreateEntraPrincipal();
        _mockOidcTokenValidator
            .Setup(v => v.Validate("acme-google", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GenericToken(principal, "Acme-Google"));
        _mockUserService
            .Setup(s => s.ResolveFromGenericOidcPrincipalAsync(It.IsAny<string>(), principal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((string Id, string? EmployeeId)?)null);

        // Act
        var act = () => _sut.ExchangeTokenAsync(
            new ExchangeTokenCommand("acme-google", "token"),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>();
        _mockUserService.Verify(s => s.ResolveFromGenericOidcPrincipalAsync("Acme-Google", principal, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(LoginProviders.MicrosoftEntraId)]
    [InlineData("microsoftentraid")]
    [InlineData(LoginProviders.Wayd)]
    public async Task ExchangeTokenAsync_ThrowsUnauthorized_WhenAGenericProviderIsStoredUnderAReservedName(string name)
    {
        // Arrange
        var principal = CreateEntraPrincipal();
        _mockOidcTokenValidator
            .Setup(v => v.Validate(name, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GenericToken(principal, name));

        // Act
        var act = () => _sut.ExchangeTokenAsync(
            new ExchangeTokenCommand(name, "token"),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>().WithMessage("Invalid token.");
        _mockUserService.Verify(s => s.ResolveFromGenericOidcPrincipalAsync(
            It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExchangeTokenAsync_ThrowsUnauthorized_WhenAnEntraProviderIsStoredUnderAnotherName()
    {
        // Arrange
        var principal = CreateEntraPrincipal();
        var provider = new OidcProviderFaker().AsMicrosoftEntraId("tenant-1").WithName("Contoso").Generate();
        _mockOidcTokenValidator
            .Setup(v => v.Validate("Contoso", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidatedOidcToken(principal, provider));

        // Act
        var act = () => _sut.ExchangeTokenAsync(
            new ExchangeTokenCommand("Contoso", "token"),
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>().WithMessage("Invalid token.");
        _mockUserService.Verify(s => s.GetOrCreateFromPrincipalAsync(It.IsAny<ClaimsPrincipal>()), Times.Never);
        _mockUserService.Verify(s => s.ResolveFromGenericOidcPrincipalAsync(
            It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region AuthProviders

    [Fact]
    public async Task GetAuthProviders_ShouldReportLocalTrueAndEmptyOidcList_WhenNoProvidersRegistered()
    {
        // Default registry mock returns empty list — local-only deployment.
        var result = await _sut.GetAuthProviders(TestContext.Current.CancellationToken);

        result.Local.Should().BeTrue();
        result.Oidc.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAuthProviders_ShouldExposePublicProviderFields()
    {
        // Pin the shape contract: response includes Name/DisplayName/ProviderType/
        // Authority/ClientId/Scopes per enabled provider. AllowedTenantIds is NOT
        // exposed — it's a security gate and the frontend never needs it. If a
        // future regression adds it to the response, this assertion catches it
        // via the explicit field list (BeEquivalentTo treats extras as a failure
        // when the expected shape doesn't include them — but here we just check
        // each known field is right).
        // Pin the fields the assertions below check; the faker fills the rest.
        var entra = new OidcProviderFaker()
            .WithName(LoginProviders.MicrosoftEntraId)
            .WithDisplayName("Microsoft Entra ID")
            .AsMicrosoftEntraId("11111111-1111-1111-1111-111111111111")
            .WithAuthority("https://login.microsoftonline.com/common/v2.0")
            .WithClientId("test-client-id")
            .WithScopes("openid", "profile")
            .Generate();

        _mockOidcProviderRegistry
            .Setup(r => r.GetEnabled(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<OidcProvider>)new[] { entra });

        var result = await _sut.GetAuthProviders(TestContext.Current.CancellationToken);

        result.Local.Should().BeTrue();
        result.Oidc.Should().HaveCount(1);
        var info = result.Oidc[0];
        info.Name.Should().Be("MicrosoftEntraId");
        info.DisplayName.Should().Be("Microsoft Entra ID");
        info.ProviderType.Should().Be(nameof(OidcProviderType.MicrosoftEntraId));
        info.Authority.Should().Be("https://login.microsoftonline.com/common/v2.0");
        info.ClientId.Should().Be("test-client-id");
        info.Scopes.Should().BeEquivalentTo(new[] { "openid", "profile" });
    }

    #endregion

    #region GetSettings

    [Fact]
    public async Task GetTokenAsync_ShouldThrowInvalidOperation_WhenJwtSettingsNotConfigured()
    {
        // Arrange
        var emptyConfig = new ConfigurationBuilder().Build();
        var sut = new TokenService(
            _mockUserManager.Object,
            _mockSignInManager.Object,
            _mockRoleManager.Object,
            emptyConfig,
            _dateTimeProvider,
            _mockUserIdentityStore.Object,
            _mockUserService.Object,
            _mockOidcTokenValidator.Object,
            _mockOidcProviderRegistry.Object,
            _refreshTokenStore,
            _sessionContext,
            _mockLogger.Object);

        var user = CreateLocalUser();
        SeedActiveWaydIdentity(user.Id);
        _mockUserManager.Setup(x => x.FindByNameAsync("testuser")).ReturnsAsync(user);
        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Password123!", true))
            .ReturnsAsync(SignInResult.Success);

        var command = new LoginCommand("testuser", "Password123!");

        // Act
        var act = () => sut.GetTokenAsync(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Local JWT settings are not configured.");
    }

    #endregion

    #region LogoutAsync and sessions

    /// <summary>
    /// Signs a user in and returns the plaintext refresh token the client would hold.
    /// </summary>
    private async Task<string> SignIn(ApplicationUser user)
    {
        SeedActiveWaydIdentity(user.Id);
        _mockUserManager.Setup(x => x.FindByNameAsync(user.UserName!)).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
        _mockSignInManager.Setup(x => x.CheckPasswordSignInAsync(user, "Password123!", true))
            .ReturnsAsync(SignInResult.Success);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var response = await _sut.GetTokenAsync(
            new LoginCommand(user.UserName!, "Password123!"),
            TestContext.Current.CancellationToken);

        return response.RefreshToken;
    }

    private Task<TokenResponse> Refresh(ApplicationUser user, string accessToken, string refreshToken) =>
        _sut.RefreshTokenAsync(new RefreshTokenCommand(accessToken, refreshToken), TestContext.Current.CancellationToken);

    [Fact]
    public async Task GetTokenAsync_ShouldOpenASecondSession_WhenUserSignsInAgain()
    {
        // The reason sessions are rows: signing in on a second device must not displace the
        // first. With the old single column, the second login silently invalidated the first.

        // Arrange
        var user = CreateLocalUser();
        var firstDevice = await SignIn(user);

        // Act
        var secondDevice = await SignIn(user);

        // Assert
        _refreshTokenStore.ActiveSessionCount(user.Id).Should().Be(2);
        secondDevice.Should().NotBe(firstDevice);
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldStillWorkOnFirstDevice_AfterSecondDeviceSignsIn()
    {
        // Arrange
        var user = CreateLocalUser();
        var firstDevice = await SignIn(user);
        var accessToken = await _sut.GetTokenAsync(
            new LoginCommand(user.UserName!, "Password123!"), TestContext.Current.CancellationToken);
        await SignIn(user);

        // Act
        var result = await Refresh(user, accessToken.Token, firstDevice);

        // Assert
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Should().NotBe(firstDevice, "refresh rotates the presented token");
    }

    [Fact]
    public async Task LogoutAsync_ShouldRevokeTheNamedSessionOnly()
    {
        // Arrange
        var user = CreateLocalUser();
        var laptop = await SignIn(user);
        await SignIn(user);

        // Act
        await _sut.LogoutAsync(user.Id, laptop, TestContext.Current.CancellationToken);

        // Assert
        _refreshTokenStore.ActiveSessionCount(user.Id).Should().Be(1);
        _refreshTokenStore.RevokeReasons(user.Id).Should().AllBe(UserRefreshTokenRevokeReasons.SignedOut);
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldThrowUnauthorized_WhenRefreshTokenWasIssuedBeforeLogout()
    {
        // F8's core claim: signing out must revoke server-side, so a token captured
        // beforehand can no longer mint a session.

        // Arrange
        var user = CreateLocalUser();
        await SignIn(user);
        var captured = await _sut.GetTokenAsync(
            new LoginCommand(user.UserName!, "Password123!"), TestContext.Current.CancellationToken);
        await _sut.LogoutAsync(user.Id, captured.RefreshToken, TestContext.Current.CancellationToken);

        // Act
        var act = () => Refresh(user, captured.Token, captured.RefreshToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid or expired refresh token.");
    }

    [Fact]
    public async Task LogoutAsync_ShouldNotAffectAnotherUsersSessions()
    {
        // Arrange
        var user = CreateLocalUser();
        var otherUser = CreateLocalUser(id: "user-2", userName: "otheruser");
        var token = await SignIn(user);
        await SignIn(otherUser);

        // Act
        await _sut.LogoutAsync(user.Id, token, TestContext.Current.CancellationToken);

        // Assert
        _refreshTokenStore.ActiveSessionCount(user.Id).Should().Be(0);
        _refreshTokenStore.ActiveSessionCount(otherUser.Id).Should().Be(1);
    }

    [Fact]
    public async Task LogoutAsync_ShouldNotThrow_WhenCalledTwice()
    {
        // Arrange
        var user = CreateLocalUser();
        await SignIn(user);
        var token = await SignIn(user);
        await _sut.LogoutAsync(user.Id, token, TestContext.Current.CancellationToken);

        // Act
        var act = () => _sut.LogoutAsync(user.Id, token, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
        _refreshTokenStore.ActiveSessionCount(user.Id).Should().Be(1, "the earlier session is untouched");
    }

    #endregion

    #region Reuse detection

    [Fact]
    public async Task RefreshTokenAsync_ShouldRevokeOnlyTheAffectedSession_WhenSupersededTokenIsReplayed()
    {
        // Reuse kills the chain it was presented against — and only that chain. Under the old
        // single-column model this revoked every device the user had.

        // Arrange
        var user = CreateLocalUser();
        var firstDevice = await SignIn(user);
        var accessToken = await _sut.GetTokenAsync(
            new LoginCommand(user.UserName!, "Password123!"), TestContext.Current.CancellationToken);
        await SignIn(user);
        _refreshTokenStore.ActiveSessionCount(user.Id).Should().Be(3);

        await Refresh(user, accessToken.Token, firstDevice);

        // Act - replay the token that rotation just superseded
        var act = () => Refresh(user, accessToken.Token, firstDevice);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid or expired refresh token.");

        _refreshTokenStore.ActiveSessionCount(user.Id).Should().Be(2, "only the replayed session dies");
        _refreshTokenStore.RevokeReasons(user.Id).Should().ContainSingle()
            .Which.Should().Be(UserRefreshTokenRevokeReasons.ReuseDetected);
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldThrowUnauthorized_WhenTokenIsUnknown()
    {
        // Arrange
        var user = CreateLocalUser();
        await SignIn(user);
        var accessToken = await _sut.GetTokenAsync(
            new LoginCommand(user.UserName!, "Password123!"), TestContext.Current.CancellationToken);

        // Act
        var act = () => Refresh(user, accessToken.Token, "not-a-token-this-user-holds");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid or expired refresh token.");

        _refreshTokenStore.ActiveSessionCount(user.Id).Should().Be(2, "an unknown token is not a reuse signal");
    }

    [Fact]
    public async Task RefreshTokenAsync_RefusesAnAccessTokenTypedAtJwt_EvenWithTheWebAppsIssuerAndAudience()
    {
        // Arrange — the authorization server's tokens share this key, so their type is what keeps them out
        var user = CreateLocalUser();
        var refreshToken = await SignIn(user);
        var accessToken = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler().CreateToken(
            new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
            {
                TokenType = "at+jwt",
                Issuer = TestIssuer,
                Audience = TestAudience,
                Subject = new ClaimsIdentity([new Claim("sub", user.Id), new Claim(ClaimTypes.NameIdentifier, user.Id)]),
                SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                    Wayd.Infrastructure.Auth.Local.ConfigureServices.CreateSigningKey(TestSecret),
                    Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256),
            });

        // Act
        var act = () => Refresh(user, accessToken, refreshToken);

        // Assert
        await act.Should().ThrowAsync<Microsoft.IdentityModel.Tokens.SecurityTokenInvalidTypeException>();
        _refreshTokenStore.ActiveSessionCount(user.Id).Should().Be(1, "the refresh token is left unspent");
    }

    #endregion

    #region Per-device sign-out

    [Fact]
    public async Task LogoutAsync_ShouldEndOnlyTheCallingDevice_WhenRefreshTokenIsSupplied()
    {
        // Signing out on the laptop must leave the phone signed in.

        // Arrange
        var user = CreateLocalUser();
        var laptop = await SignIn(user);
        await SignIn(user);
        _refreshTokenStore.ActiveSessionCount(user.Id).Should().Be(2);

        // Act
        await _sut.LogoutAsync(user.Id, laptop, TestContext.Current.CancellationToken);

        // Assert
        _refreshTokenStore.ActiveSessionCount(user.Id).Should().Be(1);
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldStillWorkOnOtherDevices_AfterOneSignsOut()
    {
        // Arrange
        var user = CreateLocalUser();
        var laptop = await SignIn(user);
        var phone = await SignIn(user);
        var accessToken = await _sut.GetTokenAsync(
            new LoginCommand(user.UserName!, "Password123!"), TestContext.Current.CancellationToken);

        // Act
        await _sut.LogoutAsync(user.Id, laptop, TestContext.Current.CancellationToken);

        // Assert
        var phoneRefresh = await Refresh(user, accessToken.Token, phone);
        phoneRefresh.RefreshToken.Should().NotBeNullOrWhiteSpace();

        var laptopRefresh = () => Refresh(user, accessToken.Token, laptop);
        await laptopRefresh.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task LogoutAsync_ShouldRevokeNothing_WhenNoRefreshTokenIsSupplied()
    {
        // Regression: this once revoked every session. "I cannot tell which session this is"
        // is not a reason to end them all — the caller is signed out locally regardless.

        // Arrange
        var user = CreateLocalUser();
        await SignIn(user);
        await SignIn(user);

        // Act
        await _sut.LogoutAsync(user.Id, refreshToken: null, TestContext.Current.CancellationToken);

        // Assert
        _refreshTokenStore.ActiveSessionCount(user.Id).Should().Be(2);
    }

    [Fact]
    public async Task LogoutAsync_ShouldRevokeNothing_WhenRefreshTokenDoesNotMatch()
    {
        // Arrange
        var user = CreateLocalUser();
        await SignIn(user);
        await SignIn(user);

        // Act
        await _sut.LogoutAsync(user.Id, "not-a-token-this-user-holds", TestContext.Current.CancellationToken);

        // Assert
        _refreshTokenStore.ActiveSessionCount(user.Id).Should().Be(2);
    }

    [Fact]
    public async Task LogoutAsync_ShouldNotEndAnotherUsersSession_WhenGivenTheirToken()
    {
        // Arrange
        var user = CreateLocalUser();
        var otherUser = CreateLocalUser(id: "user-2", userName: "otheruser");
        await SignIn(user);
        var othersToken = await SignIn(otherUser);

        // Act
        await _sut.LogoutAsync(user.Id, othersToken, TestContext.Current.CancellationToken);

        // Assert
        _refreshTokenStore.ActiveSessionCount(otherUser.Id).Should().Be(1, "their session is not the caller's to end");
    }

    [Fact]
    public async Task LogoutAllAsync_ShouldEndEverySessionForTheUser()
    {
        // Arrange
        var user = CreateLocalUser();
        await SignIn(user);
        await SignIn(user);

        // Act
        await _sut.LogoutAllAsync(user.Id, TestContext.Current.CancellationToken);

        // Assert
        _refreshTokenStore.ActiveSessionCount(user.Id).Should().Be(0);
        _refreshTokenStore.RevokeReasons(user.Id).Should().AllBe(UserRefreshTokenRevokeReasons.SignedOut);
    }

    #endregion

    #region Sessions list

    [Fact]
    public async Task GetSessions_ShouldMarkTheCallersOwnSession()
    {
        // The UI needs this to warn before someone revokes the session they are using.

        // Arrange
        var user = CreateLocalUser();
        var laptop = await SignIn(user);
        await SignIn(user);

        // Act
        var sessions = await _sut.GetSessions(user.Id, laptop, TestContext.Current.CancellationToken);

        // Assert
        sessions.Should().HaveCount(2);
        sessions.Count(s => s.IsCurrent).Should().Be(1);
    }

    [Fact]
    public async Task GetSessions_ShouldMarkNoneCurrent_WhenNoRefreshTokenIsSupplied()
    {
        // Arrange
        var user = CreateLocalUser();
        await SignIn(user);

        // Act
        var sessions = await _sut.GetSessions(user.Id, currentRefreshToken: null, TestContext.Current.CancellationToken);

        // Assert
        sessions.Should().ContainSingle();
        sessions.Should().AllSatisfy(s => s.IsCurrent.Should().BeFalse());
    }

    [Fact]
    public async Task GetSessions_ShouldNotExposeTokensOrHashes()
    {
        // The response is display detail plus an id; nothing on it may be a credential.

        // Arrange
        var user = CreateLocalUser();
        var token = await SignIn(user);

        // Act
        var sessions = await _sut.GetSessions(user.Id, token, TestContext.Current.CancellationToken);

        // Assert
        var properties = typeof(UserSessionResponse).GetProperties().Select(p => p.Name);
        properties.Should().NotContain(n => n.Contains("Token", StringComparison.OrdinalIgnoreCase)
                                         || n.Contains("Hash", StringComparison.OrdinalIgnoreCase));
        sessions.Should().ContainSingle();
    }

    [Fact]
    public async Task RevokeSession_ShouldReturnFalse_WhenSessionIsNotTheirs()
    {
        // Arrange
        var user = CreateLocalUser();
        var otherUser = CreateLocalUser(id: "user-2", userName: "otheruser");
        await SignIn(user);
        var othersToken = await SignIn(otherUser);
        var othersSessions = await _sut.GetSessions(otherUser.Id, othersToken, TestContext.Current.CancellationToken);

        // Act
        var revoked = await _sut.RevokeSession(user.Id, othersSessions[0].Id, TestContext.Current.CancellationToken);

        // Assert
        revoked.Should().BeFalse();
        _refreshTokenStore.ActiveSessionCount(otherUser.Id).Should().Be(1);
    }

    [Fact]
    public async Task RevokeSession_ShouldEndThatSessionOnly()
    {
        // Arrange
        var user = CreateLocalUser();
        var laptop = await SignIn(user);
        await SignIn(user);
        var sessions = await _sut.GetSessions(user.Id, laptop, TestContext.Current.CancellationToken);
        var other = sessions.Single(s => !s.IsCurrent);

        // Act
        var revoked = await _sut.RevokeSession(user.Id, other.Id, TestContext.Current.CancellationToken);

        // Assert
        revoked.Should().BeTrue();
        _refreshTokenStore.ActiveSessionCount(user.Id).Should().Be(1);
    }

    #endregion
}
