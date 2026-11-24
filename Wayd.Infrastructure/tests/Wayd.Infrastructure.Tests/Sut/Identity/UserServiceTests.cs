using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Identity;
using Wayd.Common.Application.Identity.OidcProviders;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Identity;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Interfaces;
using Wayd.Infrastructure.Auth.Local;
using Wayd.Infrastructure.Identity;
using Wayd.Infrastructure.Tests.Sut.Auth.Local;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Data;
using NotFoundException = Wayd.Common.Application.Exceptions.NotFoundException;

namespace Wayd.Infrastructure.Tests.Sut.Identity;

public class UserServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
    private readonly Mock<RoleManager<ApplicationRole>> _mockRoleManager;
    private readonly Mock<ILogger<UserService>> _mockLogger;
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly Mock<IDispatcher> _mockDispatcher;
    private readonly Mock<ICurrentUser> _mockCurrentUser;
    private readonly Mock<IUserIdentityStore> _mockUserIdentityStore;
    private readonly Mock<IOidcProviderRegistry> _mockOidcProviderRegistry;
    private readonly FakeRefreshTokenStore _refreshTokenStore = new();

    // UserService depends on WaydDbContext which is hard to mock. We test methods that
    // don't require it. UserIdentity writes go through IUserIdentityStore so they can be
    // verified via Moq.

    public UserServiceTests()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        _mockUserManager.Object.Options = new IdentityOptions();

        _mockSignInManager = new Mock<SignInManager<ApplicationUser>>(
            _mockUserManager.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>().Object,
            null!, null!, null!, null!);

        var roleStore = new Mock<IRoleStore<ApplicationRole>>();
        _mockRoleManager = new Mock<RoleManager<ApplicationRole>>(
            roleStore.Object, null!, null!, null!, null!);

        _mockLogger = new Mock<ILogger<UserService>>();
        _dateTimeProvider = new TestingDateTimeProvider(DateTime.UtcNow);
        _mockDispatcher = new Mock<IDispatcher>();
        _mockCurrentUser = new Mock<ICurrentUser>();
        _mockCurrentUser.Setup(x => x.GetUserId()).Returns("current-user-id");

        // The caller is an active Admin, holding every permission, unless a test says otherwise.
        _caller = CreateUser(CallerId, "caller");
        _mockUserManager.Setup(x => x.FindByIdAsync(CallerId)).ReturnsAsync(_caller);
        _mockUserManager.Setup(x => x.GetRolesAsync(_caller)).ReturnsAsync([ApplicationRoles.Admin]);
        _mockUserManager.Setup(x => x.GetUsersInRoleAsync(ApplicationRoles.Admin)).ReturnsAsync([_caller]);
        _mockRoleManager.Setup(x => x.GetClaimsAsync(It.Is<ApplicationRole>(r => r.Name == ApplicationRoles.Admin)))
            .ReturnsAsync([.. ApplicationPermissions.Admin.Select(p => new Claim(ApplicationClaims.Permission, p.Name))]);
        _mockUserIdentityStore = new Mock<IUserIdentityStore>();
        _mockOidcProviderRegistry = new Mock<IOidcProviderRegistry>();

        // Pass-through: tests don't exercise transaction semantics. Invoke the
        // action directly so CreateAsync behaves as if the transaction succeeded.
        _mockUserIdentityStore
            .Setup(s => s.ExecuteInTransaction(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>((action, ct) => action(ct));

        _mockRoleManager.Setup(x => x.Roles).Returns(new[]
        {
            new ApplicationRole(ApplicationRoles.Admin) { Id = AdminRoleId },
            new ApplicationRole("Basic") { Id = BasicRoleId },
            new ApplicationRole("Contributor") { Id = ContributorRoleId },
        }.AsQueryable().BuildMockDbSet().Object);
    }

    private const string CallerId = "current-user-id";
    private readonly ApplicationUser _caller;

    private const string AdminRoleId = "role-admin";
    private const string BasicRoleId = "role-basic";
    private const string ContributorRoleId = "role-contributor";

    private static IReadOnlyCollection<DomainEvent> RaisedEvents(IEntity entity) => entity.DomainEvents;

    private UserService CreateSut()
    {
        return new UserService(
            _mockLogger.Object,
            _mockSignInManager.Object,
            _mockUserManager.Object,
            _mockRoleManager.Object,
            null!, // WaydDbContext - not used by these methods
            _mockDispatcher.Object,
            _dateTimeProvider,
            _mockCurrentUser.Object,
            _mockUserIdentityStore.Object,
            _mockOidcProviderRegistry.Object,
            _refreshTokenStore);
    }


    private static ApplicationUser CreateUser(string id = "user-1", string userName = "testuser", bool isActive = true, string loginProvider = LoginProviders.MicrosoftEntraId)
    {
        return new ApplicationUser
        {
            Id = id,
            UserName = userName,
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            IsActive = isActive,
            LoginProvider = loginProvider,
        };
    }

    #region CreateFirstAdministrator

    [Fact]
    public async Task CreateFirstAdministrator_CreatesAnAdmin_RecordedAsTheSystems_WhenNoUserExists()
    {
        // Arrange — first-run setup has nobody signed in.
        _mockCurrentUser.Setup(x => x.GetUserId()).Returns((string?)null);
        ApplicationUser? created = null;
        _mockUserManager.Setup(x => x.Users).Returns(Array.Empty<ApplicationUser>().AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), "Password123!"))
            .Callback<ApplicationUser, string>((user, _) => created = user)
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>())).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.FindByIdAsync(It.Is<string>(id => id != CallerId))).ReturnsAsync(() => created);
        _mockUserManager.Setup(x => x.GetRolesAsync(It.Is<ApplicationUser>(u => u != _caller))).ReturnsAsync([]);
        _mockUserManager.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), ApplicationRoles.Admin)).ReturnsAsync(IdentityResult.Success);
        var sut = CreateSut();

        // Act
        var result = await sut.CreateFirstAdministrator(new CreateUserCommand
        {
            FirstName = "First",
            LastName = "Admin",
            Email = "first@example.com",
            LoginProvider = LoginProviders.Wayd,
            Password = "Password123!",
        }, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _mockUserManager.Verify(x => x.AddToRoleAsync(created!, ApplicationRoles.Admin), Times.Once);
        var granted = RaisedEvents(created!).OfType<ApplicationUserRolesChangedEvent>().Single();
        granted.Added.Should().Equal(AdminRoleId);
        granted.Actor.Should().Be(EventActor.System);
    }

    [Fact]
    public async Task CreateFirstAdministrator_IsRefused_OnceAnyUserExists()
    {
        // Arrange
        _mockUserManager.Setup(x => x.Users).Returns(new[] { CreateUser() }.AsQueryable().BuildMockDbSet().Object);
        var sut = CreateSut();

        // Act
        var result = await sut.CreateFirstAdministrator(new CreateUserCommand
        {
            FirstName = "Second",
            LastName = "Admin",
            Email = "second@example.com",
            LoginProvider = LoginProviders.Wayd,
            Password = "Password123!",
        }, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        _mockUserManager.Verify(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_ShouldSucceed_WhenLocalUserWithPassword()
    {
        // Arrange
        var command = new CreateUserCommand
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            LoginProvider = LoginProviders.Wayd,
            Password = "Password123!",
            RoleNames = ["Contributor"],
        };

        ApplicationUser? created = null;
        _mockUserManager
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), "Password123!"))
            .Callback<ApplicationUser, string>((u, _) => created = u)
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager
            .Setup(x => x.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        var result = await sut.CreateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrWhiteSpace();
        _mockUserManager.Verify(x => x.CreateAsync(It.Is<ApplicationUser>(u =>
            u.FirstName == "John" &&
            u.LastName == "Doe" &&
            u.Email == "john@example.com" &&
            u.UserName == "john@example.com" &&
            u.LoginProvider == LoginProviders.Wayd &&
            u.PendingMigrationTenantId == null &&
            u.IsActive), "Password123!"), Times.Once);
        _mockUserManager.Verify(x => x.AddToRolesAsync(It.IsAny<ApplicationUser>(),
            It.Is<IEnumerable<string>>(r => r.SequenceEqual(new[] { "Contributor" }))), Times.Once);

        var events = RaisedEvents(created!);
        events.Should().HaveCount(2);
        events.First().Should().BeOfType<ApplicationUserCreatedEvent>()
            .Which.Actor.Should().Be(EventActor.User("current-user-id"));
        var roles = events.Last().Should().BeOfType<ApplicationUserRolesChangedEvent>().Subject;
        roles.Added.Should().Equal(ContributorRoleId);
        roles.Removed.Should().BeEmpty();
        roles.Roles.Should().Equal(ContributorRoleId);
    }

    [Fact]
    public async Task CreateAsync_ShouldAttributeTheCreationToAnAnonymousCaller_WhenNoOneIsSignedIn()
    {
        // Arrange - first-run setup creates the first admin before any account exists.
        _mockCurrentUser.Setup(x => x.GetUserId()).Returns(string.Empty);
        var command = new CreateUserCommand
        {
            FirstName = "First",
            LastName = "Admin",
            Email = "admin@example.com",
            LoginProvider = LoginProviders.Wayd,
            Password = "Password123!",
            RoleNames = [],
        };

        ApplicationUser? created = null;
        _mockUserManager
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), "Password123!"))
            .Callback<ApplicationUser, string>((u, _) => created = u)
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager
            .Setup(x => x.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        await sut.CreateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        RaisedEvents(created!).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserCreatedEvent>()
            .Which.Actor.Should().Be(EventActor.Anonymous);
    }

    [Fact]
    public async Task CreateAsync_ShouldRecordTheStagedFirstSignInTenant_WhenEntraIdUser()
    {
        // Arrange
        var command = new CreateUserCommand
        {
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com",
            LoginProvider = LoginProviders.MicrosoftEntraId,
            RoleNames = ["Contributor"],
        };

        ArrangeEntraProvider("tenant-1");
        ApplicationUser? created = null;
        _mockUserManager
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>()))
            .Callback<ApplicationUser>(u => created = u)
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager
            .Setup(x => x.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        await sut.CreateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        created!.PendingMigrationTenantId.Should().Be("tenant-1");
        created.PendingMigrationStagedAt.Should().Be(_dateTimeProvider.Now);
        var staged = RaisedEvents(created).OfType<ApplicationUserTenantMigrationStagedEvent>().Should().ContainSingle().Subject;
        staged.TargetTenantId.Should().Be("tenant-1");
        staged.PreviousTargetTenantId.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ShouldSucceed_WhenEntraIdUserWithoutPassword()
    {
        // Arrange
        var command = new CreateUserCommand
        {
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com",
            LoginProvider = LoginProviders.MicrosoftEntraId,
            RoleNames = ["Contributor"],
        };

        ArrangeEntraProvider("tenant-1");
        _mockUserManager
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager
            .Setup(x => x.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        var result = await sut.CreateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockUserManager.Verify(x => x.CreateAsync(It.Is<ApplicationUser>(u =>
            u.LoginProvider == LoginProviders.MicrosoftEntraId)), Times.Once);
        // Should NOT call the password overload
        _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnFailure_WhenIdentityResultFails()
    {
        // Arrange
        var command = new CreateUserCommand
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            LoginProvider = LoginProviders.Wayd,
            Password = "Password123!",
        };

        ApplicationUser? created = null;
        _mockUserManager
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), "Password123!"))
            .Callback<ApplicationUser, string>((u, _) => created = u)
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Duplicate username." }));

        var sut = CreateSut();

        // Act
        var result = await sut.CreateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Duplicate username.");
        _mockUserManager.Verify(x => x.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()), Times.Never);
        RaisedEvents(created!).Should().BeEmpty("nothing was saved, so nothing may be recorded");
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnFailureAndNotWriteIdentity_WhenRoleAssignmentFails()
    {
        // Arrange — role assignment runs inside the create transaction; a failure
        // there must roll the whole thing back, not leave a roleless user.
        var command = new CreateUserCommand
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            LoginProvider = LoginProviders.Wayd,
            Password = "Password123!",
            RoleNames = ["Contributor"],
        };

        ApplicationUser? created = null;
        _mockUserManager
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), "Password123!"))
            .Callback<ApplicationUser, string>((u, _) => created = u)
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager
            .Setup(x => x.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Role assignment failed." }));

        var sut = CreateSut();

        // Act
        var result = await sut.CreateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Role assignment failed.");
        _mockUserIdentityStore.Verify(s => s.Add(It.IsAny<UserIdentity>(), It.IsAny<CancellationToken>()), Times.Never);
        RaisedEvents(created!).OfType<ApplicationUserRolesChangedEvent>().Should().BeEmpty("the role assignment was not saved");
    }

    [Fact]
    public async Task CreateAsync_ShouldWriteActiveWaydIdentity_WhenLocalUserCreated()
    {
        // Arrange
        var command = new CreateUserCommand
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            LoginProvider = LoginProviders.Wayd,
            Password = "Password123!",
        };

        _mockUserManager
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), "Password123!"))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager
            .Setup(x => x.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        var result = await sut.CreateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockUserIdentityStore.Verify(s => s.Add(
            It.Is<UserIdentity>(ui =>
                ui.UserId == result.Value &&
                ui.Provider == LoginProviders.Wayd &&
                ui.ProviderTenantId == null &&
                ui.ProviderSubject == result.Value &&
                ui.IsActive &&
                ui.UnlinkedAt == null),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ShouldNotWriteIdentity_WhenEntraAdminProvisionedUser()
    {
        // Arrange — Entra users created by an admin have no oid yet; the identity row
        // is only written when their staged first sign-in completes.
        var command = new CreateUserCommand
        {
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com",
            LoginProvider = LoginProviders.MicrosoftEntraId,
        };

        ArrangeEntraProvider("tenant-1");
        _mockUserManager
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager
            .Setup(x => x.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        await sut.CreateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        _mockUserIdentityStore.Verify(s => s.Add(It.IsAny<UserIdentity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ShouldStageFirstSignInToTheOnlyAllowedTenant_WhenNoTenantRequested()
    {
        // Arrange
        const string tenantId = "7d1b4a52-0000-4000-8000-000000000001";
        ArrangeEntraProvider(tenantId);
        var created = CaptureCreatedExternalUser();
        var sut = CreateSut();

        // Act
        var result = await sut.CreateAsync(EntraCreateCommand(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        created().PendingMigrationTenantId.Should().Be(tenantId);
        created().PendingMigrationStagedAt.Should().Be(_dateTimeProvider.Now);
    }

    [Fact]
    public async Task CreateAsync_ShouldStageTheRequestedTenantAsConfigured_WhenProviderAllowsIt()
    {
        // Arrange — the request differs only in case and whitespace; the stored value is the
        // provider's own spelling, since sign-in matches it against the token's tid exactly.
        const string tenantA = "7d1b4a52-0000-4000-8000-00000000000a";
        const string tenantB = "7d1b4a52-0000-4000-8000-00000000000b";
        ArrangeEntraProvider(tenantA, tenantB);
        var created = CaptureCreatedExternalUser();
        var sut = CreateSut();

        // Act
        var result = await sut.CreateAsync(
            EntraCreateCommand(tenantId: $" {tenantB.ToUpperInvariant()} "),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        created().PendingMigrationTenantId.Should().Be(tenantB);
    }

    [Fact]
    public async Task CreateAsync_ShouldFailWithoutCreating_WhenRequestedTenantIsNotAllowed()
    {
        // Arrange — staging a tenant the validator rejects at sign-in would strand the user.
        ArrangeEntraProvider("7d1b4a52-0000-4000-8000-00000000000a");
        var sut = CreateSut();

        // Act
        var result = await sut.CreateAsync(
            EntraCreateCommand(tenantId: "7d1b4a52-0000-4000-8000-0000000000ff"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not allowed");
        _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ShouldFailWithoutCreating_WhenProviderAllowsSeveralTenantsAndNoneIsRequested()
    {
        // Arrange
        ArrangeEntraProvider("7d1b4a52-0000-4000-8000-00000000000a", "7d1b4a52-0000-4000-8000-00000000000b");
        var sut = CreateSut();

        // Act
        var result = await sut.CreateAsync(EntraCreateCommand(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Choose the tenant");
        _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ShouldFailWithoutCreating_WhenEntraProviderIsNotConfigured()
    {
        // Arrange — no provider means no tenant to stage, and no way for the user to sign in.
        _mockOidcProviderRegistry
            .Setup(r => r.GetByName(LoginProviders.MicrosoftEntraId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OidcProvider?)null);
        var sut = CreateSut();

        // Act
        var result = await sut.CreateAsync(EntraCreateCommand(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not configured");
        _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ShouldLinkOnFirstSignInFromTheStagedTenant_WhenEntraUserCreatedByAdmin()
    {
        // Arrange — the account CreateAsync produces is the one the first sign-in resolves.
        const string tenantId = "7d1b4a52-0000-4000-8000-000000000001";
        const string objectId = "0a9f3c11-0000-4000-8000-000000000002";
        const string email = "morgan.lee@acme.example";
        ArrangeEntraProvider(tenantId);
        var created = CaptureCreatedExternalUser();
        await CreateSut().CreateAsync(EntraCreateCommand(email), TestContext.Current.CancellationToken);
        var user = created();

        ArrangeFirstSignIn(user);
        var sut = CreateSut();

        // Act
        var (resolvedId, _) = await sut.GetOrCreateFromPrincipalAsync(CreateEntraPrincipal(objectId, tenantId, email));

        // Assert
        resolvedId.Should().Be(user.Id);
        user.PendingMigrationTenantId.Should().BeNull();
        _mockUserIdentityStore.Verify(s => s.Add(
            It.Is<UserIdentity>(ui =>
                ui.UserId == user.Id &&
                ui.Provider == LoginProviders.MicrosoftEntraId &&
                ui.ProviderTenantId == tenantId &&
                ui.ProviderSubject == objectId &&
                ui.IsActive),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ShouldStillDenyASignInFromAnotherTenant_WhenEntraUserCreatedByAdmin()
    {
        // Arrange — F3 guard: the staged link names one tenant, so a directory object in
        // another allowed tenant presenting the same email must not claim the account.
        const string stagedTenant = "7d1b4a52-0000-4000-8000-00000000000a";
        const string otherTenant = "7d1b4a52-0000-4000-8000-00000000000b";
        const string email = "morgan.lee@acme.example";
        ArrangeEntraProvider(stagedTenant, otherTenant);
        var created = CaptureCreatedExternalUser();
        await CreateSut().CreateAsync(EntraCreateCommand(email, stagedTenant), TestContext.Current.CancellationToken);
        var user = created();

        ArrangeFirstSignIn(user);
        _mockUserManager.Setup(x => x.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);
        var sut = CreateSut();

        // Act
        var act = () => sut.GetOrCreateFromPrincipalAsync(CreateNewUserPrincipal(
            "0a9f3c11-0000-4000-8000-000000000003", otherTenant, email, displayName: "Morgan Lee"));

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*not linked to this identity provider*");
        user.PendingMigrationTenantId.Should().Be(stagedTenant);
        _mockUserIdentityStore.Verify(s => s.Add(It.IsAny<UserIdentity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetEntraTenantIds_ShouldReturnTheProvidersAllowlist()
    {
        // Arrange
        ArrangeEntraProvider("7d1b4a52-0000-4000-8000-00000000000a", "7d1b4a52-0000-4000-8000-00000000000b");
        var sut = CreateSut();

        // Act
        var tenants = await sut.GetEntraTenantIds(TestContext.Current.CancellationToken);

        // Assert
        tenants.Should().Equal("7d1b4a52-0000-4000-8000-00000000000a", "7d1b4a52-0000-4000-8000-00000000000b");
    }

    [Fact]
    public async Task GetEntraTenantIds_ShouldReturnEmpty_WhenEntraProviderIsNotConfigured()
    {
        // Arrange
        _mockOidcProviderRegistry
            .Setup(r => r.GetByName(LoginProviders.MicrosoftEntraId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OidcProvider?)null);
        var sut = CreateSut();

        // Act
        var tenants = await sut.GetEntraTenantIds(TestContext.Current.CancellationToken);

        // Assert
        tenants.Should().BeEmpty();
    }

    private void ArrangeEntraProvider(params string[] allowedTenantIds)
    {
        var provider = new OidcProviderFaker()
            .WithName(LoginProviders.MicrosoftEntraId)
            .AsMicrosoftEntraId(allowedTenantIds)
            .WithoutAutoRegistration()
            .Generate();

        _mockOidcProviderRegistry
            .Setup(r => r.GetByName(LoginProviders.MicrosoftEntraId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(provider);
    }

    // Arranges a successful create of a password-less user and returns an accessor for
    // the ApplicationUser that CreateAsync handed to UserManager.
    private Func<ApplicationUser> CaptureCreatedExternalUser()
    {
        ApplicationUser? created = null;
        _mockUserManager
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>()))
            .Callback<ApplicationUser>(u => created = u)
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager
            .Setup(x => x.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Success);

        return () => created ?? throw new InvalidOperationException("CreateAsync did not create a user.");
    }

    private static CreateUserCommand EntraCreateCommand(string email = "jane@example.com", string? tenantId = null) => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        Email = email,
        LoginProvider = LoginProviders.MicrosoftEntraId,
        TenantId = tenantId,
        RoleNames = ["Contributor"],
    };

    // Arranges an Entra sign-in that finds no identity row for the token, so it reaches
    // the staged-link rebind. `user` is the only account in the database.
    private void ArrangeFirstSignIn(ApplicationUser user)
    {
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(["Contributor"]);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        _mockUserIdentityStore.Setup(s => s.FindActive(LoginProviders.MicrosoftEntraId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserIdentity?)null);
        _mockUserIdentityStore.Setup(s => s.FindActiveByNullTenant(LoginProviders.MicrosoftEntraId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserIdentity>());
    }

    [Fact]
    public async Task CreateAsync_ShouldNotWriteIdentity_WhenUserManagerCreateFails()
    {
        var command = new CreateUserCommand
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            LoginProvider = LoginProviders.Wayd,
            Password = "Password123!",
        };

        _mockUserManager
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), "Password123!"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Duplicate." }));

        var sut = CreateSut();

        var result = await sut.CreateAsync(command, TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        _mockUserIdentityStore.Verify(s => s.Add(It.IsAny<UserIdentity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ShouldSetEmployeeId_WhenProvided()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var command = new CreateUserCommand
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            LoginProvider = LoginProviders.MicrosoftEntraId,
            EmployeeId = employeeId,
        };

        ArrangeEntraProvider("tenant-1");
        _mockUserManager
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager
            .Setup(x => x.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        var result = await sut.CreateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockUserManager.Verify(x => x.CreateAsync(It.Is<ApplicationUser>(u =>
            u.EmployeeId == employeeId)), Times.Once);
    }

    #endregion

    #region UpdateProfile

    [Fact]
    public async Task UpdateProfile_ShouldChangeNameAndPhone_AndKeepTheEmailAndUserName()
    {
        // Arrange
        var user = CreateUser();
        var (email, userName, normalizedEmail, normalizedUserName) = (user.Email, user.UserName, user.NormalizedEmail, user.NormalizedUserName);
        var command = new UpdateProfileCommand { FirstName = "Updated", LastName = "Name", PhoneNumber = "555-1234" };

        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        await sut.UpdateProfile(command, "user-1");

        // Assert
        user.FirstName.Should().Be("Updated");
        user.LastName.Should().Be("Name");
        user.PhoneNumber.Should().Be("555-1234");
        user.Email.Should().Be(email, "the email is also the username and the people sync's link key, so a profile never sets it");
        user.UserName.Should().Be(userName);
        user.NormalizedEmail.Should().Be(normalizedEmail);
        user.NormalizedUserName.Should().Be(normalizedUserName);
        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
        _mockSignInManager.Verify(x => x.RefreshSignInAsync(user), Times.Once);
        RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserDetailsUpdatedEvent>();
    }

    [Fact]
    public async Task UpdateProfile_ShouldKeepTheEmployeeLink()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var user = CreateUser();
        user.EmployeeId = employeeId;
        var command = new UpdateProfileCommand { FirstName = "Updated", LastName = "User" };

        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        await sut.UpdateProfile(command, "user-1");

        // Assert
        user.EmployeeId.Should().Be(employeeId);
    }

    [Fact]
    public async Task UpdateProfile_ShouldThrowNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        _mockUserManager.Setup(x => x.FindByIdAsync("missing")).ReturnsAsync((ApplicationUser?)null);
        var command = new UpdateProfileCommand { FirstName = "F", LastName = "L" };

        var sut = CreateSut();

        // Act
        var act = () => sut.UpdateProfile(command, "missing");

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateProfile_ShouldThrowAndRecordNothing_WhenUpdateFails()
    {
        // Arrange
        var user = CreateUser();
        var command = new UpdateProfileCommand { FirstName = "Updated", LastName = "User" };

        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Update failed." }));

        var sut = CreateSut();

        // Act
        var act = () => sut.UpdateProfile(command, "user-1");

        // Assert
        await act.Should().ThrowAsync<InternalServerException>();
        RaisedEvents(user).Should().BeEmpty("nothing was saved, so nothing may be recorded");
        _mockSignInManager.Verify(x => x.RefreshSignInAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_ShouldUpdateUser_WhenUserExists()
    {
        // Arrange
        var user = CreateUser();
        var command = new UpdateUserCommand { Id = "user-1", FirstName = "Updated", LastName = "Name", Email = "updated@example.com", PhoneNumber = "555-1234" };

        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        await sut.UpdateAsync(command, "user-1");

        // Assert
        user.FirstName.Should().Be("Updated");
        user.LastName.Should().Be("Name");
        user.Email.Should().Be("updated@example.com");
        user.UserName.Should().Be("updated@example.com");
        user.NormalizedEmail.Should().Be("UPDATED@EXAMPLE.COM");
        user.NormalizedUserName.Should().Be("UPDATED@EXAMPLE.COM");
        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
        _mockSignInManager.Verify(x => x.RefreshSignInAsync(user), Times.Once);
        RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserDetailsUpdatedEvent>();
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        _mockUserManager.Setup(x => x.FindByIdAsync("missing")).ReturnsAsync((ApplicationUser?)null);
        var command = new UpdateUserCommand { Id = "missing", FirstName = "F", LastName = "L", Email = "e@e.com" };

        var sut = CreateSut();

        // Act
        var act = () => sut.UpdateAsync(command, "missing");

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_ShouldRaiseNothing_WhenNothingChanged()
    {
        // Arrange — the form sends every field on every save; an email differing only in case is kept as stored.
        var user = CreateUser();
        var command = new UpdateUserCommand { Id = "user-1", FirstName = "Test", LastName = "User", Email = "TEST@example.com" };

        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        await sut.UpdateAsync(command, "user-1");

        // Assert
        user.Email.Should().Be("test@example.com");
        RaisedEvents(user).Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrow_WhenUpdateFails()
    {
        // Arrange
        var user = CreateUser();
        var command = new UpdateUserCommand { Id = "user-1", FirstName = "Test", LastName = "User", Email = "test@example.com" };

        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Update failed." }));

        var sut = CreateSut();

        // Act
        var act = () => sut.UpdateAsync(command, "user-1");

        // Assert
        await act.Should().ThrowAsync<InternalServerException>();
        RaisedEvents(user).Should().BeEmpty("nothing was saved, so nothing may be recorded");
        _mockSignInManager.Verify(x => x.RefreshSignInAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ShouldPreserveEmployeeLink_WhenUpdateDoesNotManageIt()
    {
        // Arrange — a command that does not manage the link sends no employee id. Applying its
        // default null would silently unlink the user from their employee record.
        var employeeId = Guid.NewGuid();
        var user = CreateUser();
        user.EmployeeId = employeeId;
        var command = new UpdateUserCommand { Id = "user-1", FirstName = "Test", LastName = "User", Email = "test@example.com" };

        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        await sut.UpdateAsync(command, "user-1");

        // Assert
        user.EmployeeId.Should().Be(employeeId);
    }

    [Fact]
    public async Task UpdateAsync_ShouldSetEmployeeLink_WhenUpdateManagesIt()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var user = CreateUser();
        var command = new UpdateUserCommand
        {
            Id = "user-1",
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            EmployeeId = employeeId,
            ManageEmployeeLink = true
        };

        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        await sut.UpdateAsync(command, "user-1");

        // Assert
        user.EmployeeId.Should().Be(employeeId);
        var linked = RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserEmployeeLinkChangedEvent>().Subject;
        linked.PreviousEmployeeId.Should().BeNull();
        linked.EmployeeId.Should().Be(employeeId);
    }

    [Fact]
    public async Task UpdateAsync_ShouldClearEmployeeLink_WhenUpdateManagesItAndEmployeeIdIsNull()
    {
        // Arrange — unlinking stays possible, but only as an explicit act from the admin path.
        var user = CreateUser();
        user.EmployeeId = Guid.NewGuid();
        var command = new UpdateUserCommand
        {
            Id = "user-1",
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            EmployeeId = null,
            ManageEmployeeLink = true
        };

        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        await sut.UpdateAsync(command, "user-1");

        // Assert
        user.EmployeeId.Should().BeNull();
    }

    #endregion

    #region ExistsWithEmployeeIdAsync

    [Fact]
    public async Task ExistsWithEmployeeIdAsync_ShouldReturnTrue_WhenAnotherUserIsLinkedToTheEmployee()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var existing = CreateUser();
        existing.EmployeeId = employeeId;
        _mockUserManager.Setup(x => x.Users).Returns(new[] { existing }.AsQueryable().BuildMockDbSet().Object);

        var sut = CreateSut();

        // Act
        var result = await sut.ExistsWithEmployeeIdAsync(employeeId, "different-user");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsWithEmployeeIdAsync_ShouldReturnFalse_WhenOnlyTheExcludedUserIsLinked()
    {
        // Arrange — a user keeping their own existing link is not a conflict with themselves.
        var employeeId = Guid.NewGuid();
        var existing = CreateUser();
        existing.EmployeeId = employeeId;
        _mockUserManager.Setup(x => x.Users).Returns(new[] { existing }.AsQueryable().BuildMockDbSet().Object);

        var sut = CreateSut();

        // Act
        var result = await sut.ExistsWithEmployeeIdAsync(employeeId, existing.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsWithEmployeeIdAsync_ShouldReturnFalse_WhenNoUserIsLinkedToTheEmployee()
    {
        // Arrange
        var existing = CreateUser();
        existing.EmployeeId = null;
        _mockUserManager.Setup(x => x.Users).Returns(new[] { existing }.AsQueryable().BuildMockDbSet().Object);

        var sut = CreateSut();

        // Act
        var result = await sut.ExistsWithEmployeeIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetUserIdsForEmployees

    [Fact]
    public async Task GetUserIdsForEmployees_ReturnsTheUsersLinkedToThoseEmployees()
    {
        // Arrange
        var linkedEmployee = Guid.NewGuid();
        var otherLinkedEmployee = Guid.NewGuid();
        var unlinkedEmployee = Guid.NewGuid();
        var linked = CreateUser("user-linked", "linked");
        linked.EmployeeId = linkedEmployee;
        var otherLinked = CreateUser("user-other-linked", "other-linked");
        otherLinked.EmployeeId = otherLinkedEmployee;
        var someoneElse = CreateUser("user-someone-else", "someone-else");
        someoneElse.EmployeeId = Guid.NewGuid();
        var noEmployee = CreateUser("user-no-employee", "no-employee");
        _mockUserManager.Setup(x => x.Users)
            .Returns(new[] { linked, otherLinked, someoneElse, noEmployee }.AsQueryable().BuildMockDbSet().Object);

        var sut = CreateSut();

        // Act
        var result = await sut.GetUserIdsForEmployees([linkedEmployee, otherLinkedEmployee, unlinkedEmployee], TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEquivalentTo(["user-linked", "user-other-linked"]);
    }

    [Fact]
    public async Task GetUserIdsForEmployees_WithNoEmployees_ReadsNothing()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.GetUserIdsForEmployees([], TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
        _mockUserManager.Verify(x => x.Users, Times.Never);
    }

    #endregion

    #region GetEmployeeIdAsync

    [Fact]
    public async Task GetEmployeeIdAsync_ShouldReturnEmployeeId_WhenUserIsLinked()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        var user = CreateUser();
        user.EmployeeId = employeeId;
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);

        var sut = CreateSut();

        // Act
        var result = await sut.GetEmployeeIdAsync(user.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(employeeId);
    }

    [Fact]
    public async Task GetEmployeeIdAsync_ShouldReturnNull_WhenUserIsNotLinked()
    {
        // Arrange
        var user = CreateUser();
        user.EmployeeId = null;
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);

        var sut = CreateSut();

        // Act
        var result = await sut.GetEmployeeIdAsync(user.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetEmployeeIdAsync_ShouldReturnNull_WhenUserDoesNotExist()
    {
        // Arrange
        _mockUserManager.Setup(x => x.Users).Returns(Array.Empty<ApplicationUser>().AsQueryable().BuildMockDbSet().Object);

        var sut = CreateSut();

        // Act
        var result = await sut.GetEmployeeIdAsync("missing", TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetEmployeeIdAsync_ShouldReturnNull_WhenUserIdIsEmpty()
    {
        // Arrange — background scopes can reach here with no acting user; that is not a store lookup.
        var sut = CreateSut();

        // Act
        var result = await sut.GetEmployeeIdAsync(string.Empty, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
        _mockUserManager.Verify(x => x.Users, Times.Never);
    }

    #endregion

    #region ActivateUserAsync

    [Fact]
    public async Task ActivateUserAsync_ShouldActivateUser_WhenUserIsInactive()
    {
        // Arrange
        var user = CreateUser();
        user.IsActive = false;
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var command = new ActivateUserCommand("user-1");
        var sut = CreateSut();

        // Act
        var result = await sut.ActivateUserAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeTrue();
        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
        RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserActivatedEvent>();
    }

    [Fact]
    public async Task ActivateUserAsync_ShouldFailAndRecordNothing_WhenUpdateFails()
    {
        // Arrange
        var user = CreateUser();
        user.IsActive = false;
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Concurrency failure." }));

        var sut = CreateSut();

        // Act
        var result = await sut.ActivateUserAsync(new ActivateUserCommand("user-1"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Concurrency failure.");
        RaisedEvents(user).Should().BeEmpty("nothing was saved, so nothing may be recorded");
    }

    [Fact]
    public async Task ActivateUserAsync_ShouldFail_WhenUserIsAlreadyActive()
    {
        // Arrange
        var user = CreateUser();
        user.IsActive = true;
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);

        var command = new ActivateUserCommand("user-1");
        var sut = CreateSut();

        // Act
        var result = await sut.ActivateUserAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _mockUserManager.Verify(x => x.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task ActivateUserAsync_ShouldThrowNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        _mockUserManager.Setup(x => x.Users).Returns(Array.Empty<ApplicationUser>().AsQueryable().BuildMockDbSet().Object);

        var command = new ActivateUserCommand("missing");
        var sut = CreateSut();

        // Act
        var act = () => sut.ActivateUserAsync(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region Ending sessions

    [Fact]
    public async Task ChangePasswordAsync_EndsEveryOneOfTheUsersSessions()
    {
        // Arrange
        var user = CreateUser(loginProvider: LoginProviders.Wayd);
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.ChangePasswordAsync(user, "OldPass123!", "NewPass456!")).ReturnsAsync(IdentityResult.Success);
        await _refreshTokenStore.Issue(user.Id, SessionContext.None, TestContext.Current.CancellationToken);
        await _refreshTokenStore.Issue(user.Id, SessionContext.None, TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        var result = await sut.ChangePasswordAsync("user-1", new ChangePasswordCommand("OldPass123!", "NewPass456!"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _refreshTokenStore.ActiveSessionCount(user.Id).Should().Be(0);
        _refreshTokenStore.RevokeReasons(user.Id).Should().AllBe(UserRefreshTokenRevokeReasons.PasswordChanged);
    }

    [Fact]
    public async Task ChangePasswordAsync_CountsAWrongCurrentPassword_TowardLockout()
    {
        // Arrange
        var user = CreateUser(loginProvider: LoginProviders.Wayd);
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.ChangePasswordAsync(user, "WrongPass1!", "NewPass456!"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityErrorDescriber().PasswordMismatch()));
        _mockUserManager.Setup(x => x.AccessFailedAsync(user)).ReturnsAsync(IdentityResult.Success);
        var sut = CreateSut();

        // Act
        var result = await sut.ChangePasswordAsync("user-1", new ChangePasswordCommand("WrongPass1!", "NewPass456!"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _mockUserManager.Verify(x => x.AccessFailedAsync(user), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_DoesNotCountAPasswordThatIsMerelyTooWeak()
    {
        // Arrange — the current password was right; only the new one was refused
        var user = CreateUser(loginProvider: LoginProviders.Wayd);
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.ChangePasswordAsync(user, "OldPass123!", "weak"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityErrorDescriber().PasswordTooShort(8)));
        var sut = CreateSut();

        // Act
        await sut.ChangePasswordAsync("user-1", new ChangePasswordCommand("OldPass123!", "weak"), TestContext.Current.CancellationToken);

        // Assert
        _mockUserManager.Verify(x => x.AccessFailedAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_RefusesALockedOutUser_WithoutCheckingThePassword()
    {
        // Arrange
        var user = CreateUser(loginProvider: LoginProviders.Wayd);
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(true);
        var sut = CreateSut();

        // Act
        var result = await sut.ChangePasswordAsync("user-1", new ChangePasswordCommand("OldPass123!", "NewPass456!"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _mockUserManager.Verify(x => x.ChangePasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_CountsTheLastAdministratorsWrongPassword_WithoutLockingThemOut()
    {
        // Arrange
        var user = CreateUser(loginProvider: LoginProviders.Wayd);
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync([ApplicationRoles.Admin]);
        _mockUserManager.Setup(x => x.GetUsersInRoleAsync(ApplicationRoles.Admin)).ReturnsAsync([user]);
        _mockUserManager.Setup(x => x.ChangePasswordAsync(user, "WrongPass1!", "NewPass456!"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityErrorDescriber().PasswordMismatch()));
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        var sut = CreateSut();

        // Act
        await sut.ChangePasswordAsync("user-1", new ChangePasswordCommand("WrongPass1!", "NewPass456!"), TestContext.Current.CancellationToken);

        // Assert
        user.AccessFailedCount.Should().Be(1);
        _mockUserManager.Verify(x => x.AccessFailedAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_RecordsTheLockout_WhenAWrongPasswordStartsOne()
    {
        // Arrange
        var user = CreateUser(loginProvider: LoginProviders.Wayd);
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.ChangePasswordAsync(user, "WrongPass1!", "NewPass456!"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityErrorDescriber().PasswordMismatch()));
        _mockUserManager.Setup(x => x.AccessFailedAsync(user)).ReturnsAsync(IdentityResult.Success)
            .Callback(() => user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15));
        _mockUserManager.SetupSequence(x => x.IsLockedOutAsync(user)).ReturnsAsync(false).ReturnsAsync(true);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        var sut = CreateSut();

        // Act
        await sut.ChangePasswordAsync("user-1", new ChangePasswordCommand("WrongPass1!", "NewPass456!"), TestContext.Current.CancellationToken);

        // Assert
        RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserLockedOutEvent>();
    }

    [Fact]
    public async Task ChangePasswordAsync_RefusesTheLastAdministrator_WhileTheirNextAttemptIsHeldOff()
    {
        // Arrange — a stolen session of the only administrator is throttled as their sign-in is
        var user = CreateUser(loginProvider: LoginProviders.Wayd);
        user.AccessFailedCount = 6;
        user.LockoutEnd = _dateTimeProvider.Now.ToDateTimeOffset().AddSeconds(2);
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync([ApplicationRoles.Admin]);
        _mockUserManager.Setup(x => x.GetUsersInRoleAsync(ApplicationRoles.Admin)).ReturnsAsync([user]);
        var sut = CreateSut();

        // Act
        var result = await sut.ChangePasswordAsync("user-1", new ChangePasswordCommand("Guess1!", "NewPass456!"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _mockUserManager.Verify(x => x.ChangePasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_RecordsWhenTheTemporaryPasswordWasSet()
    {
        // Arrange
        ApplicationUser? created = null;
        _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), "Password123!"))
            .Callback<ApplicationUser, string>((u, _) => created = u)
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>())).ReturnsAsync(IdentityResult.Success);
        var sut = CreateSut();

        // Act
        await sut.CreateAsync(new CreateUserCommand
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            LoginProvider = LoginProviders.Wayd,
            Password = "Password123!",
            MustChangePassword = true,
            RoleNames = ["Contributor"],
        }, TestContext.Current.CancellationToken);

        // Assert
        created!.MustChangePassword.Should().BeTrue();
        created.TemporaryPasswordSetAt.Should().Be(_dateTimeProvider.Now);
    }

    [Fact]
    public async Task ChangePasswordAsync_ClearsTheTemporaryPasswordsAge()
    {
        // Arrange
        var user = CreateUser(loginProvider: LoginProviders.Wayd);
        user.MustChangePassword = true;
        user.TemporaryPasswordSetAt = _dateTimeProvider.Now;
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.ChangePasswordAsync(user, "OldPass123!", "NewPass456!")).ReturnsAsync(IdentityResult.Success);
        var sut = CreateSut();

        // Act
        await sut.ChangePasswordAsync("user-1", new ChangePasswordCommand("OldPass123!", "NewPass456!"), TestContext.Current.CancellationToken);

        // Assert
        user.TemporaryPasswordSetAt.Should().BeNull();
    }

    [Fact]
    public async Task ResetPasswordAsync_RecordsWhenTheTemporaryPasswordWasSet()
    {
        // Arrange
        var user = CreateUser(loginProvider: LoginProviders.Wayd);
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-token");
        _mockUserManager.Setup(x => x.ResetPasswordAsync(user, "reset-token", "NewPass456!")).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);
        var sut = CreateSut();

        // Act
        await sut.ResetPasswordAsync(new ResetPasswordCommand("user-1", "NewPass456!"), TestContext.Current.CancellationToken);

        // Assert
        user.TemporaryPasswordSetAt.Should().Be(_dateTimeProvider.Now);
    }

    [Fact]
    public async Task ChangePasswordAsync_EndsNoSession_WhenTheChangeIsRefused()
    {
        // Arrange
        var user = CreateUser(loginProvider: LoginProviders.Wayd);
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.ChangePasswordAsync(user, "WrongPass1!", "NewPass456!"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Incorrect password." }));
        await _refreshTokenStore.Issue(user.Id, SessionContext.None, TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        var result = await sut.ChangePasswordAsync("user-1", new ChangePasswordCommand("WrongPass1!", "NewPass456!"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _refreshTokenStore.ActiveSessionCount(user.Id).Should().Be(1);
    }

    [Fact]
    public async Task ResetPasswordAsync_EndsEveryOneOfTheUsersSessions()
    {
        // Arrange
        var user = CreateUser(loginProvider: LoginProviders.Wayd);
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-token");
        _mockUserManager.Setup(x => x.ResetPasswordAsync(user, "reset-token", "NewPass456!")).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);
        await _refreshTokenStore.Issue(user.Id, SessionContext.None, TestContext.Current.CancellationToken);
        await _refreshTokenStore.Issue(user.Id, SessionContext.None, TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        var result = await sut.ResetPasswordAsync(new ResetPasswordCommand("user-1", "NewPass456!"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _refreshTokenStore.ActiveSessionCount(user.Id).Should().Be(0);
        _refreshTokenStore.RevokeReasons(user.Id).Should().AllBe(UserRefreshTokenRevokeReasons.PasswordReset);
    }

    [Fact]
    public async Task ResetPasswordAsync_EndsNoSession_WhenTheResetIsRefused()
    {
        // Arrange
        var user = CreateUser(loginProvider: LoginProviders.Wayd);
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-token");
        _mockUserManager.Setup(x => x.ResetPasswordAsync(user, "reset-token", "weak"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Too short." }));
        _mockUserManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);
        await _refreshTokenStore.Issue(user.Id, SessionContext.None, TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        var result = await sut.ResetPasswordAsync(new ResetPasswordCommand("user-1", "weak"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _refreshTokenStore.ActiveSessionCount(user.Id).Should().Be(1);
    }

    [Fact]
    public async Task DeactivateUserAsync_EndsEveryOneOfTheUsersSessions()
    {
        // Arrange
        var user = CreateUser();
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        await _refreshTokenStore.Issue(user.Id, SessionContext.None, TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        var result = await sut.DeactivateUserAsync(new DeactivateUserCommand("user-1"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _refreshTokenStore.ActiveSessionCount(user.Id).Should().Be(0);
        _refreshTokenStore.RevokeReasons(user.Id).Should().Equal(UserRefreshTokenRevokeReasons.Deactivated);
    }

    [Fact]
    public async Task DeactivateUserAsync_EndsNoSession_WhenTheSaveIsRefused()
    {
        // Arrange
        var user = CreateUser();
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Concurrency failure." }));
        await _refreshTokenStore.Issue(user.Id, SessionContext.None, TestContext.Current.CancellationToken);
        var sut = CreateSut();

        // Act
        var result = await sut.DeactivateUserAsync(new DeactivateUserCommand("user-1"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _refreshTokenStore.ActiveSessionCount(user.Id).Should().Be(1);
    }

    [Fact]
    public async Task GetPermissionsAsync_GrantsNothing_ToADeactivatedUser()
    {
        // Arrange — a web access token outlives the deactivation, so this is what stops it acting
        var user = CreateUser(isActive: false);
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync([ApplicationRoles.Admin]);
        var sut = CreateSut();

        // Act
        var permissions = await sut.GetPermissionsAsync("user-1", TestContext.Current.CancellationToken);

        // Assert
        permissions.Should().BeEmpty();
    }

    #endregion

    #region Privileged accounts

    private static readonly string ViewUsers = ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Users);
    private static readonly string UpdateUsers = ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.Users);
    private static readonly string UpdateUserRoles = ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.UserRoles);
    private static readonly string ManageRoles = ApplicationPermission.NameFor(ApplicationAction.Manage, ApplicationResource.Roles);
    private static readonly string ManagePrivilegedUsers = ApplicationPermission.NameFor(ApplicationAction.Manage, ApplicationResource.PrivilegedUsers);
    private static readonly string UpdateProjects = ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.Projects);
    private static readonly string[] EveryPermission = [.. ApplicationPermissions.Admin.Select(p => p.Name)];

    /// <summary>Makes the caller hold <paramref name="roles"/> in place of Admin.</summary>
    private void CallerHolds(params string[] roles) =>
        _mockUserManager.Setup(x => x.GetRolesAsync(_caller)).ReturnsAsync(roles);

    /// <summary>Makes the role named <paramref name="roleName"/> grant <paramref name="permissions"/>.</summary>
    private void RoleGrants(string roleName, params string[] permissions) =>
        _mockRoleManager.Setup(x => x.GetClaimsAsync(It.Is<ApplicationRole>(r => r.Name == roleName)))
            .ReturnsAsync([.. permissions.Select(p => new Claim(ApplicationClaims.Permission, p))]);

    /// <summary>Makes <paramref name="members"/> the users holding the role named <paramref name="roleName"/>.</summary>
    private void RoleHeldBy(string roleName, params ApplicationUser[] members) =>
        _mockUserManager.Setup(x => x.GetUsersInRoleAsync(roleName)).ReturnsAsync(members);

    /// <summary>A local user in <paramref name="roles"/>, found by id.</summary>
    private ApplicationUser TargetIn(string[] roles, string loginProvider = LoginProviders.Wayd, bool isActive = true)
    {
        var target = CreateUser(loginProvider: loginProvider, isActive: isActive);
        _mockUserManager.Setup(x => x.FindByIdAsync(target.Id)).ReturnsAsync(target);
        _mockUserManager.Setup(x => x.Users).Returns(new[] { target, _caller }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.GetRolesAsync(target)).ReturnsAsync(roles);
        _mockUserManager.Setup(x => x.UpdateAsync(target)).ReturnsAsync(IdentityResult.Success);
        return target;
    }

    /// <summary>A caller who administers users but cannot reach privileged accounts.</summary>
    private void CallerIsAUserAdministrator()
    {
        CallerHolds("Basic");
        RoleGrants("Basic", ViewUsers, UpdateUsers, UpdateUserRoles);
    }

    private void PasswordResetSucceeds(ApplicationUser target)
    {
        _mockUserManager.Setup(x => x.GeneratePasswordResetTokenAsync(target)).ReturnsAsync("reset-token");
        _mockUserManager.Setup(x => x.ResetPasswordAsync(target, "reset-token", "NewPass456!")).ReturnsAsync(IdentityResult.Success);
    }

    [Fact]
    public async Task ResetPasswordAsync_IsForbidden_WhenTheTargetHoldsAPrivilegedPermission()
    {
        // Arrange
        CallerIsAUserAdministrator();
        RoleGrants("Contributor", ViewUsers, ManageRoles);
        var target = TargetIn(["Contributor"]);
        var sut = CreateSut();

        // Act
        var act = () => sut.ResetPasswordAsync(new ResetPasswordCommand(target.Id, "NewPass456!"), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*Manage Privileged Users*");
        _mockUserManager.Verify(x => x.ResetPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        RaisedEvents(target).Should().BeEmpty();
    }

    [Fact]
    public async Task ResetPasswordAsync_IsAllowed_ForATargetHoldingOrdinaryPermissionsTheCallerLacks()
    {
        // Arrange — user administration is delegable: the caller need not hold what an ordinary target holds.
        CallerIsAUserAdministrator();
        RoleGrants("Contributor", ViewUsers, UpdateProjects);
        var target = TargetIn(["Contributor"]);
        PasswordResetSucceeds(target);
        var sut = CreateSut();

        // Act
        var result = await sut.ResetPasswordAsync(new ResetPasswordCommand(target.Id, "NewPass456!"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ResetPasswordAsync_IsAllowed_ForAPrivilegedTarget_WhenTheCallerMayManagePrivilegedUsers()
    {
        // Arrange
        CallerHolds("Basic");
        RoleGrants("Basic", ViewUsers, UpdateUsers, ManagePrivilegedUsers);
        var target = TargetIn([ApplicationRoles.Admin]);
        PasswordResetSucceeds(target);
        var sut = CreateSut();

        // Act
        var result = await sut.ResetPasswordAsync(new ResetPasswordCommand(target.Id, "NewPass456!"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ResetPasswordAsync_IsForbidden_WhenThePrivilegedTargetIsInactive()
    {
        // Arrange — a deactivated administrator's roles still count, or they could be reset and then reactivated.
        CallerIsAUserAdministrator();
        var target = TargetIn([ApplicationRoles.Admin], isActive: false);
        var sut = CreateSut();

        // Act
        var act = () => sut.ResetPasswordAsync(new ResetPasswordCommand(target.Id, "NewPass456!"), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task ResetPasswordAsync_IsAllowed_WhenTheTargetHoldsOnlyARetiredPrivilegedClaim()
    {
        // Arrange — a claim the catalog no longer defines is checked by no endpoint, so it grants nothing.
        CallerIsAUserAdministrator();
        RoleGrants("Contributor", ViewUsers, "Permissions.RoleClaims.Update");
        var target = TargetIn(["Contributor"]);
        PasswordResetSucceeds(target);
        var sut = CreateSut();

        // Act
        var result = await sut.ResetPasswordAsync(new ResetPasswordCommand(target.Id, "NewPass456!"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ResetPasswordAsync_IsForbidden_ForAPrivilegedTarget_WhenTheCallerIsInactive()
    {
        // Arrange — a session issued before the caller was deactivated reaches nothing.
        _caller.IsActive = false;
        var target = TargetIn([ApplicationRoles.Admin]);
        var sut = CreateSut();

        // Act
        var act = () => sut.ResetPasswordAsync(new ResetPasswordCommand(target.Id, "NewPass456!"), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task DeactivateUserAsync_IsForbidden_WhenTheTargetIsPrivileged()
    {
        // Arrange
        CallerIsAUserAdministrator();
        var target = TargetIn([ApplicationRoles.Admin]);
        var sut = CreateSut();

        // Act
        var act = () => sut.DeactivateUserAsync(new DeactivateUserCommand(target.Id), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        target.IsActive.Should().BeTrue();
        _refreshTokenStore.RevokeReasons(target.Id).Should().BeEmpty();
    }

    [Fact]
    public async Task ActivateUserAsync_IsForbidden_WhenTheTargetIsPrivileged()
    {
        // Arrange
        CallerIsAUserAdministrator();
        var target = TargetIn([ApplicationRoles.Admin], isActive: false);
        var sut = CreateSut();

        // Act
        var act = () => sut.ActivateUserAsync(new ActivateUserCommand(target.Id), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        target.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UnlockUserAsync_IsForbidden_WhenTheTargetIsPrivileged()
    {
        // Arrange
        CallerIsAUserAdministrator();
        var target = TargetIn([ApplicationRoles.Admin]);
        _mockUserManager.Setup(x => x.IsLockedOutAsync(target)).ReturnsAsync(true);
        var sut = CreateSut();

        // Act
        var act = () => sut.UnlockUserAsync(target.Id);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        RaisedEvents(target).Should().BeEmpty();
    }

    [Fact]
    public async Task ConvertToLocalAccount_IsForbidden_WhenTheTargetIsPrivileged()
    {
        // Arrange
        CallerIsAUserAdministrator();
        RoleGrants("Contributor", ManageRoles);
        var target = TargetIn(["Contributor"], loginProvider: LoginProviders.MicrosoftEntraId);
        var sut = CreateSut();

        // Act
        var act = () => sut.ConvertToLocalAccount(new ConvertToLocalAccountCommand(target.Id, "NewPass456!"), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        _mockUserIdentityStore.Verify(s => s.ExecuteInTransaction(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StageProviderMigration_IsForbidden_WhenTheTargetIsPrivileged()
    {
        // Arrange
        CallerIsAUserAdministrator();
        var target = TargetIn([ApplicationRoles.Admin], loginProvider: LoginProviders.MicrosoftEntraId);
        var sut = CreateSut();

        // Act
        var act = () => sut.StageProviderMigration(new StageProviderMigrationCommand(target.Id, "Acme-Okta"), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        target.PendingMigrationProviderId.Should().BeNull();
    }

    [Fact]
    public async Task StageEntraSignInTenant_IsForbidden_WhenTheTargetIsPrivileged()
    {
        // Arrange
        CallerIsAUserAdministrator();
        var target = TargetIn([ApplicationRoles.Admin], loginProvider: LoginProviders.MicrosoftEntraId);
        var sut = CreateSut();

        // Act
        var act = () => sut.StageEntraSignInTenant(new StageEntraSignInTenantCommand(target.Id, null), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        target.PendingMigrationTenantId.Should().BeNull();
    }

    [Fact]
    public async Task CancelTenantMigration_IsForbidden_WhenTheTargetIsPrivileged()
    {
        // Arrange
        CallerIsAUserAdministrator();
        var target = TargetIn([ApplicationRoles.Admin], loginProvider: LoginProviders.MicrosoftEntraId);
        target.PendingMigrationTenantId = "staged-tenant";
        target.PendingMigrationStagedAt = _dateTimeProvider.Now;
        var sut = CreateSut();

        // Act
        var act = () => sut.CancelTenantMigration(target.Id, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        target.PendingMigrationTenantId.Should().Be("staged-tenant");
    }

    [Fact]
    public async Task CancelProviderMigration_IsForbidden_WhenTheTargetIsPrivileged()
    {
        // Arrange
        CallerIsAUserAdministrator();
        var target = TargetIn([ApplicationRoles.Admin], loginProvider: LoginProviders.MicrosoftEntraId);
        target.PendingMigrationProviderId = "Acme-Okta";
        target.PendingProviderMigrationStagedAt = _dateTimeProvider.Now;
        var sut = CreateSut();

        // Act
        var act = () => sut.CancelProviderMigration(target.Id, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        target.PendingMigrationProviderId.Should().Be("Acme-Okta");
    }

    private static CreateUserCommand NewUserIn(params string[] roles) => new()
    {
        FirstName = "John",
        LastName = "Doe",
        Email = "john@example.com",
        LoginProvider = LoginProviders.Wayd,
        Password = "Password123!",
        RoleNames = [.. roles],
    };

    [Fact]
    public async Task CreateAsync_IsForbidden_WhenARoleGrantsAPrivilegedPermission()
    {
        // Arrange
        CallerIsAUserAdministrator();
        RoleGrants("Contributor", ViewUsers, ManageRoles);
        var sut = CreateSut();

        // Act
        var act = () => sut.CreateAsync(NewUserIn("Contributor"), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*Manage Privileged Users*");
        _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_AssignsAnOrdinaryRole_TheCallerDoesNotHold()
    {
        // Arrange
        CallerIsAUserAdministrator();
        RoleGrants("Contributor", ViewUsers, UpdateProjects);
        _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), "Password123!")).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>())).ReturnsAsync(IdentityResult.Success);
        var sut = CreateSut();

        // Act
        var result = await sut.CreateAsync(NewUserIn("Contributor"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    private static UpdateUserCommand AdminEdit(ApplicationUser user, Guid? employeeId, string? email = null) => new()
    {
        Id = user.Id,
        FirstName = user.FirstName!,
        LastName = user.LastName!,
        Email = email ?? user.Email!,
        EmployeeId = employeeId,
        ManageEmployeeLink = true,
    };

    [Fact]
    public async Task UpdateAsync_IsForbidden_WhenTheCallerChangesTheEmployeeLinkedToTheirOwnAccount()
    {
        // Arrange — an Admin is refused too: linking yourself takes on that employee's standing.
        _mockUserManager.Setup(x => x.UpdateAsync(_caller)).ReturnsAsync(IdentityResult.Success);
        var sut = CreateSut();

        // Act
        var act = () => sut.UpdateAsync(AdminEdit(_caller, Guid.NewGuid()), CallerId);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*your own account*");
        _caller.EmployeeId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_IsForbidden_WhenTheCallerChangesTheEmailOfTheirOwnAccount()
    {
        // Arrange — people sync links an unlinked user by email, so a new address of your own choosing would
        // link you to whichever employee holds it.
        _mockUserManager.Setup(x => x.UpdateAsync(_caller)).ReturnsAsync(IdentityResult.Success);
        var email = _caller.Email;
        var command = AdminEdit(_caller, null, email: "unlinked.employee@example.com");
        command.ManageEmployeeLink = false;
        var sut = CreateSut();

        // Act
        var act = () => sut.UpdateAsync(command, CallerId);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*your own account*");
        _caller.Email.Should().Be(email);
        _mockUserManager.Verify(x => x.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ChangesTheEmailOfAnotherUser()
    {
        // Arrange
        CallerIsAUserAdministrator();
        RoleGrants("Contributor", ViewUsers);
        var target = TargetIn(["Contributor"]);
        var sut = CreateSut();

        // Act
        await sut.UpdateAsync(AdminEdit(target, null, email: "moved@example.com"), target.Id);

        // Assert
        target.Email.Should().Be("moved@example.com");
        target.UserName.Should().Be("moved@example.com");
    }

    [Fact]
    public async Task UpdateAsync_LinksAnEmployee_ToAnOrdinaryUser()
    {
        // Arrange
        CallerIsAUserAdministrator();
        RoleGrants("Contributor", ViewUsers);
        var target = TargetIn(["Contributor"]);
        var employeeId = Guid.NewGuid();
        var sut = CreateSut();

        // Act
        await sut.UpdateAsync(AdminEdit(target, employeeId), target.Id);

        // Assert
        target.EmployeeId.Should().Be(employeeId);
    }

    [Fact]
    public async Task UpdateAsync_IsForbidden_ForAnyChangeToAPrivilegedUser()
    {
        // Arrange — the rule is the account, not which field: renaming is refused as linking is.
        CallerIsAUserAdministrator();
        var target = TargetIn([ApplicationRoles.Admin]);
        var command = AdminEdit(target, null);
        command.FirstName = "Renamed";
        var sut = CreateSut();

        // Act
        var act = () => sut.UpdateAsync(command, target.Id);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        target.FirstName.Should().Be("Test");
    }

    [Fact]
    public async Task UpdateAsync_SavesTheCallersOwnDetails_WhenTheyArePrivileged()
    {
        // Arrange — an administrator editing their own account through Settings → Access → Users.
        _mockUserManager.Setup(x => x.UpdateAsync(_caller)).ReturnsAsync(IdentityResult.Success);
        var command = AdminEdit(_caller, null);
        command.ManageEmployeeLink = false;
        command.FirstName = "Renamed";
        var sut = CreateSut();

        // Act
        await sut.UpdateAsync(command, CallerId);

        // Assert
        _caller.FirstName.Should().Be("Renamed");
    }

    [Fact]
    public async Task AssignRolesAsync_IsForbidden_OnTheCallersOwnAccount()
    {
        // Arrange
        _mockUserManager.Setup(x => x.Users).Returns(new[] { _caller }.AsQueryable().BuildMockDbSet().Object);
        var sut = CreateSut();

        // Act
        var act = () => sut.AssignRolesAsync(new AssignUserRolesCommand(CallerId, [ApplicationRoles.Admin, "Basic"]), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*your own roles*");
    }

    [Fact]
    public async Task AssignRolesAsync_IsForbidden_WhenGrantingAPrivilegedRole()
    {
        // Arrange
        CallerIsAUserAdministrator();
        RoleGrants("Contributor", ViewUsers);
        var target = TargetIn(["Contributor"]);
        var sut = CreateSut();

        // Act
        var act = () => sut.AssignRolesAsync(new AssignUserRolesCommand(target.Id, ["Contributor", ApplicationRoles.Admin]), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*Manage Privileged Users*");
        _mockUserManager.Verify(x => x.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task AssignRolesAsync_IsForbidden_WhenTheTargetIsPrivileged()
    {
        // Arrange — taking a privileged role away needs the same reach as granting one.
        CallerIsAUserAdministrator();
        var target = TargetIn([ApplicationRoles.Admin]);
        var sut = CreateSut();

        // Act
        var act = () => sut.AssignRolesAsync(new AssignUserRolesCommand(target.Id, ["Basic"]), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        _mockUserManager.Verify(x => x.RemoveFromRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task ManageRoleUsersAsync_IsForbidden_WhenTheCallerIsAmongTheUsers()
    {
        // Arrange
        _mockRoleManager.Setup(x => x.FindByIdAsync(BasicRoleId)).ReturnsAsync(new ApplicationRole("Basic") { Id = BasicRoleId });
        _mockUserManager.Setup(x => x.Users).Returns(new[] { _caller }.AsQueryable().BuildMockDbSet().Object);
        var sut = CreateSut();

        // Act
        var act = () => sut.ManageRoleUsersAsync(new ManageRoleUsersCommand(BasicRoleId, [CallerId], []), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*your own roles*");
    }

    [Fact]
    public async Task ManageRoleUsersAsync_IsForbidden_WhenAddingUsersToAPrivilegedRole()
    {
        // Arrange
        CallerIsAUserAdministrator();
        RoleGrants("Contributor", ManageRoles);
        _mockRoleManager.Setup(x => x.FindByIdAsync(ContributorRoleId)).ReturnsAsync(new ApplicationRole("Contributor") { Id = ContributorRoleId });
        var target = TargetIn([]);
        var sut = CreateSut();

        // Act
        var act = () => sut.ManageRoleUsersAsync(new ManageRoleUsersCommand(ContributorRoleId, [target.Id], []), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*Manage Privileged Users*");
        _mockUserManager.Verify(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region The last administrator

    /// <summary>
    /// A caller who may reach privileged accounts without holding every permission, so they are not an administrator
    /// themselves and can be the one whose change would leave none.
    /// </summary>
    private void CallerIsAPrivilegedUserAdministrator()
    {
        CallerHolds("Basic");
        RoleGrants("Basic", ViewUsers, UpdateUsers, UpdateUserRoles, ManagePrivilegedUsers);
        RoleHeldBy("Basic", _caller);
    }

    /// <summary>The only active user holding every permission, through the Admin role.</summary>
    private ApplicationUser TheOnlyAdministrator()
    {
        var target = TargetIn([ApplicationRoles.Admin]);
        RoleHeldBy(ApplicationRoles.Admin, target);
        return target;
    }

    [Fact]
    public async Task DeactivateUserAsync_Fails_WhenTheTargetIsTheOnlyActiveAdministrator()
    {
        // Arrange
        CallerIsAPrivilegedUserAdministrator();
        var target = TheOnlyAdministrator();
        RoleHeldBy(ApplicationRoles.Admin, target, CreateUser(id: "inactive-admin", isActive: false));
        var sut = CreateSut();

        // Act
        var result = await sut.DeactivateUserAsync(new DeactivateUserCommand(target.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("holds every permission");
        target.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DeactivateUserAsync_Succeeds_WhenAnotherUserHoldsEveryPermissionThroughTheirOwnRole()
    {
        // Arrange — an administrator is whoever holds every permission, whichever role grants it.
        CallerIsAPrivilegedUserAdministrator();
        var target = TheOnlyAdministrator();
        RoleGrants("Contributor", EveryPermission);
        RoleHeldBy("Contributor", CreateUser(id: "custom-admin"));
        var sut = CreateSut();

        // Act
        var result = await sut.DeactivateUserAsync(new DeactivateUserCommand(target.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        target.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task AssignRolesAsync_ThrowsConflict_WhenItTakesAwayTheOnlyAdministratorsAccess()
    {
        // Arrange
        CallerIsAPrivilegedUserAdministrator();
        var target = TheOnlyAdministrator();
        var sut = CreateSut();

        // Act
        var act = () => sut.AssignRolesAsync(new AssignUserRolesCommand(target.Id, ["Basic"]), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*holds every permission*");
    }

    [Fact]
    public async Task ManageRoleUsersAsync_SavesNothing_WhenTheRemovalsAreRefused()
    {
        // Arrange — the additions come first, so a refusal found only afterwards would leave them committed. The
        // joining user is inactive, so they do not replace the administrator being removed.
        CallerIsAPrivilegedUserAdministrator();
        var target = TheOnlyAdministrator();
        var joining = CreateUser(id: "joining", isActive: false);
        _mockUserManager.Setup(x => x.Users).Returns(new[] { target, joining, _caller }.AsQueryable().BuildMockDbSet().Object);
        _mockRoleManager.Setup(x => x.FindByIdAsync(AdminRoleId)).ReturnsAsync(new ApplicationRole(ApplicationRoles.Admin) { Id = AdminRoleId });
        _mockUserManager.Setup(x => x.GetRolesAsync(joining)).ReturnsAsync([]);
        var sut = CreateSut();

        // Act
        var result = await sut.ManageRoleUsersAsync(new ManageRoleUsersCommand(AdminRoleId, [joining.Id], [target.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _mockUserManager.Verify(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        _mockUserManager.Verify(x => x.RemoveFromRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ManageRoleUsersAsync_SwapsOneAdministratorForAnother()
    {
        // Arrange
        CallerIsAPrivilegedUserAdministrator();
        var target = TheOnlyAdministrator();
        var joining = CreateUser(id: "joining");
        _mockUserManager.Setup(x => x.Users).Returns(new[] { target, joining, _caller }.AsQueryable().BuildMockDbSet().Object);
        _mockRoleManager.Setup(x => x.FindByIdAsync(AdminRoleId)).ReturnsAsync(new ApplicationRole(ApplicationRoles.Admin) { Id = AdminRoleId });
        _mockUserManager.Setup(x => x.GetRolesAsync(joining)).ReturnsAsync([]);
        _mockUserManager.Setup(x => x.IsInRoleAsync(joining, ApplicationRoles.Admin)).ReturnsAsync(false);
        _mockUserManager.Setup(x => x.IsInRoleAsync(target, ApplicationRoles.Admin)).ReturnsAsync(true);
        _mockUserManager.Setup(x => x.AddToRoleAsync(joining, ApplicationRoles.Admin)).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.RemoveFromRoleAsync(target, ApplicationRoles.Admin)).ReturnsAsync(IdentityResult.Success);
        var sut = CreateSut();

        // Act
        var result = await sut.ManageRoleUsersAsync(new ManageRoleUsersCommand(AdminRoleId, [joining.Id], [target.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _mockUserManager.Verify(x => x.RemoveFromRoleAsync(target, ApplicationRoles.Admin), Times.Once);
    }

    [Fact]
    public async Task ManageRoleUsersAsync_Fails_WhenRemovingTheOnlyAdministratorFromTheRoleThatMakesThemOne()
    {
        // Arrange
        CallerIsAPrivilegedUserAdministrator();
        var target = TheOnlyAdministrator();
        _mockRoleManager.Setup(x => x.FindByIdAsync(AdminRoleId)).ReturnsAsync(new ApplicationRole(ApplicationRoles.Admin) { Id = AdminRoleId });
        var sut = CreateSut();

        // Act
        var result = await sut.ManageRoleUsersAsync(new ManageRoleUsersCommand(AdminRoleId, [], [target.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("holds every permission");
        _mockUserManager.Verify(x => x.RemoveFromRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region DeactivateUserAsync

    [Fact]
    public async Task DeactivateUserAsync_ShouldDeactivateUser_WhenUserIsActive()
    {
        // Arrange
        var user = CreateUser();
        user.IsActive = true;
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var command = new DeactivateUserCommand("user-1");
        var sut = CreateSut();

        // Act
        var result = await sut.DeactivateUserAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeFalse();
        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
        RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserDeactivatedEvent>();
    }

    [Fact]
    public async Task DeactivateUserAsync_ShouldSucceed_WhenDeactivatingAnotherAdmin()
    {
        // Arrange
        var adminUser = CreateUser(id: "other-admin");
        adminUser.IsActive = true;
        _mockUserManager.Setup(x => x.Users).Returns(new[] { adminUser }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.UpdateAsync(adminUser)).ReturnsAsync(IdentityResult.Success);

        var command = new DeactivateUserCommand("other-admin");
        var sut = CreateSut();

        // Act
        var result = await sut.DeactivateUserAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        adminUser.IsActive.Should().BeFalse();
        _mockUserManager.Verify(x => x.UpdateAsync(adminUser), Times.Once);
        RaisedEvents(adminUser).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserDeactivatedEvent>();
    }

    [Fact]
    public async Task DeactivateUserAsync_ShouldFail_WhenUserIsAlreadyInactive()
    {
        // Arrange
        var user = CreateUser();
        user.IsActive = false;
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);

        var command = new DeactivateUserCommand("user-1");
        var sut = CreateSut();

        // Act
        var result = await sut.DeactivateUserAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _mockUserManager.Verify(x => x.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task DeactivateUserAsync_ShouldFail_WhenDeactivatingSelf()
    {
        // Arrange
        var user = CreateUser(id: "current-user-id");
        user.IsActive = true;
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);

        var command = new DeactivateUserCommand("current-user-id");
        var sut = CreateSut();

        // Act
        var result = await sut.DeactivateUserAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot deactivate your own account");
        user.IsActive.Should().BeTrue();
        _mockUserManager.Verify(x => x.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task DeactivateUserAsync_ShouldThrowNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        _mockUserManager.Setup(x => x.Users).Returns(Array.Empty<ApplicationUser>().AsQueryable().BuildMockDbSet().Object);

        var command = new DeactivateUserCommand("missing");
        var sut = CreateSut();

        // Act
        var act = () => sut.DeactivateUserAsync(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region AssignRolesAsync

    [Fact]
    public async Task AssignRolesAsync_ShouldAddAndRemoveRoles()
    {
        // Arrange
        var user = CreateUser();
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(["Basic"]);
        _mockUserManager.Setup(x => x.RemoveFromRolesAsync(user, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.AddToRolesAsync(user, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Success);

        var command = new AssignUserRolesCommand("user-1", ["Admin"]);
        var sut = CreateSut();

        // Act
        var result = await sut.AssignRolesAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockUserManager.Verify(x => x.RemoveFromRolesAsync(user, It.Is<IEnumerable<string>>(r => r.Contains("Basic"))), Times.Once);
        _mockUserManager.Verify(x => x.AddToRolesAsync(user, It.Is<IEnumerable<string>>(r => r.Contains("Admin"))), Times.Once);
        var changed = RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserRolesChangedEvent>().Subject;
        changed.Added.Should().Equal(AdminRoleId);
        changed.Removed.Should().Equal(BasicRoleId);
        changed.Roles.Should().Equal(AdminRoleId);
    }

    [Fact]
    public async Task AssignRolesAsync_ShouldRaiseNothing_WhenRolesAreUnchanged()
    {
        // Arrange
        var user = CreateUser();
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(["Basic"]);
        _mockUserManager.Setup(x => x.RemoveFromRolesAsync(user, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.AddToRolesAsync(user, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        var result = await sut.AssignRolesAsync(new AssignUserRolesCommand("user-1", ["Basic"]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        RaisedEvents(user).Should().BeEmpty();
    }

    [Fact]
    public async Task AssignRolesAsync_ShouldRollBackAndRecordNothing_WhenAddingRolesFails()
    {
        // Arrange
        var user = CreateUser();
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(["Basic"]);
        _mockUserManager.Setup(x => x.RemoveFromRolesAsync(user, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.AddToRolesAsync(user, It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Role assignment failed." }));

        var sut = CreateSut();

        // Act
        var act = () => sut.AssignRolesAsync(new AssignUserRolesCommand("user-1", ["Admin"]), TestContext.Current.CancellationToken);

        // Assert — the throw is what rolls the removals back with the transaction.
        await act.Should().ThrowAsync<InternalServerException>();
        _mockUserIdentityStore.Verify(s => s.ExecuteInTransaction(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
        RaisedEvents(user).Should().BeEmpty("the additions were not saved, so the change may not be recorded");
    }

    [Fact]
    public async Task AssignRolesAsync_ShouldThrowConflict_WhenRemovingLastAdmin()
    {
        // Arrange
        var user = CreateUser();
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(["Admin"]);
        _mockUserManager.Setup(x => x.GetUsersInRoleAsync(ApplicationRoles.Admin))
            .ReturnsAsync([user]);

        var command = new AssignUserRolesCommand("user-1", ["Basic"]);
        var sut = CreateSut();

        // Act
        var act = () => sut.AssignRolesAsync(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*holds every permission*");
    }

    [Fact]
    public async Task AssignRolesAsync_ShouldThrowNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        _mockUserManager.Setup(x => x.Users).Returns(Array.Empty<ApplicationUser>().AsQueryable().BuildMockDbSet().Object);

        var command = new AssignUserRolesCommand("missing", ["Basic"]);
        var sut = CreateSut();

        // Act
        var act = () => sut.AssignRolesAsync(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region ChangePasswordAsync

    [Fact]
    public async Task ChangePasswordAsync_ShouldSucceed_WhenLocalUserWithValidPassword()
    {
        // Arrange
        var user = CreateUser(loginProvider: LoginProviders.Wayd);
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.ChangePasswordAsync(user, "OldPass123!", "NewPass456!"))
            .ReturnsAsync(IdentityResult.Success);

        var command = new ChangePasswordCommand("OldPass123!", "NewPass456!");
        var sut = CreateSut();

        // Act
        var result = await sut.ChangePasswordAsync("user-1", command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockUserManager.Verify(x => x.ChangePasswordAsync(user, "OldPass123!", "NewPass456!"), Times.Once);
        RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserPasswordChangedEvent>()
            .Which.Actor.Should().Be(EventActor.User("current-user-id"));
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldClearMustChangePassword_WhenFlagIsSet()
    {
        // Arrange
        var user = CreateUser(loginProvider: LoginProviders.Wayd);
        user.MustChangePassword = true;
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        var clearedBeforeTheSave = false;
        _mockUserManager.Setup(x => x.ChangePasswordAsync(user, "OldPass123!", "NewPass456!"))
            .Callback(() => clearedBeforeTheSave = !user.MustChangePassword)
            .ReturnsAsync(IdentityResult.Success);

        var command = new ChangePasswordCommand("OldPass123!", "NewPass456!");
        var sut = CreateSut();

        // Act
        var result = await sut.ChangePasswordAsync("user-1", command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.MustChangePassword.Should().BeFalse();
        clearedBeforeTheSave.Should().BeTrue("the change's own save writes the flag");
        _mockUserManager.Verify(x => x.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_KeepsMustChangePassword_WhenTheChangeIsRefused()
    {
        // Arrange
        var user = CreateUser(loginProvider: LoginProviders.Wayd);
        user.MustChangePassword = true;
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.ChangePasswordAsync(user, "WrongPass1!", "NewPass456!"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Incorrect password." }));
        var sut = CreateSut();

        // Act
        var result = await sut.ChangePasswordAsync("user-1", new ChangePasswordCommand("WrongPass1!", "NewPass456!"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        user.MustChangePassword.Should().BeTrue();
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldReturnFailure_WhenUserIsNotLocal()
    {
        // Arrange
        var user = CreateUser(); // Default is MicrosoftEntraId
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);

        var command = new ChangePasswordCommand("OldPass123!", "NewPass456!");
        var sut = CreateSut();

        // Act
        var result = await sut.ChangePasswordAsync("user-1", command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("only available for local accounts");
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldReturnFailure_WhenIdentityFails()
    {
        // Arrange
        var user = CreateUser(loginProvider: LoginProviders.Wayd);
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.ChangePasswordAsync(user, "wrong", "NewPass456!"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Incorrect password." }));

        var command = new ChangePasswordCommand("wrong", "NewPass456!");
        var sut = CreateSut();

        // Act
        var result = await sut.ChangePasswordAsync("user-1", command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Incorrect password.");
        RaisedEvents(user).Should().BeEmpty("nothing was saved, so nothing may be recorded");
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldThrowNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        _mockUserManager.Setup(x => x.FindByIdAsync("missing")).ReturnsAsync((ApplicationUser?)null);

        var command = new ChangePasswordCommand("OldPass123!", "NewPass456!");
        var sut = CreateSut();

        // Act
        var act = () => sut.ChangePasswordAsync("missing", command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region ResetPasswordAsync

    [Fact]
    public async Task ResetPasswordAsync_ShouldSucceed_AndSetMustChangePassword()
    {
        // Arrange
        var user = CreateUser(loginProvider: LoginProviders.Wayd);
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-token");
        var flagSetBeforeTheSave = false;
        _mockUserManager.Setup(x => x.ResetPasswordAsync(user, "reset-token", "NewPass456!"))
            .Callback(() => flagSetBeforeTheSave = user.MustChangePassword)
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);

        var command = new ResetPasswordCommand("user-1", "NewPass456!");
        var sut = CreateSut();

        // Act
        var result = await sut.ResetPasswordAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        flagSetBeforeTheSave.Should().BeTrue("the reset's own save writes the flag");
        _mockUserManager.Verify(x => x.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        var reset = RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserPasswordResetEvent>().Subject;
        reset.ClearedLockout.Should().BeFalse();
        reset.Actor.Should().Be(EventActor.User("current-user-id"));
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldClearLockout_WhenUserIsLockedOut()
    {
        // Arrange
        var user = CreateUser(loginProvider: LoginProviders.Wayd);
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-token");
        user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(10);
        user.AccessFailedCount = 5;
        _mockUserManager.Setup(x => x.ResetPasswordAsync(user, "reset-token", "NewPass456!"))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(true);

        var command = new ResetPasswordCommand("user-1", "NewPass456!");
        var sut = CreateSut();

        // Act
        var result = await sut.ResetPasswordAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.LockoutEnd.Should().BeNull();
        user.AccessFailedCount.Should().Be(0);
        RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserPasswordResetEvent>()
            .Which.ClearedLockout.Should().BeTrue();
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldNotClearLockout_WhenUserIsNotLockedOut()
    {
        // Arrange
        var user = CreateUser(loginProvider: LoginProviders.Wayd);
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-token");
        user.AccessFailedCount = 2;
        _mockUserManager.Setup(x => x.ResetPasswordAsync(user, "reset-token", "NewPass456!"))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);

        var command = new ResetPasswordCommand("user-1", "NewPass456!");
        var sut = CreateSut();

        // Act
        var result = await sut.ResetPasswordAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.AccessFailedCount.Should().Be(2);
        RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserPasswordResetEvent>()
            .Which.ClearedLockout.Should().BeFalse();
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldReturnFailure_WhenUserIsNotLocal()
    {
        // Arrange
        var user = CreateUser(); // Default is MicrosoftEntraId
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);

        var command = new ResetPasswordCommand("user-1", "NewPass456!");
        var sut = CreateSut();

        // Act
        var result = await sut.ResetPasswordAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("only available for local accounts");
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldReturnFailure_WhenIdentityFails()
    {
        // Arrange
        var user = CreateUser(loginProvider: LoginProviders.Wayd);
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-token");
        _mockUserManager.Setup(x => x.ResetPasswordAsync(user, "reset-token", "weak"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too short." }));

        var command = new ResetPasswordCommand("user-1", "weak");
        var sut = CreateSut();

        // Act
        var result = await sut.ResetPasswordAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Password too short.");
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldThrowNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        _mockUserManager.Setup(x => x.FindByIdAsync("missing")).ReturnsAsync((ApplicationUser?)null);

        var command = new ResetPasswordCommand("missing", "NewPass456!");
        var sut = CreateSut();

        // Act
        var act = () => sut.ResetPasswordAsync(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region StageEntraSignInTenant

    private ApplicationUser ArrangeUserForSignInTenant(
        string loginProvider = LoginProviders.MicrosoftEntraId,
        bool hasActiveIdentity = false)
    {
        var user = CreateUser(loginProvider: loginProvider);
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        _mockUserIdentityStore
            .Setup(s => s.ExistsActive(user.Id, LoginProviders.MicrosoftEntraId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasActiveIdentity);
        return user;
    }

    [Fact]
    public async Task StageEntraSignInTenant_ShouldStageTheTenant_WhenUserHasNoActiveIdentity()
    {
        // Arrange — e.g. an admin-created user whose staged link was cancelled.
        const string tenantId = "7d1b4a52-0000-4000-8000-000000000001";
        ArrangeEntraProvider(tenantId);
        var user = ArrangeUserForSignInTenant();
        var sut = CreateSut();

        // Act
        var result = await sut.StageEntraSignInTenant(
            new StageEntraSignInTenantCommand(user.Id, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.PendingMigrationTenantId.Should().Be(tenantId);
        user.PendingMigrationStagedAt.Should().Be(_dateTimeProvider.Now);
        var staged = RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserTenantMigrationStagedEvent>().Subject;
        staged.TargetTenantId.Should().Be(tenantId);
        staged.PreviousTargetTenantId.Should().BeNull();
    }

    [Fact]
    public async Task StageEntraSignInTenant_ShouldReplaceTheStagedTenant_WhenAnotherIsChosen()
    {
        // Arrange — the admin picked the wrong tenant at creation.
        const string tenantA = "7d1b4a52-0000-4000-8000-00000000000a";
        const string tenantB = "7d1b4a52-0000-4000-8000-00000000000b";
        ArrangeEntraProvider(tenantA, tenantB);
        var user = ArrangeUserForSignInTenant();
        user.PendingMigrationTenantId = tenantA;
        user.PendingMigrationStagedAt = _dateTimeProvider.Now;
        var sut = CreateSut();

        // Act
        var result = await sut.StageEntraSignInTenant(
            new StageEntraSignInTenantCommand(user.Id, tenantB), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.PendingMigrationTenantId.Should().Be(tenantB);
        RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserTenantMigrationStagedEvent>()
            .Which.PreviousTargetTenantId.Should().Be(tenantA);
    }

    [Fact]
    public async Task StageEntraSignInTenant_ShouldRefuseAndLeaveTheUserAlone_WhenTheyHaveAnActiveIdentity()
    {
        // Arrange — someone signs in with this account today; moving it is a tenant migration.
        ArrangeEntraProvider("7d1b4a52-0000-4000-8000-00000000000a", "7d1b4a52-0000-4000-8000-00000000000b");
        var user = ArrangeUserForSignInTenant(hasActiveIdentity: true);
        var sut = CreateSut();

        // Act
        var result = await sut.StageEntraSignInTenant(
            new StageEntraSignInTenantCommand(user.Id, "7d1b4a52-0000-4000-8000-00000000000b"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("tenant migration");
        user.PendingMigrationTenantId.Should().BeNull();
        _mockUserManager.Verify(x => x.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task StageEntraSignInTenant_ShouldRefuse_WhenUserIsNotAnEntraUser()
    {
        // Arrange
        ArrangeEntraProvider("7d1b4a52-0000-4000-8000-000000000001");
        var user = ArrangeUserForSignInTenant(loginProvider: LoginProviders.Wayd);
        var sut = CreateSut();

        // Act
        var result = await sut.StageEntraSignInTenant(
            new StageEntraSignInTenantCommand(user.Id, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        user.PendingMigrationTenantId.Should().BeNull();
        _mockUserManager.Verify(x => x.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task StageEntraSignInTenant_ShouldRefuse_WhenTheTenantIsNotAllowed()
    {
        // Arrange
        ArrangeEntraProvider("7d1b4a52-0000-4000-8000-00000000000a");
        var user = ArrangeUserForSignInTenant();
        var sut = CreateSut();

        // Act
        var result = await sut.StageEntraSignInTenant(
            new StageEntraSignInTenantCommand(user.Id, "7d1b4a52-0000-4000-8000-0000000000ff"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not allowed");
        user.PendingMigrationTenantId.Should().BeNull();
    }

    [Fact]
    public async Task StageEntraSignInTenant_ShouldThrowNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        _mockUserManager.Setup(x => x.Users).Returns(Array.Empty<ApplicationUser>().AsQueryable().BuildMockDbSet().Object);
        var sut = CreateSut();

        // Act
        var act = () => sut.StageEntraSignInTenant(
            new StageEntraSignInTenantCommand("missing", null), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region CancelTenantMigration

    [Fact]
    public async Task CancelTenantMigration_ShouldClearPendingTenant_WhenStaged()
    {
        // Arrange
        var user = CreateUser();
        var stagedTenant = Guid.NewGuid().ToString();
        user.PendingMigrationTenantId = stagedTenant;
        user.PendingMigrationStagedAt = _dateTimeProvider.Now;

        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        var result = await sut.CancelTenantMigration(user.Id, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.PendingMigrationTenantId.Should().BeNull();
        // StagedAt moves together with the tenant flag — both clear on cancel.
        user.PendingMigrationStagedAt.Should().BeNull();
        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
        RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserTenantMigrationCanceledEvent>()
            .Which.TargetTenantId.Should().Be(stagedTenant);
    }

    [Fact]
    public async Task CancelTenantMigration_ShouldBeIdempotent_WhenNothingStaged()
    {
        // Arrange
        var user = CreateUser();
        user.PendingMigrationTenantId = null;

        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);

        var sut = CreateSut();

        // Act
        var result = await sut.CancelTenantMigration(user.Id, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockUserManager.Verify(x => x.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        RaisedEvents(user).Should().BeEmpty();
    }

    [Fact]
    public async Task CancelTenantMigration_ShouldThrowNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        _mockUserManager.Setup(x => x.Users).Returns(Array.Empty<ApplicationUser>().AsQueryable().BuildMockDbSet().Object);

        var sut = CreateSut();

        // Act
        var act = () => sut.CancelTenantMigration("missing", TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region GetOrCreateFromPrincipalAsync — pending tenant migration rebind

    private static ClaimsPrincipal CreateEntraPrincipal(string objectId, string tenantId, string? upn = null)
    {
        var claims = new List<Claim>
        {
            new(Microsoft.Identity.Web.ClaimConstants.ObjectId, objectId),
            new(Microsoft.Identity.Web.ClaimConstants.TenantId, tenantId),
        };
        if (upn is not null)
        {
            claims.Add(new Claim(ClaimTypes.Upn, upn));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public async Task GetOrCreateFromPrincipalAsync_ShouldRebindIdentity_WhenMigrationStagedAndUpnMatches()
    {
        var newTenantId = Guid.NewGuid().ToString();
        var newObjectId = Guid.NewGuid().ToString();
        var upn = "alice@newtenant.com";

        var user = CreateUser(id: "user-rebind", userName: upn, loginProvider: LoginProviders.MicrosoftEntraId);
        user.NormalizedUserName = upn.ToUpperInvariant();
        user.NormalizedEmail = "ALICE@NEWTENANT.COM";
        user.PendingMigrationTenantId = newTenantId;
        user.PendingMigrationStagedAt = _dateTimeProvider.Now;

        // No active identity for the new (tid, oid). No NULL-tenant backfill row either.
        _mockUserIdentityStore.Setup(s => s.FindActive(LoginProviders.MicrosoftEntraId, newTenantId, newObjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserIdentity?)null);
        _mockUserIdentityStore.Setup(s => s.FindActiveByNullTenant(LoginProviders.MicrosoftEntraId, newObjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserIdentity>());

        // Two queryable hits on _userManager.Users: AnyAsync (isFirstUser) then FirstOrDefault for the migration lookup.
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(["Basic"]);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        await _refreshTokenStore.Issue(user.Id, SessionContext.None, TestContext.Current.CancellationToken);

        var sut = CreateSut();

        var (resolvedId, _) = await sut.GetOrCreateFromPrincipalAsync(
            CreateEntraPrincipal(newObjectId, newTenantId, upn));

        resolvedId.Should().Be(user.Id);
        user.PendingMigrationTenantId.Should().BeNull();
        _refreshTokenStore.ActiveSessionCount(user.Id).Should().Be(0, "a session opened through the old sign-in must not outlive it");
        _refreshTokenStore.RevokeReasons(user.Id).Should().Equal(UserRefreshTokenRevokeReasons.SignInChanged);
        // StagedAt moves together with the tenant flag — both clear on a completed rebind.
        user.PendingMigrationStagedAt.Should().BeNull();
        var completed = RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserTenantMigrationCompletedEvent>().Subject;
        completed.TenantId.Should().Be(newTenantId);
        completed.Actor.Should().Be(EventActor.System);

        _mockUserIdentityStore.Verify(s => s.DeactivateAllActive(
            user.Id,
            It.IsAny<NodaTime.Instant>(),
            UserIdentityUnlinkReasons.TenantMigration,
            It.IsAny<CancellationToken>()),
            Times.Once);

        _mockUserIdentityStore.Verify(s => s.Add(
            It.Is<UserIdentity>(ui =>
                ui.UserId == user.Id &&
                ui.Provider == LoginProviders.MicrosoftEntraId &&
                ui.ProviderTenantId == newTenantId &&
                ui.ProviderSubject == newObjectId &&
                ui.IsActive),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TryApplyPendingTenantMigration_ShouldNotRebind_WhenPendingTenantDoesNotMatchToken()
    {
        var stagedTenant = Guid.NewGuid().ToString();
        var unrelatedTenant = Guid.NewGuid().ToString();
        var newObjectId = Guid.NewGuid().ToString();
        var upn = "alice@example.com";

        var user = CreateUser(id: "user-rebind", userName: upn, loginProvider: LoginProviders.MicrosoftEntraId);
        user.NormalizedUserName = upn.ToUpperInvariant();
        user.NormalizedEmail = "ALICE@EXAMPLE.COM";
        // Staged for a different tenant than the token's tid.
        user.PendingMigrationTenantId = stagedTenant;
        user.PendingMigrationStagedAt = _dateTimeProvider.Now;

        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);

        var sut = CreateSut();

        // Exercise the rebind decision directly so the test doesn't depend on the
        // surrounding GetOrCreateFromPrincipalAsync (which calls into Graph on the
        // create path). Returning null here is what causes the caller to fall through
        // to CreateOrUpdateFromPrincipalAsync.
        var result = await sut.TryApplyPendingTenantMigration(unrelatedTenant, newObjectId, upn);

        result.Should().BeNull();
        user.PendingMigrationTenantId.Should().Be(stagedTenant);
        _mockUserIdentityStore.Verify(s => s.DeactivateAllActive(
            It.IsAny<string>(), It.IsAny<NodaTime.Instant>(), UserIdentityUnlinkReasons.TenantMigration, It.IsAny<CancellationToken>()),
            Times.Never);
        _mockUserIdentityStore.Verify(s => s.Add(It.IsAny<UserIdentity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryApplyPendingTenantMigration_ShouldNotRebind_WhenDifferentUserHasMatchingEmailButNoFlag()
    {
        var newTenantId = Guid.NewGuid().ToString();
        var newObjectId = Guid.NewGuid().ToString();
        var upn = "alice@example.com";

        // A user with the same email but no pending migration must not be rebound.
        var unrelatedUser = CreateUser(id: "user-unrelated", userName: upn, loginProvider: LoginProviders.MicrosoftEntraId);
        unrelatedUser.NormalizedUserName = upn.ToUpperInvariant();
        unrelatedUser.NormalizedEmail = "ALICE@EXAMPLE.COM";
        unrelatedUser.PendingMigrationTenantId = null;

        _mockUserManager.Setup(x => x.Users).Returns(new[] { unrelatedUser }.AsQueryable().BuildMockDbSet().Object);

        var sut = CreateSut();

        var result = await sut.TryApplyPendingTenantMigration(newTenantId, newObjectId, upn);

        result.Should().BeNull();
        _mockUserIdentityStore.Verify(s => s.DeactivateAllActive(
            It.IsAny<string>(), It.IsAny<NodaTime.Instant>(), UserIdentityUnlinkReasons.TenantMigration, It.IsAny<CancellationToken>()),
            Times.Never);
        _mockUserIdentityStore.Verify(s => s.Add(It.IsAny<UserIdentity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryApplyPendingTenantMigration_ShouldReturnNull_WhenUpnIsMissing()
    {
        // Defensive: a token without a UPN claim should not match any user, even if
        // one happens to have a pending migration for the token's tenant.
        var newTenantId = Guid.NewGuid().ToString();
        var newObjectId = Guid.NewGuid().ToString();

        var user = CreateUser(id: "user-with-pending", loginProvider: LoginProviders.MicrosoftEntraId);
        user.PendingMigrationTenantId = newTenantId;
        user.PendingMigrationStagedAt = _dateTimeProvider.Now;

        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);

        var sut = CreateSut();

        var result = await sut.TryApplyPendingTenantMigration(newTenantId, newObjectId, upn: null);

        result.Should().BeNull();
        user.PendingMigrationTenantId.Should().Be(newTenantId);
        _mockUserIdentityStore.Verify(s => s.DeactivateAllActive(
            It.IsAny<string>(), It.IsAny<NodaTime.Instant>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryApplyPendingTenantMigration_ShouldThrowAndRollBackTransaction_WhenClearingFlagFails()
    {
        // If UserManager.UpdateAsync fails inside the rebind transaction (e.g.,
        // concurrency token mismatch), the deactivate+insert must NOT commit — otherwise
        // the user has a fresh active identity row but PendingMigrationTenantId is still
        // set, which would re-trigger the rebind path on next login and explode against
        // the unique-active-row index.
        var newTenantId = Guid.NewGuid().ToString();
        var newObjectId = Guid.NewGuid().ToString();
        var upn = "alice@newtenant.com";

        var user = CreateUser(id: "user-rebind-fail", userName: upn, loginProvider: LoginProviders.MicrosoftEntraId);
        user.NormalizedUserName = upn.ToUpperInvariant();
        user.NormalizedEmail = "ALICE@NEWTENANT.COM";
        user.PendingMigrationTenantId = newTenantId;
        user.PendingMigrationStagedAt = _dateTimeProvider.Now;

        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Concurrency failure." }));

        // Track what happened inside the transaction lambda. We can't inspect EF's
        // Database.BeginTransactionAsync rollback directly with the mocked store, but
        // we can verify (a) the action threw — which is what triggers rollback in the
        // real ExecuteInTransaction — and (b) Add was called before the failing
        // UpdateAsync, proving the rebind logic ran to the failing step rather than
        // bailing out earlier.
        Exception? captured = null;
        _mockUserIdentityStore
            .Setup(s => s.ExecuteInTransaction(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>(async (action, ct) =>
            {
                try
                {
                    await action(ct);
                }
                catch (Exception ex)
                {
                    captured = ex;
                    throw;
                }
            });

        var sut = CreateSut();

        var act = () => sut.TryApplyPendingTenantMigration(newTenantId, newObjectId, upn);

        await act.Should().ThrowAsync<InternalServerException>()
            .WithMessage("Sign-in could not complete the tenant migration. Contact an administrator.");
        RaisedEvents(user).Should().BeEmpty("nothing was saved, so nothing may be recorded");

        captured.Should().NotBeNull("the transaction lambda must throw so ExecuteInTransaction rolls back");
        _mockUserIdentityStore.Verify(s => s.Add(It.IsAny<UserIdentity>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUserIdentityStore.Verify(s => s.DeactivateAllActive(
            user.Id, It.IsAny<NodaTime.Instant>(), UserIdentityUnlinkReasons.TenantMigration, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetOrCreateFromPrincipalAsync — registration policy

    // A principal carrying every claim the create path reads: object/tenant id for
    // identity resolution, UPN as email, name for the username, and given/surname
    // which the entity guards against being blank.
    private static ClaimsPrincipal CreateNewUserPrincipal(
        string objectId, string tenantId, string upn, string displayName = "New User")
    {
        var claims = new List<Claim>
        {
            new(Microsoft.Identity.Web.ClaimConstants.ObjectId, objectId),
            new(Microsoft.Identity.Web.ClaimConstants.TenantId, tenantId),
            new(ClaimTypes.Upn, upn),
            new("name", displayName),
            new(ClaimTypes.GivenName, "New"),
            new(ClaimTypes.Surname, "User"),
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static OidcProvider CreateEntraProviderWithPolicy(
        bool allowAutoRegistration = true,
        bool requireEmployeeRecord = true,
        string? defaultRoleId = null)
    {
        // Name must be the well-known Entra key — the registry lookup in
        // GetOrCreateFromPrincipalAsync resolves the policy by it.
        var faker = new OidcProviderFaker()
            .WithName(LoginProviders.MicrosoftEntraId)
            .AsMicrosoftEntraId("tenant-1");

        faker = allowAutoRegistration
            ? faker.WithAutoRegistration(requireEmployeeRecord, defaultRoleId)
            : faker.WithoutAutoRegistration();

        return faker.Generate();
    }

    // Arranges the registry to return the given provider for the Entra key, an
    // empty (non-first-user) user set, no existing user/identity match, and a
    // successful create. The employee lookup defaults to "no match" unless overridden.
    private void ArrangeNewUserCreatePath(OidcProvider provider, Guid? employeeId = null)
    {
        _mockOidcProviderRegistry
            .Setup(r => r.GetByName(LoginProviders.MicrosoftEntraId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(provider);

        // A single existing user makes isFirstUser false without matching the new
        // principal (different username/email), so we exercise real policy rather
        // than the first-user bootstrap exemption.
        var existing = CreateUser(id: "existing", userName: "someone-else", loginProvider: LoginProviders.MicrosoftEntraId);
        existing.NormalizedUserName = "SOMEONE-ELSE";
        existing.NormalizedEmail = "SOMEONE-ELSE@EXAMPLE.COM";
        _mockUserManager.Setup(x => x.Users).Returns(new[] { existing }.AsQueryable().BuildMockDbSet().Object);

        _mockUserIdentityStore.Setup(s => s.FindActive(LoginProviders.MicrosoftEntraId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserIdentity?)null);
        _mockUserIdentityStore.Setup(s => s.FindActiveByNullTenant(LoginProviders.MicrosoftEntraId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserIdentity>());
        _mockUserIdentityStore.Setup(s => s.ExistsActive(It.IsAny<string>(), LoginProviders.MicrosoftEntraId))
            .ReturnsAsync(false);

        _mockUserManager.Setup(x => x.FindByNameAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        _mockUserManager.Setup(x => x.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.GetRolesAsync(It.IsAny<ApplicationUser>())).ReturnsAsync([]);
        _mockUserManager.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);

        // The registration policy's default role resolves to a real role by default.
        // Tests exercising a deleted/missing role override this with a more specific setup.
        _mockRoleManager.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => new ApplicationRole("Contributor") { Id = id });

        _mockDispatcher.Setup(s => s.Send(It.IsAny<Wayd.Common.Application.Employees.Queries.GetEmployeeByEmailQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(employeeId);
    }

    [Fact]
    public async Task GetOrCreateFromPrincipalAsync_ShouldDenyRegistration_WhenAutoRegistrationDisabled()
    {
        // Arrange
        var provider = CreateEntraProviderWithPolicy(allowAutoRegistration: false);
        ArrangeNewUserCreatePath(provider, employeeId: Guid.NewGuid());
        var sut = CreateSut();

        // Act
        var act = () => sut.GetOrCreateFromPrincipalAsync(
            CreateNewUserPrincipal("oid-1", "tenant-1", "newuser@acme.example"));

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*disabled for this identity provider*");
        _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task GetOrCreateFromPrincipalAsync_ShouldDenyRegistration_WhenEmployeeRequiredButNoMatch()
    {
        // Arrange
        var provider = CreateEntraProviderWithPolicy(allowAutoRegistration: true, requireEmployeeRecord: true);
        ArrangeNewUserCreatePath(provider, employeeId: null);
        var sut = CreateSut();

        // Act
        var act = () => sut.GetOrCreateFromPrincipalAsync(
            CreateNewUserPrincipal("oid-2", "tenant-1", "newuser@acme.example"));

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*restricted to users with an employee record*");
        _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task GetOrCreateFromPrincipalAsync_ShouldCreateUser_WhenEmployeeRequiredAndMatches()
    {
        // Arrange
        const string roleId = "role-guid-default";
        const string roleName = "Contributor";
        var employeeId = Guid.NewGuid();
        var provider = CreateEntraProviderWithPolicy(allowAutoRegistration: true, requireEmployeeRecord: true, defaultRoleId: roleId);
        ArrangeNewUserCreatePath(provider, employeeId: employeeId);
        _mockRoleManager.Setup(x => x.FindByIdAsync(roleId))
            .ReturnsAsync(new ApplicationRole(roleName) { Id = roleId });
        var sut = CreateSut();

        // Act
        var (resolvedId, resolvedEmployeeId) = await sut.GetOrCreateFromPrincipalAsync(
            CreateNewUserPrincipal("oid-3", "tenant-1", "newuser@acme.example"));

        // Assert
        resolvedId.Should().NotBeNullOrWhiteSpace();
        resolvedEmployeeId.Should().Be(employeeId.ToString());
        _mockUserManager.Verify(x => x.CreateAsync(It.Is<ApplicationUser>(u => u.EmployeeId == employeeId)), Times.Once);
        _mockUserManager.Verify(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), roleName), Times.Once);
    }

    [Fact]
    public async Task GetOrCreateFromPrincipalAsync_ShouldCreateUserUnlinked_WhenMatchedEmployeeIsAlreadyLinked()
    {
        // Arrange — Employee.Email is connector-owned and mutable, so an employee renamed onto a new
        // address can resolve by email for a second user. One user per employee is a DB invariant, so
        // the new user is created unlinked rather than failing the insert and blocking their sign-in.
        var employeeId = Guid.NewGuid();
        var provider = CreateEntraProviderWithPolicy(allowAutoRegistration: true, requireEmployeeRecord: false);
        ArrangeNewUserCreatePath(provider, employeeId: employeeId);

        var alreadyLinked = CreateUser(id: "existing", userName: "someone-else", loginProvider: LoginProviders.MicrosoftEntraId);
        alreadyLinked.NormalizedUserName = "SOMEONE-ELSE";
        alreadyLinked.NormalizedEmail = "SOMEONE-ELSE@EXAMPLE.COM";
        alreadyLinked.EmployeeId = employeeId;
        _mockUserManager.Setup(x => x.Users).Returns(new[] { alreadyLinked }.AsQueryable().BuildMockDbSet().Object);

        var sut = CreateSut();

        // Act
        var (resolvedId, resolvedEmployeeId) = await sut.GetOrCreateFromPrincipalAsync(
            CreateNewUserPrincipal("oid-9", "tenant-1", "renamed@acme.example"));

        // Assert
        resolvedId.Should().NotBeNullOrWhiteSpace();
        resolvedEmployeeId.Should().BeNull();
        _mockUserManager.Verify(x => x.CreateAsync(It.Is<ApplicationUser>(u => u.EmployeeId == null)), Times.Once);
    }

    [Fact]
    public async Task GetOrCreateFromPrincipalAsync_ShouldDenyRegistration_WhenMatchedEmployeeIsAlreadyLinkedAndEmployeeRequired()
    {
        // Arrange — with the employee gate on, an already-claimed employee is not a usable match, so
        // registration is denied with the standard message instead of failing the unique index.
        var employeeId = Guid.NewGuid();
        var provider = CreateEntraProviderWithPolicy(allowAutoRegistration: true, requireEmployeeRecord: true, defaultRoleId: "role-guid-default");
        ArrangeNewUserCreatePath(provider, employeeId: employeeId);

        var alreadyLinked = CreateUser(id: "existing", userName: "someone-else", loginProvider: LoginProviders.MicrosoftEntraId);
        alreadyLinked.NormalizedUserName = "SOMEONE-ELSE";
        alreadyLinked.NormalizedEmail = "SOMEONE-ELSE@EXAMPLE.COM";
        alreadyLinked.EmployeeId = employeeId;
        _mockUserManager.Setup(x => x.Users).Returns(new[] { alreadyLinked }.AsQueryable().BuildMockDbSet().Object);

        var sut = CreateSut();

        // Act
        var act = () => sut.GetOrCreateFromPrincipalAsync(
            CreateNewUserPrincipal("oid-10", "tenant-1", "renamed@acme.example"));

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*restricted to users with an employee record*");
        _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task GetOrCreateFromPrincipalAsync_ShouldCreateUser_WhenEmployeeNotRequiredAndNoEmployeeMatch()
    {
        // Arrange — the loosened posture: anyone who authenticates gets an account.
        var provider = CreateEntraProviderWithPolicy(allowAutoRegistration: true, requireEmployeeRecord: false);
        ArrangeNewUserCreatePath(provider, employeeId: null);
        var sut = CreateSut();

        // Act
        var (resolvedId, _) = await sut.GetOrCreateFromPrincipalAsync(
            CreateNewUserPrincipal("oid-4", "tenant-1", "outsider@acme.example"));

        // Assert
        resolvedId.Should().NotBeNullOrWhiteSpace();
        _mockUserManager.Verify(x => x.CreateAsync(It.Is<ApplicationUser>(u => u.EmployeeId == null)), Times.Once);
    }

    [Fact]
    public async Task GetOrCreateFromPrincipalAsync_ShouldAssignConfiguredDefaultRole_WhenPolicyNamesRole()
    {
        // Arrange
        const string roleId = "role-guid-pm";
        const string roleName = "ProjectManager";
        var provider = CreateEntraProviderWithPolicy(requireEmployeeRecord: false, defaultRoleId: roleId);
        ArrangeNewUserCreatePath(provider, employeeId: null);
        _mockRoleManager.Setup(x => x.FindByIdAsync(roleId))
            .ReturnsAsync(new ApplicationRole(roleName) { Id = roleId });
        var sut = CreateSut();

        // Act
        await sut.GetOrCreateFromPrincipalAsync(CreateNewUserPrincipal("oid-5", "tenant-1", "pm@acme.example"));

        // Assert
        _mockUserManager.Verify(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), roleName), Times.Once);
        // Only the configured role is assigned — no implicit additional role.
        _mockUserManager.Verify(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.Is<string>(r => r != roleName)), Times.Never);
    }

    [Fact]
    public async Task GetOrCreateFromPrincipalAsync_ShouldThrow_WhenConfiguredDefaultRoleDeleted()
    {
        // Arrange — there is no fallback role. A stale default-role reference (which
        // the FK + role-delete guard normally prevent) is an invariant violation that
        // must fail loudly rather than silently provisioning an unintended role.
        const string roleId = "role-guid-deleted";
        var provider = CreateEntraProviderWithPolicy(requireEmployeeRecord: false, defaultRoleId: roleId);
        ArrangeNewUserCreatePath(provider, employeeId: null);
        _mockRoleManager.Setup(x => x.FindByIdAsync(roleId)).ReturnsAsync((ApplicationRole?)null);
        var sut = CreateSut();

        // Act
        var act = () => sut.GetOrCreateFromPrincipalAsync(
            CreateNewUserPrincipal("oid-6", "tenant-1", "user@acme.example"));

        // Assert
        await act.Should().ThrowAsync<InternalServerException>()
            .WithMessage("*configured registration role no longer exists*");
        _mockUserManager.Verify(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region Staged link lifetime and the roles a sign-in grants

    [Fact]
    public async Task TryApplyPendingTenantMigration_ShouldNotRebind_WhenTheStagedLinkHasExpired()
    {
        // Arrange
        const string tenantId = "tenant-new";
        const string upn = "alice@example.com";
        var user = CreateUser(id: "user-expired");
        user.NormalizedUserName = upn.ToUpperInvariant();
        user.PendingMigrationTenantId = tenantId;
        user.PendingMigrationStagedAt = _dateTimeProvider.Now - StagedLinks.Lifetime - NodaTime.Duration.FromMinutes(1);
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        var sut = CreateSut();

        // Act
        var result = await sut.TryApplyPendingTenantMigration(tenantId, "oid-1", upn);

        // Assert
        result.Should().BeNull();
        user.PendingMigrationTenantId.Should().Be(tenantId);
        _mockUserIdentityStore.Verify(s => s.Add(It.IsAny<UserIdentity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryApplyPendingProviderMigration_ShouldNotRebind_WhenTheStagedLinkHasExpired()
    {
        // Arrange
        const string email = "alice@example.com";
        var user = CreateUser(id: "user-expired");
        user.NormalizedEmail = email.ToUpperInvariant();
        user.PendingMigrationProviderId = "Acme-Okta";
        user.PendingProviderMigrationStagedAt = _dateTimeProvider.Now - StagedLinks.Lifetime - NodaTime.Duration.FromMinutes(1);
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        var sut = CreateSut();

        // Act
        var result = await sut.TryApplyPendingProviderMigration("Acme-Okta", null, "okta-sub", email);

        // Assert
        result.Should().BeNull();
        _mockUserIdentityStore.Verify(s => s.Add(It.IsAny<UserIdentity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryApplyPendingProviderMigration_ShouldNotRebind_WhenStagedBeforeItsTimeWasRecorded()
    {
        // Arrange
        const string email = "alice@example.com";
        var user = CreateUser(id: "user-legacy");
        user.NormalizedEmail = email.ToUpperInvariant();
        user.PendingMigrationProviderId = "Acme-Okta";
        user.PendingProviderMigrationStagedAt = null;
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        var sut = CreateSut();

        // Act
        var result = await sut.TryApplyPendingProviderMigration("Acme-Okta", null, "okta-sub", email);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public async Task ResolveFromGenericOidcPrincipalAsync_ShouldNotApplyAStagedMigration_UnlessTheEmailIsVerified(string? emailVerified)
    {
        // Arrange
        const string email = "alice@example.com";
        var user = CreateUser(id: "user-staged", loginProvider: LoginProviders.Wayd);
        user.NormalizedEmail = email.ToUpperInvariant();
        user.PendingMigrationProviderId = "Acme-Okta";
        user.PendingProviderMigrationStagedAt = _dateTimeProvider.Now;
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserIdentityStore.Setup(s => s.FindActive("Acme-Okta", null, "okta-sub", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserIdentity?)null);
        var sut = CreateSut();

        // Act
        var result = await sut.ResolveFromGenericOidcPrincipalAsync(
            "Acme-Okta", CreateGenericPrincipal("okta-sub", email, emailVerified), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
        user.PendingMigrationProviderId.Should().Be("Acme-Okta");
    }

    [Fact]
    public async Task ResolveFromGenericOidcPrincipalAsync_ShouldApplyAStagedMigration_WhenTheEmailIsVerified()
    {
        // Arrange
        const string email = "alice@example.com";
        var user = CreateUser(id: "user-staged", loginProvider: LoginProviders.MicrosoftEntraId);
        user.NormalizedEmail = email.ToUpperInvariant();
        user.PendingMigrationProviderId = "Acme-Okta";
        user.PendingProviderMigrationStagedAt = _dateTimeProvider.Now;
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        _mockUserIdentityStore.Setup(s => s.FindActive("Acme-Okta", null, "okta-sub", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserIdentity?)null);
        var sut = CreateSut();

        // Act
        var result = await sut.ResolveFromGenericOidcPrincipalAsync(
            "Acme-Okta", CreateGenericPrincipal("okta-sub", email, "true"), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Id.Should().Be(user.Id);
        user.LoginProvider.Should().Be("Acme-Okta");
    }

    [Fact]
    public async Task GetOrCreateFromPrincipalAsync_ShouldNotMatchAStagedTenantLink_OnTheEmailClaimAlone()
    {
        // Arrange
        const string tenantId = "tenant-new";
        const string email = "alice@example.com";
        var staged = CreateUser(id: "user-staged");
        staged.NormalizedEmail = email.ToUpperInvariant();
        staged.PendingMigrationTenantId = tenantId;
        staged.PendingMigrationStagedAt = _dateTimeProvider.Now;
        ArrangeNewUserCreatePath(CreateEntraProviderWithPolicy(requireEmployeeRecord: false, defaultRoleId: "role-guid-default"));
        _mockUserManager.Setup(x => x.Users).Returns(new[] { staged }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.FindByEmailAsync(email)).ReturnsAsync(staged);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>
        {
            new(Microsoft.Identity.Web.ClaimConstants.ObjectId, "oid-email-only"),
            new(Microsoft.Identity.Web.ClaimConstants.TenantId, tenantId),
            new("email", email),
            new("name", "Alice"),
            new(ClaimTypes.GivenName, "Alice"),
            new(ClaimTypes.Surname, "Example"),
        }, "test"));
        var sut = CreateSut();

        // Act
        var act = () => sut.GetOrCreateFromPrincipalAsync(principal);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        staged.PendingMigrationTenantId.Should().Be(tenantId);
        _mockUserIdentityStore.Verify(s => s.Add(It.IsAny<UserIdentity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetOrCreateFromPrincipalAsync_ShouldGrantNoRole_WhenAnExistingUserWithNoRolesSignsIn()
    {
        // Arrange
        const string tenantId = "tenant-1";
        const string objectId = "oid-existing";
        var user = CreateUser(id: "user-roleless");
        ArrangeNewUserCreatePath(CreateEntraProviderWithPolicy(requireEmployeeRecord: false, defaultRoleId: "role-guid-default"));
        _mockUserIdentityStore.Setup(s => s.FindActive(LoginProviders.MicrosoftEntraId, tenantId, objectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserIdentity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                User = user,
                Provider = LoginProviders.MicrosoftEntraId,
                ProviderTenantId = tenantId,
                ProviderSubject = objectId,
                IsActive = true,
            });
        var sut = CreateSut();

        // Act
        var (resolvedId, _) = await sut.GetOrCreateFromPrincipalAsync(CreateEntraPrincipal(objectId, tenantId));

        // Assert
        resolvedId.Should().Be(user.Id);
        _mockUserManager.Verify(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetOrCreateFromPrincipalAsync_ShouldRefuseRegistrationAndCreateNoUser_WhenTheDefaultRoleIsPrivileged()
    {
        // Arrange
        const string roleId = "role-guid-privileged";
        var role = new ApplicationRole("Helpdesk") { Id = roleId };
        ArrangeNewUserCreatePath(CreateEntraProviderWithPolicy(requireEmployeeRecord: false, defaultRoleId: roleId));
        _mockRoleManager.Setup(x => x.FindByIdAsync(roleId)).ReturnsAsync(role);
        _mockRoleManager.Setup(x => x.GetClaimsAsync(role)).ReturnsAsync(
        [
            new Claim(ApplicationClaims.Permission, ApplicationPermission.NameFor(ApplicationAction.Manage, ApplicationResource.PrivilegedUsers)),
        ]);
        var sut = CreateSut();

        // Act
        var act = () => sut.GetOrCreateFromPrincipalAsync(CreateNewUserPrincipal("oid-p", "tenant-1", "user@acme.example"));

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task GetOrCreateFromPrincipalAsync_ShouldThrowAndWriteNoIdentity_WhenTheFirstUserCannotBeMadeAdmin()
    {
        // Arrange
        ArrangeNewUserCreatePath(CreateEntraProviderWithPolicy(allowAutoRegistration: false));
        _mockUserManager.Setup(x => x.Users).Returns(Array.Empty<ApplicationUser>().AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), ApplicationRoles.Admin))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "boom" }));

        // The rollback is the transaction's; what this can show is that the account and its role were written inside
        // it, and that the failure left it by throwing.
        var inTransaction = false;
        var createdInTransaction = false;
        var transactionFailed = false;
        _mockUserIdentityStore
            .Setup(s => s.ExecuteInTransaction(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>(async (action, ct) =>
            {
                inTransaction = true;
                try
                {
                    await action(ct);
                }
                catch
                {
                    transactionFailed = true;
                    throw;
                }
                finally
                {
                    inTransaction = false;
                }
            });
        _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>()))
            .Callback(() => createdInTransaction = inTransaction)
            .ReturnsAsync(IdentityResult.Success);
        var sut = CreateSut();

        // Act
        var act = () => sut.GetOrCreateFromPrincipalAsync(CreateNewUserPrincipal("oid-first", "tenant-1", "first@acme.example"));

        // Assert
        await act.Should().ThrowAsync<InternalServerException>();
        createdInTransaction.Should().BeTrue();
        transactionFailed.Should().BeTrue();
        _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>()), Times.Once);
        _mockUserIdentityStore.Verify(s => s.Add(It.IsAny<UserIdentity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryApplyPendingTenantMigration_ShouldNotRebind_WhenStagedBeforeItsTimeWasRecorded()
    {
        // Arrange
        const string tenantId = "tenant-new";
        const string upn = "alice@example.com";
        var user = CreateUser(id: "user-legacy");
        user.NormalizedUserName = upn.ToUpperInvariant();
        user.PendingMigrationTenantId = tenantId;
        user.PendingMigrationStagedAt = null;
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        var sut = CreateSut();

        // Act
        var result = await sut.TryApplyPendingTenantMigration(tenantId, "oid-1", upn);

        // Assert
        result.Should().BeNull();
    }

    private static ClaimsPrincipal CreateGenericPrincipal(string subject, string email, string? emailVerified)
    {
        var claims = new List<Claim> { new("sub", subject), new("email", email) };
        if (emailVerified is not null)
            claims.Add(new Claim("email_verified", emailVerified));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    #endregion

    #region GetOrCreateFromPrincipalAsync — unstaged link denial (F3)

    // Arranges the Entra sign-in path so that identity resolution finds nothing —
    // no active (tid, oid) row, no NULL-tenant backfill — leaving the token to fall
    // through to the link-or-create decision. `existing` is the account already in
    // the database that the token's display name / UPN will match.
    private void ArrangeUnstagedSignIn(ApplicationUser existing, OidcProvider? provider = null)
    {
        _mockOidcProviderRegistry
            .Setup(r => r.GetByName(LoginProviders.MicrosoftEntraId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(provider ?? CreateEntraProviderWithPolicy(requireEmployeeRecord: false));

        _mockUserManager.Setup(x => x.Users).Returns(new[] { existing }.AsQueryable().BuildMockDbSet().Object);

        _mockUserIdentityStore.Setup(s => s.FindActive(LoginProviders.MicrosoftEntraId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserIdentity?)null);
        _mockUserIdentityStore.Setup(s => s.FindActiveByNullTenant(LoginProviders.MicrosoftEntraId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserIdentity>());

        // The token's display name and UPN both resolve to the existing account —
        // this is exactly the mutable-attribute match F3 exploited.
        _mockUserManager.Setup(x => x.FindByNameAsync(It.IsAny<string>())).ReturnsAsync(existing);
        _mockUserManager.Setup(x => x.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(existing);
    }

    // Asserts the deny left the matched account exactly as it was: their active
    // identity was never deactivated and their password hash was never removed.
    // Both matter — the first is the takeover damage, the second is what lets them
    // keep using local login until an admin stages a migration.
    private void VerifyDenyWasInert(ApplicationUser matched, string? originalPasswordHash)
    {
        _mockUserIdentityStore.Verify(s => s.DeactivateAllActive(
            It.IsAny<string>(), It.IsAny<NodaTime.Instant>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockUserIdentityStore.Verify(s => s.DeactivateAllActive(
            It.IsAny<string>(), It.IsAny<NodaTime.Instant>(), It.IsAny<string>()),
            Times.Never);
        _mockUserIdentityStore.Verify(s => s.Add(It.IsAny<UserIdentity>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUserIdentityStore.Verify(s => s.Add(It.IsAny<UserIdentity>()), Times.Never);
        _mockUserManager.Verify(x => x.RemovePasswordAsync(It.IsAny<ApplicationUser>()), Times.Never);
        _mockUserManager.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        matched.PasswordHash.Should().Be(originalPasswordHash);
        matched.LoginProvider.Should().Be(LoginProviders.Wayd);
    }

    [Fact]
    public async Task GetOrCreateFromPrincipalAsync_ShouldDenyInertly_WhenTokenMatchesLocalAccountWithNoStagedMigration()
    {
        // Arrange — a Wayd-local account whose email an Entra directory object now
        // presents. Nothing authorizes adopting it, and denying must not degrade the
        // account: they keep signing in locally until an admin stages the migration.
        const string upn = "dana.reyes@acme.example";
        var local = CreateUser(id: "user-local-admin", userName: upn, loginProvider: LoginProviders.Wayd);
        local.NormalizedUserName = upn.ToUpperInvariant();
        local.NormalizedEmail = upn.ToUpperInvariant();
        local.PasswordHash = "AQAAAAIAAYagAAAAEPLACEHOLDERHASHVALUE==";
        var originalHash = local.PasswordHash;

        ArrangeUnstagedSignIn(local);
        var sut = CreateSut();

        // Act
        var act = () => sut.GetOrCreateFromPrincipalAsync(CreateNewUserPrincipal(
            "11111111-1111-1111-1111-111111111111",
            "22222222-2222-2222-2222-222222222222",
            upn,
            displayName: upn));

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*not linked to this identity provider*");
        VerifyDenyWasInert(local, originalHash);
    }

    [Fact]
    public async Task GetOrCreateFromPrincipalAsync_ShouldDenyInertly_WhenUserIsStagedForADifferentTenant()
    {
        // Arrange — a migration exists but targets another tenant, so it does not
        // authorize this token. The staged flag must survive the denial so the real
        // migration can still complete when the user signs in from the right tenant.
        const string upn = "sam.okafor@acme.example";
        const string stagedTenant = "33333333-3333-3333-3333-333333333333";
        const string tokenTenant = "44444444-4444-4444-4444-444444444444";

        var staged = CreateUser(id: "user-staged-elsewhere", userName: upn, loginProvider: LoginProviders.Wayd);
        staged.NormalizedUserName = upn.ToUpperInvariant();
        staged.NormalizedEmail = upn.ToUpperInvariant();
        staged.PasswordHash = "AQAAAAIAAYagAAAAEPLACEHOLDERHASHVALUE==";
        staged.PendingMigrationTenantId = stagedTenant;
        staged.PendingMigrationStagedAt = _dateTimeProvider.Now;
        var originalHash = staged.PasswordHash;

        ArrangeUnstagedSignIn(staged);
        var sut = CreateSut();

        // Act
        var act = () => sut.GetOrCreateFromPrincipalAsync(CreateNewUserPrincipal(
            "55555555-5555-5555-5555-555555555555", tokenTenant, upn, displayName: upn));

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*not linked to this identity provider*");
        VerifyDenyWasInert(staged, originalHash);
        staged.PendingMigrationTenantId.Should().Be(stagedTenant);
    }

    [Fact]
    public async Task GetOrCreateFromPrincipalAsync_ShouldDenyInertly_WhenAutoRegistrationIsDisabledAndAccountMatches()
    {
        // Arrange — the policy gates now apply to the matched-account branch too, but
        // the deny fires first either way. Previously this path linked regardless of
        // the provider's registration policy.
        const string upn = "priya.venkat@acme.example";
        var local = CreateUser(id: "user-policy-off", userName: upn, loginProvider: LoginProviders.Wayd);
        local.NormalizedUserName = upn.ToUpperInvariant();
        local.NormalizedEmail = upn.ToUpperInvariant();
        local.PasswordHash = "AQAAAAIAAYagAAAAEPLACEHOLDERHASHVALUE==";
        var originalHash = local.PasswordHash;

        ArrangeUnstagedSignIn(local, CreateEntraProviderWithPolicy(allowAutoRegistration: false));
        var sut = CreateSut();

        // Act
        var act = () => sut.GetOrCreateFromPrincipalAsync(CreateNewUserPrincipal(
            "66666666-6666-6666-6666-666666666666",
            "22222222-2222-2222-2222-222222222222",
            upn,
            displayName: upn));

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        VerifyDenyWasInert(local, originalHash);
    }

    [Fact]
    public async Task GetOrCreateFromPrincipalAsync_ShouldDenyInertly_WhenEmailIsExplicitlyUnverified()
    {
        // Arrange — an unverified address is not evidence of who the presenter is, so
        // it must not even be used to look up an account, let alone bind one.
        const string upn = "unverified@acme.example";
        var local = CreateUser(id: "user-unverified-target", userName: upn, loginProvider: LoginProviders.Wayd);
        local.NormalizedUserName = upn.ToUpperInvariant();
        local.NormalizedEmail = upn.ToUpperInvariant();
        local.PasswordHash = "AQAAAAIAAYagAAAAEPLACEHOLDERHASHVALUE==";
        var originalHash = local.PasswordHash;

        ArrangeUnstagedSignIn(local);

        var principal = CreateNewUserPrincipal(
            "77777777-7777-7777-7777-777777777777",
            "22222222-2222-2222-2222-222222222222",
            upn,
            displayName: upn);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim("email_verified", "false"));

        var sut = CreateSut();

        // Act
        var act = () => sut.GetOrCreateFromPrincipalAsync(principal);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*has not verified your email address*");
        VerifyDenyWasInert(local, originalHash);
    }

    [Fact]
    public async Task GetOrCreateFromPrincipalAsync_ShouldRebindStagedMigration_WhenTenantMatches()
    {
        // Arrange — the legitimate flow must keep working: an admin staged this user
        // for this token's tenant, so the rebind proceeds even though the same token
        // would have been denied without the staging.
        const string upn = "lin.zhao@acme.example";
        const string tenantId = "88888888-8888-8888-8888-888888888888";
        const string objectId = "99999999-9999-9999-9999-999999999999";

        var user = CreateUser(id: "user-staged-correctly", userName: upn, loginProvider: LoginProviders.MicrosoftEntraId);
        user.NormalizedUserName = upn.ToUpperInvariant();
        user.NormalizedEmail = upn.ToUpperInvariant();
        user.PendingMigrationTenantId = tenantId;
        user.PendingMigrationStagedAt = _dateTimeProvider.Now;

        _mockOidcProviderRegistry
            .Setup(r => r.GetByName(LoginProviders.MicrosoftEntraId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEntraProviderWithPolicy(requireEmployeeRecord: false));
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(["Basic"]);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        _mockUserIdentityStore.Setup(s => s.FindActive(LoginProviders.MicrosoftEntraId, tenantId, objectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserIdentity?)null);
        _mockUserIdentityStore.Setup(s => s.FindActiveByNullTenant(LoginProviders.MicrosoftEntraId, objectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserIdentity>());

        var sut = CreateSut();

        // Act
        var (resolvedId, _) = await sut.GetOrCreateFromPrincipalAsync(
            CreateNewUserPrincipal(objectId, tenantId, upn, displayName: upn));

        // Assert
        resolvedId.Should().Be(user.Id);
        user.PendingMigrationTenantId.Should().BeNull();
        _mockUserIdentityStore.Verify(s => s.Add(
            It.Is<UserIdentity>(ui =>
                ui.UserId == user.Id &&
                ui.ProviderTenantId == tenantId &&
                ui.ProviderSubject == objectId &&
                ui.IsActive),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOrCreateFromPrincipalAsync_ShouldRebindStagedProviderMigration_WhenLocalUserSignsInViaEntra()
    {
        // Arrange — the flow this bug (destacey/Wayd#741) blocked entirely: an admin
        // staged a local (Wayd) account's migration onto Entra. No active (tid, oid)
        // identity exists yet, and PendingMigrationTenantId is unset (this is a
        // provider migration, not a tenant migration), so resolution must fall through
        // to TryApplyPendingProviderMigration rather than denying the sign-in.
        const string upn = "morgan.ellis@acme.example";
        const string tenantId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        const string objectId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";

        var user = CreateUser(id: "user-local-migrating-to-entra", userName: upn, loginProvider: LoginProviders.Wayd);
        user.NormalizedUserName = upn.ToUpperInvariant();
        user.NormalizedEmail = upn.ToUpperInvariant();
        user.PendingMigrationProviderId = LoginProviders.MicrosoftEntraId;
        user.PendingProviderMigrationStagedAt = _dateTimeProvider.Now;
        user.PasswordHash = "AQAAAAIAAYagAAAAEPLACEHOLDERHASHVALUE==";

        _mockOidcProviderRegistry
            .Setup(r => r.GetByName(LoginProviders.MicrosoftEntraId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEntraProviderWithPolicy(requireEmployeeRecord: false));
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(["Basic"]);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.RemovePasswordAsync(user)).ReturnsAsync(IdentityResult.Success);
        _mockUserIdentityStore.Setup(s => s.FindActive(LoginProviders.MicrosoftEntraId, tenantId, objectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserIdentity?)null);
        _mockUserIdentityStore.Setup(s => s.FindActiveByNullTenant(LoginProviders.MicrosoftEntraId, objectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserIdentity>());

        await _refreshTokenStore.Issue(user.Id, SessionContext.None, TestContext.Current.CancellationToken);

        var sut = CreateSut();

        // Act
        var (resolvedId, _) = await sut.GetOrCreateFromPrincipalAsync(
            CreateNewUserPrincipal(objectId, tenantId, upn, displayName: upn));

        // Assert — same user Id preserved, LoginProvider flipped, flag cleared, and the
        // new active identity carries this token's tenant/subject.
        resolvedId.Should().Be(user.Id);
        user.LoginProvider.Should().Be(LoginProviders.MicrosoftEntraId);
        _refreshTokenStore.ActiveSessionCount(user.Id).Should().Be(0, "a session opened through the old sign-in must not outlive it");
        _refreshTokenStore.RevokeReasons(user.Id).Should().Equal(UserRefreshTokenRevokeReasons.SignInChanged);
        user.PendingMigrationProviderId.Should().BeNull();
        _mockUserIdentityStore.Verify(s => s.Add(
            It.Is<UserIdentity>(ui =>
                ui.UserId == user.Id &&
                ui.Provider == LoginProviders.MicrosoftEntraId &&
                ui.ProviderTenantId == tenantId &&
                ui.ProviderSubject == objectId &&
                ui.IsActive),
            It.IsAny<CancellationToken>()), Times.Once);
        _mockUserManager.Verify(x => x.RemovePasswordAsync(user), Times.Once);
    }

    [Fact]
    public async Task GetOrCreateFromPrincipalAsync_ShouldNotRebindStagedProviderMigration_WhenEmailIsExplicitlyUnverified()
    {
        // Arrange — the staged-migration rebind runs *before* the email_verified deny in
        // the create/link path, so an unverified token must be stopped at the identifier
        // itself. Otherwise someone who registered an unconfirmed address in the target
        // directory could complete a migration staged for the real owner of that address.
        const string upn = "morgan.ellis@acme.example";
        var user = CreateUser(id: "user-staged-unverified-token", userName: upn, loginProvider: LoginProviders.Wayd);
        user.NormalizedUserName = upn.ToUpperInvariant();
        user.NormalizedEmail = upn.ToUpperInvariant();
        user.PendingMigrationProviderId = LoginProviders.MicrosoftEntraId;
        user.PendingProviderMigrationStagedAt = _dateTimeProvider.Now;
        user.PasswordHash = "AQAAAAIAAYagAAAAEPLACEHOLDERHASHVALUE==";
        var originalHash = user.PasswordHash;

        ArrangeUnstagedSignIn(user);

        var principal = CreateNewUserPrincipal(
            "cccccccc-cccc-cccc-cccc-cccccccccccc",
            "dddddddd-dddd-dddd-dddd-dddddddddddd",
            upn,
            displayName: upn);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim("email_verified", "false"));

        var sut = CreateSut();

        // Act
        var act = () => sut.GetOrCreateFromPrincipalAsync(principal);

        // Assert — denied, and the staged migration is still pending for a verified sign-in.
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*has not verified your email address*");
        user.PendingMigrationProviderId.Should().Be(LoginProviders.MicrosoftEntraId);
        VerifyDenyWasInert(user, originalHash);
    }

    [Fact]
    public async Task TryApplyPendingProviderMigration_ShouldSetProviderAndClearPassword_WhenStagedUserMigrates()
    {
        // Arrange — the supported staged-migration route. It must leave no split state:
        // the account that had to be repaired by hand carried LoginProvider = Wayd next
        // to a live SSO identity and a still-present password hash. The hash is cleared
        // whenever one is present, whatever the previous provider was.
        const string targetProvider = "Acme-Okta";
        const string email = "jordan.blake@acme.example";

        var user = CreateUser(id: "user-migrating", loginProvider: LoginProviders.MicrosoftEntraId);
        user.NormalizedEmail = email.ToUpperInvariant();
        user.PendingMigrationProviderId = targetProvider;
        user.PendingProviderMigrationStagedAt = _dateTimeProvider.Now;
        user.PasswordHash = "AQAAAAIAAYagAAAAEPLACEHOLDERHASHVALUE==";

        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.RemovePasswordAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        var result = await sut.TryApplyPendingProviderMigration(targetProvider, null, "okta-sub-placeholder", email);

        // Assert
        result.Should().NotBeNull();
        user.LoginProvider.Should().Be(targetProvider);
        user.PendingMigrationProviderId.Should().BeNull();
        _mockUserManager.Verify(x => x.RemovePasswordAsync(user), Times.Once);
    }

    [Fact]
    public async Task TryApplyPendingProviderMigration_ShouldRebind_WhenStagedUserIsLocal()
    {
        // A local (Wayd) account staged for SSO is the primary use case for this
        // feature — StageProviderMigration accepts local users and the docs advertise
        // Wayd→OIDC. The candidate query previously excluded LoginProvider == Wayd,
        // which left every such migration permanently stuck (destacey/Wayd#741).
        const string targetProvider = "Acme-Okta";
        const string email = "jordan.blake@acme.example";

        var user = CreateUser(id: "user-local-staged", loginProvider: LoginProviders.Wayd);
        user.NormalizedEmail = email.ToUpperInvariant();
        user.PendingMigrationProviderId = targetProvider;
        user.PendingProviderMigrationStagedAt = _dateTimeProvider.Now;
        user.PasswordHash = "AQAAAAIAAYagAAAAEPLACEHOLDERHASHVALUE==";

        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.RemovePasswordAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        var result = await sut.TryApplyPendingProviderMigration(targetProvider, null, "okta-sub-placeholder", email);

        // Assert
        result.Should().NotBeNull();
        user.LoginProvider.Should().Be(targetProvider);
        user.PendingMigrationProviderId.Should().BeNull();
        _mockUserManager.Verify(x => x.RemovePasswordAsync(user), Times.Once);
    }

    [Fact]
    public async Task TryApplyPendingProviderMigration_ShouldStillSucceed_WhenClearingLocalPasswordFails()
    {
        // Arrange — password removal runs after the rebind transaction has committed,
        // so a failure there has nothing to roll back and no retry path (the next
        // sign-in resolves on the triple and never re-enters this method). It must not
        // fail a migration that actually succeeded; the leftover hash is unreachable at
        // login because the Wayd identity is deactivated.
        const string targetProvider = "Acme-Okta";
        const string email = "rowan.pike@acme.example";

        var user = CreateUser(id: "user-hash-stuck", loginProvider: LoginProviders.MicrosoftEntraId);
        user.NormalizedEmail = email.ToUpperInvariant();
        user.PendingMigrationProviderId = targetProvider;
        user.PendingProviderMigrationStagedAt = _dateTimeProvider.Now;
        user.PasswordHash = "AQAAAAIAAYagAAAAEPLACEHOLDERHASHVALUE==";

        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.RemovePasswordAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Concurrency failure." }));

        var sut = CreateSut();

        // Act
        var result = await sut.TryApplyPendingProviderMigration(targetProvider, null, "okta-sub-placeholder", email);

        // Assert — the rebind stands and is reported as such.
        result.Should().NotBeNull();
        user.LoginProvider.Should().Be(targetProvider);
        user.PendingMigrationProviderId.Should().BeNull();
        _mockUserIdentityStore.Verify(s => s.Add(
            It.Is<UserIdentity>(ui => ui.Provider == targetProvider && ui.IsActive),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConvertToLocalAccount_ShouldKeepPassword_WhenMigratingTowardLocal()
    {
        // Arrange — the reverse direction. Relinking toward a local account sets a
        // credential; the password-clearing step belongs to local→external only and
        // must not run here, or the converted user would have no way to sign in.
        var user = CreateUser(id: "user-converting", loginProvider: LoginProviders.MicrosoftEntraId);

        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-token");
        _mockUserManager.Setup(x => x.ResetPasswordAsync(user, "reset-token", "NewPass123!"))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        var result = await sut.ConvertToLocalAccount(
            new ConvertToLocalAccountCommand(user.Id, "NewPass123!"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.LoginProvider.Should().Be(LoginProviders.Wayd);
        _mockUserManager.Verify(x => x.RemovePasswordAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task GetOrCreateFromPrincipalAsync_ShouldCreateFirstUser_WhenDatabaseIsEmpty()
    {
        // Arrange — bootstrap must still work with no users present. There is no
        // account to match, so the deny cannot fire, and the policy gates are bypassed
        // by design so a fresh install can always create its admin.
        var provider = CreateEntraProviderWithPolicy(allowAutoRegistration: false);
        _mockOidcProviderRegistry
            .Setup(r => r.GetByName(LoginProviders.MicrosoftEntraId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(provider);

        _mockUserManager.Setup(x => x.Users).Returns(Array.Empty<ApplicationUser>().AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.FindByNameAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        _mockUserManager.Setup(x => x.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        ApplicationUser? created = null;
        _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>()))
            .Callback<ApplicationUser>(u => created = u)
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.GetRolesAsync(It.IsAny<ApplicationUser>())).ReturnsAsync([]);
        _mockUserManager.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
        _mockUserIdentityStore.Setup(s => s.FindActive(LoginProviders.MicrosoftEntraId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserIdentity?)null);
        _mockUserIdentityStore.Setup(s => s.FindActiveByNullTenant(LoginProviders.MicrosoftEntraId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserIdentity>());
        _mockUserIdentityStore.Setup(s => s.ExistsActive(It.IsAny<string>(), LoginProviders.MicrosoftEntraId))
            .ReturnsAsync(false);
        _mockDispatcher.Setup(s => s.Send(It.IsAny<Wayd.Common.Application.Employees.Queries.GetEmployeeByEmailQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var sut = CreateSut();

        // Act
        var (resolvedId, _) = await sut.GetOrCreateFromPrincipalAsync(CreateNewUserPrincipal(
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
            "founder@acme.example"));

        // Assert
        resolvedId.Should().NotBeNullOrWhiteSpace();
        _mockUserManager.Verify(x => x.CreateAsync(It.Is<ApplicationUser>(
            u => u.LoginProvider == LoginProviders.MicrosoftEntraId)), Times.Once);
        _mockUserManager.Verify(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), ApplicationRoles.Admin), Times.Once);

        var events = RaisedEvents(created!);
        events.Should().HaveCount(2);
        events.First().Should().BeOfType<ApplicationUserCreatedEvent>().Which.Actor.Should().Be(EventActor.System);
        events.Last().Should().BeOfType<ApplicationUserRolesChangedEvent>().Which.Added.Should().Equal(AdminRoleId);
    }

    #endregion

    #region StageProviderMigration

    [Fact]
    public async Task StageProviderMigration_ShouldSetPendingProvider_WhenOidcUserHasActiveIdentity()
    {
        const string targetProvider = "Acme-Okta";
        var user = CreateUser(loginProvider: LoginProviders.MicrosoftEntraId);

        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserIdentityStore.Setup(s => s.ExistsActive(user.Id, LoginProviders.MicrosoftEntraId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockOidcProviderRegistry.Setup(r => r.GetByName(targetProvider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildOidcProvider(targetProvider));
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        var result = await sut.StageProviderMigration(
            new StageProviderMigrationCommand(user.Id, targetProvider),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        user.PendingMigrationProviderId.Should().Be(targetProvider);
        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
        var staged = RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserProviderMigrationStagedEvent>().Subject;
        staged.TargetProvider.Should().Be(targetProvider);
        staged.PreviousTargetProvider.Should().BeNull();
    }

    [Fact]
    public async Task StageProviderMigration_ShouldSucceed_ForLocalUser()
    {
        // Local (Wayd) users can now be staged to migrate to an OIDC provider.
        const string targetProvider = "Acme-Okta";
        var user = CreateUser(loginProvider: LoginProviders.Wayd);

        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserIdentityStore.Setup(s => s.ExistsActive(user.Id, LoginProviders.Wayd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockOidcProviderRegistry.Setup(r => r.GetByName(targetProvider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildOidcProvider(targetProvider));
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        var result = await sut.StageProviderMigration(
            new StageProviderMigrationCommand(user.Id, targetProvider),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        user.PendingMigrationProviderId.Should().Be(targetProvider);
    }

    [Fact]
    public async Task StageProviderMigration_ShouldOverwritePreviousTarget_WhenAlreadyStaged()
    {
        const string firstProvider = "Acme-Okta";
        const string secondProvider = "Acme-Google";
        var user = CreateUser(loginProvider: LoginProviders.MicrosoftEntraId);
        user.PendingMigrationProviderId = firstProvider;
        user.PendingProviderMigrationStagedAt = _dateTimeProvider.Now;

        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserIdentityStore.Setup(s => s.ExistsActive(user.Id, LoginProviders.MicrosoftEntraId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockOidcProviderRegistry.Setup(r => r.GetByName(secondProvider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildOidcProvider(secondProvider));
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        var result = await sut.StageProviderMigration(
            new StageProviderMigrationCommand(user.Id, secondProvider),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        user.PendingMigrationProviderId.Should().Be(secondProvider);
        RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserProviderMigrationStagedEvent>()
            .Which.PreviousTargetProvider.Should().Be(firstProvider);
    }

    [Fact]
    public async Task StageProviderMigration_ShouldFail_WhenTargetIsSameAsCurrentProvider()
    {
        var user = CreateUser(loginProvider: LoginProviders.MicrosoftEntraId);
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);

        var sut = CreateSut();

        var result = await sut.StageProviderMigration(
            new StageProviderMigrationCommand(user.Id, LoginProviders.MicrosoftEntraId),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("same as the user's current provider");
        _mockUserManager.Verify(x => x.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task StageProviderMigration_ShouldFail_WhenTargetProviderDoesNotExist()
    {
        var user = CreateUser(loginProvider: LoginProviders.MicrosoftEntraId);
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockOidcProviderRegistry.Setup(r => r.GetByName("unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Wayd.Common.Domain.Identity.OidcProvider?)null);

        var sut = CreateSut();

        var result = await sut.StageProviderMigration(
            new StageProviderMigrationCommand(user.Id, "unknown"),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("does not exist");
        _mockUserManager.Verify(x => x.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task StageProviderMigration_ShouldFail_WhenTargetProviderIsDisabled()
    {
        const string targetProvider = "Acme-Okta";
        var user = CreateUser(loginProvider: LoginProviders.MicrosoftEntraId);
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockOidcProviderRegistry.Setup(r => r.GetByName(targetProvider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildOidcProvider(targetProvider, isEnabled: false));

        var sut = CreateSut();

        var result = await sut.StageProviderMigration(
            new StageProviderMigrationCommand(user.Id, targetProvider),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("disabled");
        _mockUserManager.Verify(x => x.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task StageProviderMigration_ShouldThrowNotFound_WhenUserDoesNotExist()
    {
        _mockUserManager.Setup(x => x.Users).Returns(Array.Empty<ApplicationUser>().AsQueryable().BuildMockDbSet().Object);

        var sut = CreateSut();

        var act = () => sut.StageProviderMigration(
            new StageProviderMigrationCommand("missing", "Acme-Okta"),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region CancelProviderMigration

    [Fact]
    public async Task CancelProviderMigration_ShouldClearPendingProvider_WhenStaged()
    {
        var user = CreateUser();
        user.PendingMigrationProviderId = "Acme-Okta";
        user.PendingProviderMigrationStagedAt = _dateTimeProvider.Now;

        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        var result = await sut.CancelProviderMigration(user.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        user.PendingMigrationProviderId.Should().BeNull();
        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
        RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserProviderMigrationCanceledEvent>()
            .Which.TargetProvider.Should().Be("Acme-Okta");
    }

    [Fact]
    public async Task CancelProviderMigration_ShouldBeIdempotent_WhenNothingStaged()
    {
        var user = CreateUser();
        user.PendingMigrationProviderId = null;

        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);

        var sut = CreateSut();

        var result = await sut.CancelProviderMigration(user.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        _mockUserManager.Verify(x => x.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        RaisedEvents(user).Should().BeEmpty();
    }

    [Fact]
    public async Task CancelProviderMigration_ShouldThrowNotFound_WhenUserDoesNotExist()
    {
        _mockUserManager.Setup(x => x.Users).Returns(Array.Empty<ApplicationUser>().AsQueryable().BuildMockDbSet().Object);

        var sut = CreateSut();

        var act = () => sut.CancelProviderMigration("missing", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region TryApplyPendingProviderMigration

    [Fact]
    public async Task TryApplyPendingProviderMigration_ShouldRebindIdentity_WhenMigrationStagedAndEmailMatches()
    {
        const string targetProvider = "Acme-Okta";
        const string subject = "okta-sub-abc123";
        const string email = "alice@example.com";

        var user = CreateUser(id: "user-rebind", loginProvider: LoginProviders.MicrosoftEntraId);
        user.NormalizedEmail = email.ToUpperInvariant();
        user.PendingMigrationProviderId = targetProvider;
        user.PendingProviderMigrationStagedAt = _dateTimeProvider.Now;

        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        var result = await sut.TryApplyPendingProviderMigration(targetProvider, null, subject, email);

        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
        user.LoginProvider.Should().Be(targetProvider);
        user.PendingMigrationProviderId.Should().BeNull();
        var completed = RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserProviderMigrationCompletedEvent>().Subject;
        completed.FromProvider.Should().Be(LoginProviders.MicrosoftEntraId);
        completed.ToProvider.Should().Be(targetProvider);

        _mockUserIdentityStore.Verify(s => s.DeactivateAllActive(
            user.Id, It.IsAny<NodaTime.Instant>(), UserIdentityUnlinkReasons.ProviderRelinked,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockUserIdentityStore.Verify(s => s.Add(
            It.Is<UserIdentity>(ui =>
                ui.UserId == user.Id &&
                ui.Provider == targetProvider &&
                ui.ProviderTenantId == null &&
                ui.ProviderSubject == subject &&
                ui.IsActive),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TryApplyPendingProviderMigration_ShouldReturnNull_WhenEmailIsNull()
    {
        var sut = CreateSut();

        var result = await sut.TryApplyPendingProviderMigration("Acme-Okta", null, "sub-123", email: null);

        result.Should().BeNull();
        _mockUserIdentityStore.Verify(s => s.DeactivateAllActive(
            It.IsAny<string>(), It.IsAny<NodaTime.Instant>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryApplyPendingProviderMigration_ShouldReturnNull_WhenNoPendingMigrationForProvider()
    {
        const string targetProvider = "Acme-Okta";
        const string email = "alice@example.com";

        // User has no pending migration (PendingMigrationProviderId is null)
        var user = CreateUser(loginProvider: LoginProviders.MicrosoftEntraId);
        user.NormalizedEmail = email.ToUpperInvariant();

        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);

        var sut = CreateSut();

        var result = await sut.TryApplyPendingProviderMigration(targetProvider, null, "sub-123", email);

        result.Should().BeNull();
        _mockUserIdentityStore.Verify(s => s.DeactivateAllActive(
            It.IsAny<string>(), It.IsAny<NodaTime.Instant>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryApplyPendingProviderMigration_ShouldThrowAndSignalRollback_WhenUpdateFails()
    {
        const string targetProvider = "Acme-Okta";
        const string email = "alice@example.com";

        var user = CreateUser(id: "user-rebind-fail", loginProvider: LoginProviders.MicrosoftEntraId);
        user.NormalizedEmail = email.ToUpperInvariant();
        user.PendingMigrationProviderId = targetProvider;
        user.PendingProviderMigrationStagedAt = _dateTimeProvider.Now;

        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Concurrency failure." }));

        Exception? captured = null;
        _mockUserIdentityStore
            .Setup(s => s.ExecuteInTransaction(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>(async (action, ct) =>
            {
                try { await action(ct); }
                catch (Exception ex) { captured = ex; throw; }
            });

        var sut = CreateSut();

        var act = () => sut.TryApplyPendingProviderMigration(targetProvider, null, "sub-123", email);

        await act.Should().ThrowAsync<InternalServerException>()
            .WithMessage("Sign-in could not complete the provider migration. Contact an administrator.");
        RaisedEvents(user).Should().BeEmpty("nothing was saved, so nothing may be recorded");

        captured.Should().NotBeNull("the transaction lambda must throw to trigger rollback");
        _mockUserIdentityStore.Verify(s => s.Add(It.IsAny<UserIdentity>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region ConvertToLocalAccount

    [Fact]
    public async Task ConvertToLocalAccount_ShouldSucceed_WhenOidcUserConverts()
    {
        var user = CreateUser(loginProvider: LoginProviders.MicrosoftEntraId);

        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-token");
        _mockUserManager.Setup(x => x.ResetPasswordAsync(user, "reset-token", "NewPass123!"))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        await _refreshTokenStore.Issue(user.Id, SessionContext.None, TestContext.Current.CancellationToken);

        var sut = CreateSut();

        var result = await sut.ConvertToLocalAccount(
            new ConvertToLocalAccountCommand(user.Id, "NewPass123!"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        user.LoginProvider.Should().Be(LoginProviders.Wayd);
        _refreshTokenStore.ActiveSessionCount(user.Id).Should().Be(0, "a session opened through the old sign-in must not outlive it");
        _refreshTokenStore.RevokeReasons(user.Id).Should().Equal(UserRefreshTokenRevokeReasons.SignInChanged);
        user.MustChangePassword.Should().BeTrue();
        user.PendingMigrationProviderId.Should().BeNull();

        _mockUserIdentityStore.Verify(s => s.DeactivateAllActive(
            user.Id, It.IsAny<NodaTime.Instant>(), UserIdentityUnlinkReasons.ProviderRelinked,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockUserIdentityStore.Verify(s => s.Add(
            It.Is<UserIdentity>(ui =>
                ui.UserId == user.Id &&
                ui.Provider == LoginProviders.Wayd &&
                ui.ProviderTenantId == null &&
                ui.ProviderSubject == user.Id &&
                ui.IsActive),
            It.IsAny<CancellationToken>()), Times.Once);

        RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserConvertedToLocalAccountEvent>()
            .Which.FromProvider.Should().Be(LoginProviders.MicrosoftEntraId);
    }

    [Fact]
    public async Task ConvertToLocalAccount_ShouldClearPendingProviderMigration_WhenFlagIsSet()
    {
        var user = CreateUser(loginProvider: LoginProviders.MicrosoftEntraId);
        user.PendingMigrationProviderId = "Acme-Okta";
        user.PendingProviderMigrationStagedAt = _dateTimeProvider.Now;

        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-token");
        _mockUserManager.Setup(x => x.ResetPasswordAsync(user, "reset-token", "NewPass123!"))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        var result = await sut.ConvertToLocalAccount(
            new ConvertToLocalAccountCommand(user.Id, "NewPass123!"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        user.PendingMigrationProviderId.Should().BeNull();
        RaisedEvents(user).Select(e => e.GetType()).Should().Equal(
            typeof(ApplicationUserProviderMigrationCanceledEvent),
            typeof(ApplicationUserConvertedToLocalAccountEvent));
    }

    [Fact]
    public async Task ConvertToLocalAccount_ShouldFail_WhenUserIsAlreadyLocal()
    {
        var user = CreateUser(loginProvider: LoginProviders.Wayd);
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);

        var sut = CreateSut();

        var result = await sut.ConvertToLocalAccount(
            new ConvertToLocalAccountCommand(user.Id, "NewPass123!"),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already a local account");
        _mockUserManager.Verify(x => x.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task ConvertToLocalAccount_ShouldReturnFailure_WhenPasswordValidationFails()
    {
        var user = CreateUser(loginProvider: LoginProviders.MicrosoftEntraId);
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);

        // PasswordValidators is a non-virtual IList<> populated by ASP.NET Identity
        // from the UserManager ctor arg. We can't Moq.Setup it, but we can add to
        // the list that was already created — UserManager<T> exposes it as IList.
        var mockValidator = new Mock<IPasswordValidator<ApplicationUser>>();
        mockValidator.Setup(v => v.ValidateAsync(It.IsAny<UserManager<ApplicationUser>>(), user, "weak"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too short." }));
        _mockUserManager.Object.PasswordValidators.Add(mockValidator.Object);

        var sut = CreateSut();

        var result = await sut.ConvertToLocalAccount(
            new ConvertToLocalAccountCommand(user.Id, "weak"),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Password too short.");
        // Transaction must not have opened — no identity writes
        _mockUserIdentityStore.Verify(s => s.DeactivateAllActive(
            It.IsAny<string>(), It.IsAny<NodaTime.Instant>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ConvertToLocalAccount_ShouldThrowNotFound_WhenUserDoesNotExist()
    {
        _mockUserManager.Setup(x => x.Users).Returns(Array.Empty<ApplicationUser>().AsQueryable().BuildMockDbSet().Object);

        var sut = CreateSut();

        var act = () => sut.ConvertToLocalAccount(
            new ConvertToLocalAccountCommand("missing", "NewPass123!"),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    // Builds a minimal OidcProvider via reflection so tests don't need a Create factory.
    private static Wayd.Common.Domain.Identity.OidcProvider BuildOidcProvider(string name, bool isEnabled = true)
    {
        var provider = (Wayd.Common.Domain.Identity.OidcProvider)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(Wayd.Common.Domain.Identity.OidcProvider));

        typeof(Wayd.Common.Domain.Identity.OidcProvider)
            .GetProperty(nameof(Wayd.Common.Domain.Identity.OidcProvider.Name))!
            .SetValue(provider, name);
        typeof(Wayd.Common.Domain.Identity.OidcProvider)
            .GetProperty(nameof(Wayd.Common.Domain.Identity.OidcProvider.IsEnabled))!
            .SetValue(provider, isEnabled);

        return provider;
    }

    #region UnlockUserAsync

    [Fact]
    public async Task UnlockUserAsync_ShouldSucceed_WhenUserIsLockedOut()
    {
        // Arrange
        var user = CreateUser(loginProvider: LoginProviders.Wayd);
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(10);
        user.AccessFailedCount = 5;
        _mockUserManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(true);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        var result = await sut.UnlockUserAsync("user-1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.LockoutEnd.Should().BeNull();
        user.AccessFailedCount.Should().Be(0);
        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
        RaisedEvents(user).Should().ContainSingle().Which.Should().BeOfType<ApplicationUserUnlockedEvent>();
    }

    [Fact]
    public async Task UnlockUserAsync_ShouldReturnFailure_WhenUserIsNotLockedOut()
    {
        // Arrange
        var user = CreateUser(loginProvider: LoginProviders.Wayd);
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);

        var sut = CreateSut();

        // Act
        var result = await sut.UnlockUserAsync("user-1");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not currently locked out");
        _mockUserManager.Verify(x => x.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        RaisedEvents(user).Should().BeEmpty();
    }

    [Fact]
    public async Task UnlockUserAsync_ShouldThrowNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        _mockUserManager.Setup(x => x.FindByIdAsync("missing")).ReturnsAsync((ApplicationUser?)null);

        var sut = CreateSut();

        // Act
        var act = () => sut.UnlockUserAsync("missing");

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    #endregion

    #region Preferences

    private static UserHomeLayout SavedHomeLayout() => new()
    {
        Widgets =
        [
            new UserHomeLayoutWidget { WidgetId = "my-risks", Column = HomeLayoutColumn.Side, Visible = true },
            new UserHomeLayoutWidget { WidgetId = "following", Column = HomeLayoutColumn.Main, Visible = false },
        ],
    };

    private ApplicationUser UserWithPreferences(UserPreferences preferences)
    {
        var user = CreateUser();
        user.Preferences = preferences;
        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        return user;
    }

    [Fact]
    public async Task UpdateThemeConfig_ShouldKeepHomeLayoutAndTours()
    {
        // Arrange
        var layout = SavedHomeLayout();
        var user = UserWithPreferences(new UserPreferences
        {
            Tours = new() { ["home"] = true },
            HomeLayout = layout,
        });
        var sut = CreateSut();

        // Act
        var result = await sut.UpdateThemeConfig(
            "user-1",
            new UserThemeConfigDto { ColorPrimary = "#123456", UseCompactAlgorithm = true },
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        user.Preferences.ThemeConfig.Should().BeEquivalentTo(new UserThemeConfig { ColorPrimary = "#123456", UseCompactAlgorithm = true });
        user.Preferences.HomeLayout.Should().BeSameAs(layout);
        user.Preferences.Tours.Should().Equal(new Dictionary<string, bool> { ["home"] = true });
        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task UpdatePreferences_ShouldKeepHomeLayout()
    {
        // Arrange
        var layout = SavedHomeLayout();
        var user = UserWithPreferences(new UserPreferences { HomeLayout = layout });
        var sut = CreateSut();

        // Act
        var result = await sut.UpdatePreferences(
            "user-1",
            new UserPreferencesDto { Tours = new() { ["projects"] = true } },
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        user.Preferences.Tours.Should().Equal(new Dictionary<string, bool> { ["projects"] = true });
        user.Preferences.HomeLayout.Should().BeSameAs(layout);
        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task UpdateHomeLayout_ShouldSaveWidgetsInOrder_AndKeepThemeAndTours()
    {
        // Arrange
        var theme = new UserThemeConfig { ColorPrimary = "#abcdef", UseCompactAlgorithm = true };
        var user = UserWithPreferences(new UserPreferences
        {
            Tours = new() { ["home"] = true },
            ThemeConfig = theme,
        });
        var layout = new UserHomeLayoutDto
        {
            Widgets =
            [
                new UserHomeLayoutWidgetDto { WidgetId = "following", Column = HomeLayoutColumn.Main, Visible = true },
                new UserHomeLayoutWidgetDto { WidgetId = "my-projects", Column = HomeLayoutColumn.Side, Visible = false },
            ],
        };
        var sut = CreateSut();

        // Act
        var result = await sut.UpdateHomeLayout("user-1", layout, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        user.Preferences.HomeLayout!.Widgets.Should().BeEquivalentTo(
            [
                new UserHomeLayoutWidget { WidgetId = "following", Column = HomeLayoutColumn.Main, Visible = true },
                new UserHomeLayoutWidget { WidgetId = "my-projects", Column = HomeLayoutColumn.Side, Visible = false },
            ],
            o => o.WithStrictOrdering());
        user.Preferences.ThemeConfig.Should().BeSameAs(theme);
        user.Preferences.Tours.Should().Equal(new Dictionary<string, bool> { ["home"] = true });
        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task UpdateHomeLayout_ShouldClearTheLayout_WhenNoWidgetsAreSent()
    {
        // Arrange
        var user = UserWithPreferences(new UserPreferences { HomeLayout = SavedHomeLayout() });
        var sut = CreateSut();

        // Act
        var result = await sut.UpdateHomeLayout("user-1", new UserHomeLayoutDto(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        user.Preferences.HomeLayout.Should().BeNull();
        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task UpdateHomeLayout_ShouldFail_WhenUserDoesNotExist()
    {
        // Arrange
        UserWithPreferences(new UserPreferences());
        var sut = CreateSut();

        // Act
        var result = await sut.UpdateHomeLayout("missing", new UserHomeLayoutDto(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
        _mockUserManager.Verify(x => x.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task GetHomeLayout_ShouldReturnTheSavedWidgetsInOrder()
    {
        // Arrange
        UserWithPreferences(new UserPreferences { HomeLayout = SavedHomeLayout() });
        var sut = CreateSut();

        // Act
        var layout = await sut.GetHomeLayout("user-1", TestContext.Current.CancellationToken);

        // Assert
        layout.Widgets.Should().BeEquivalentTo(
            [
                new UserHomeLayoutWidgetDto { WidgetId = "my-risks", Column = HomeLayoutColumn.Side, Visible = true },
                new UserHomeLayoutWidgetDto { WidgetId = "following", Column = HomeLayoutColumn.Main, Visible = false },
            ],
            o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task GetHomeLayout_ShouldReturnEachWidgetsSettings()
    {
        // Arrange
        UserWithPreferences(new UserPreferences
        {
            HomeLayout = new UserHomeLayout
            {
                Widgets =
                [
                    new UserHomeLayoutWidget
                    {
                        WidgetId = "my-risks",
                        Column = HomeLayoutColumn.Side,
                        Visible = true,
                        Settings = new() { ["rows"] = "10" },
                    },
                    new UserHomeLayoutWidget { WidgetId = "following", Column = HomeLayoutColumn.Main, Visible = true },
                ],
            },
        });
        var sut = CreateSut();

        // Act
        var layout = await sut.GetHomeLayout("user-1", TestContext.Current.CancellationToken);

        // Assert
        layout.Widgets[0].Settings.Should().Equal(new Dictionary<string, string> { ["rows"] = "10" });
        layout.Widgets[1].Settings.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateHomeLayout_ShouldSaveEachWidgetsSettings_AndNoneAsNull()
    {
        // Arrange
        var user = UserWithPreferences(new UserPreferences());
        var layout = new UserHomeLayoutDto
        {
            Widgets =
            [
                new UserHomeLayoutWidgetDto
                {
                    WidgetId = "following",
                    Column = HomeLayoutColumn.Main,
                    Visible = true,
                    Settings = new() { ["rows"] = "10" },
                },
                new UserHomeLayoutWidgetDto { WidgetId = "my-projects", Column = HomeLayoutColumn.Side, Visible = true },
            ],
        };
        var sut = CreateSut();

        // Act
        var result = await sut.UpdateHomeLayout("user-1", layout, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var saved = user.Preferences.HomeLayout!.Widgets;
        saved[0].Settings.Should().Equal(new Dictionary<string, string> { ["rows"] = "10" });
        saved[1].Settings.Should().BeNull();
    }

    [Fact]
    public async Task UpdateThemeConfig_ShouldKeepWidgetSettings()
    {
        // Arrange
        var user = UserWithPreferences(new UserPreferences
        {
            HomeLayout = new UserHomeLayout
            {
                Widgets =
                [
                    new UserHomeLayoutWidget
                    {
                        WidgetId = "my-risks",
                        Column = HomeLayoutColumn.Side,
                        Visible = true,
                        Settings = new() { ["rows"] = "10" },
                    },
                ],
            },
        });
        var sut = CreateSut();

        // Act
        var result = await sut.UpdateThemeConfig("user-1", null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        user.Preferences.HomeLayout!.Widgets[0].Settings
            .Should().Equal(new Dictionary<string, string> { ["rows"] = "10" });
    }

    [Fact]
    public async Task GetHomeLayout_ShouldReturnNoWidgets_WhenNoneIsSaved()
    {
        // Arrange
        UserWithPreferences(new UserPreferences());
        var sut = CreateSut();

        // Act
        var layout = await sut.GetHomeLayout("user-1", TestContext.Current.CancellationToken);

        // Assert
        layout.Widgets.Should().BeEmpty();
        layout.MutedTiles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHomeLayout_ShouldReturnTheMutedTiles()
    {
        // Arrange
        var saved = SavedHomeLayout();
        saved.MutedTiles = ["unread-notifications", "tasks-due-this-week"];
        UserWithPreferences(new UserPreferences { HomeLayout = saved });
        var sut = CreateSut();

        // Act
        var layout = await sut.GetHomeLayout("user-1", TestContext.Current.CancellationToken);

        // Assert
        layout.MutedTiles.Should().Equal("unread-notifications", "tasks-due-this-week");
    }

    [Fact]
    public async Task UpdateHomeLayout_ShouldKeepMutedTiles_WhenNoWidgetsAreSent()
    {
        // Arrange
        var user = UserWithPreferences(new UserPreferences());
        var sut = CreateSut();

        // Act
        var result = await sut.UpdateHomeLayout(
            "user-1",
            new UserHomeLayoutDto { MutedTiles = ["unread-notifications", "unread-notifications"] },
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        user.Preferences.HomeLayout!.Widgets.Should().BeEmpty();
        user.Preferences.HomeLayout.MutedTiles.Should().Equal("unread-notifications");
    }

    [Fact]
    public async Task UpdateHomeLayout_ShouldSaveNoMutedTilesAsNull()
    {
        // Arrange
        var user = UserWithPreferences(new UserPreferences());
        var layout = new UserHomeLayoutDto
        {
            Widgets = [new UserHomeLayoutWidgetDto { WidgetId = "following", Column = HomeLayoutColumn.Main, Visible = true }],
        };
        var sut = CreateSut();

        // Act
        var result = await sut.UpdateHomeLayout("user-1", layout, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        user.Preferences.HomeLayout!.MutedTiles.Should().BeNull();
    }

    [Fact]
    public async Task UpdateHomeLayout_ShouldClearSavedMutedTiles_WhenNoneAreSent()
    {
        // Arrange
        var saved = SavedHomeLayout();
        saved.MutedTiles = ["unread-notifications"];
        var user = UserWithPreferences(new UserPreferences { HomeLayout = saved });
        var layout = new UserHomeLayoutDto
        {
            Widgets = [new UserHomeLayoutWidgetDto { WidgetId = "following", Column = HomeLayoutColumn.Main, Visible = true }],
        };
        var sut = CreateSut();

        // Act
        var result = await sut.UpdateHomeLayout("user-1", layout, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        user.Preferences.HomeLayout!.MutedTiles.Should().BeNull();
    }

    [Fact]
    public async Task GetHomeLayout_ShouldReturnNoMutedTiles_WhenTheStoredLayoutHasNone()
    {
        // Arrange
        var saved = SavedHomeLayout();
        saved.MutedTiles = null;
        UserWithPreferences(new UserPreferences { HomeLayout = saved });
        var sut = CreateSut();

        // Act
        var layout = await sut.GetHomeLayout("user-1", TestContext.Current.CancellationToken);

        // Assert
        layout.Widgets.Should().NotBeEmpty();
        layout.MutedTiles.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateThemeConfig_ShouldKeepMutedTiles()
    {
        // Arrange
        var saved = SavedHomeLayout();
        saved.MutedTiles = ["unread-notifications"];
        var user = UserWithPreferences(new UserPreferences { HomeLayout = saved });
        var sut = CreateSut();

        // Act
        var result = await sut.UpdateThemeConfig("user-1", null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        user.Preferences.HomeLayout!.MutedTiles.Should().Equal("unread-notifications");
    }

    [Fact]
    public async Task UpdatePreferences_ShouldKeepMutedTiles()
    {
        // Arrange
        var saved = SavedHomeLayout();
        saved.MutedTiles = ["unread-notifications"];
        var user = UserWithPreferences(new UserPreferences { HomeLayout = saved });
        var sut = CreateSut();

        // Act
        var result = await sut.UpdatePreferences(
            "user-1",
            new UserPreferencesDto { Tours = new() { ["projects"] = true } },
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        user.Preferences.HomeLayout!.MutedTiles.Should().Equal("unread-notifications");
    }

    #endregion
}

/// <summary>
/// Extension to build a mock DbSet from an IQueryable for UserManager.Users property.
/// </summary>
internal static class MockDbSetExtensions
{
    public static Mock<Microsoft.EntityFrameworkCore.DbSet<T>> BuildMockDbSet<T>(this IQueryable<T> source) where T : class
    {
        var mockSet = new Mock<Microsoft.EntityFrameworkCore.DbSet<T>>();

        mockSet.As<IAsyncEnumerable<T>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new TestAsyncEnumerator<T>(source.GetEnumerator()));

        mockSet.As<IQueryable<T>>()
            .Setup(m => m.Provider)
            .Returns(new TestAsyncQueryProvider<T>(source.Provider));

        mockSet.As<IQueryable<T>>()
            .Setup(m => m.Expression)
            .Returns(source.Expression);

        mockSet.As<IQueryable<T>>()
            .Setup(m => m.ElementType)
            .Returns(source.ElementType);

        mockSet.As<IQueryable<T>>()
            .Setup(m => m.GetEnumerator())
            .Returns(source.GetEnumerator());

        return mockSet;
    }
}

internal class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
{
    private readonly IQueryProvider _inner;

    public TestAsyncQueryProvider(IQueryProvider inner) => _inner = inner;

    public IQueryable CreateQuery(System.Linq.Expressions.Expression expression)
        => new TestAsyncEnumerable<TEntity>(expression);

    public IQueryable<TElement> CreateQuery<TElement>(System.Linq.Expressions.Expression expression)
        => new TestAsyncEnumerable<TElement>(expression);

    public object? Execute(System.Linq.Expressions.Expression expression)
        => _inner.Execute(expression);

    public TResult Execute<TResult>(System.Linq.Expressions.Expression expression)
        => _inner.Execute<TResult>(expression);

    public TResult ExecuteAsync<TResult>(System.Linq.Expressions.Expression expression, CancellationToken cancellationToken = default)
    {
        var expectedResultType = typeof(TResult).GetGenericArguments()[0];
        var executionResult = typeof(IQueryProvider)
            .GetMethod(nameof(IQueryProvider.Execute), 1, [typeof(System.Linq.Expressions.Expression)])!
            .MakeGenericMethod(expectedResultType)
            .Invoke(_inner, [expression]);

        return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(expectedResultType)
            .Invoke(null, [executionResult])!;
    }
}

internal class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    public TestAsyncEnumerable(System.Linq.Expressions.Expression expression) : base(expression) { }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());

    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
}

internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IEnumerator<T> _inner;

    public TestAsyncEnumerator(IEnumerator<T> inner) => _inner = inner;

    public T Current => _inner.Current;

    public ValueTask DisposeAsync()
    {
        _inner.Dispose();
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(_inner.MoveNext());
}
