using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Events;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Identity;
using Wayd.Common.Application.Identity.OidcProviders;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Identity;
using Wayd.Infrastructure.Identity;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Data;
using NotFoundException = Wayd.Common.Application.Exceptions.NotFoundException;

namespace Wayd.Infrastructure.Tests.Sut.Identity;

public class UserServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
    private readonly Mock<RoleManager<ApplicationRole>> _mockRoleManager;
    private readonly Mock<IEventPublisher> _mockEvents;
    private readonly Mock<ILogger<UserService>> _mockLogger;
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly Mock<IDispatcher> _mockDispatcher;
    private readonly Mock<ICurrentUser> _mockCurrentUser;
    private readonly Mock<IUserIdentityStore> _mockUserIdentityStore;
    private readonly Mock<IOidcProviderRegistry> _mockOidcProviderRegistry;

    // UserService depends on WaydDbContext which is hard to mock. We test methods that
    // don't require it. UserIdentity writes go through IUserIdentityStore so they can be
    // verified via Moq.

    public UserServiceTests()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _mockSignInManager = new Mock<SignInManager<ApplicationUser>>(
            _mockUserManager.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>().Object,
            null!, null!, null!, null!);

        var roleStore = new Mock<IRoleStore<ApplicationRole>>();
        _mockRoleManager = new Mock<RoleManager<ApplicationRole>>(
            roleStore.Object, null!, null!, null!, null!);

        _mockEvents = new Mock<IEventPublisher>();
        _mockLogger = new Mock<ILogger<UserService>>();
        _dateTimeProvider = new TestingDateTimeProvider(DateTime.UtcNow);
        _mockDispatcher = new Mock<IDispatcher>();
        _mockCurrentUser = new Mock<ICurrentUser>();
        _mockCurrentUser.Setup(x => x.GetUserId()).Returns("current-user-id");
        _mockUserIdentityStore = new Mock<IUserIdentityStore>();
        _mockOidcProviderRegistry = new Mock<IOidcProviderRegistry>();

        // Pass-through: tests don't exercise transaction semantics. Invoke the
        // action directly so CreateAsync behaves as if the transaction succeeded.
        _mockUserIdentityStore
            .Setup(s => s.ExecuteInTransaction(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>((action, ct) => action(ct));
    }

    private UserService CreateSut()
    {
        return new UserService(
            _mockLogger.Object,
            _mockSignInManager.Object,
            _mockUserManager.Object,
            _mockRoleManager.Object,
            null!, // WaydDbContext - not used by these methods
            _mockEvents.Object,
            _mockDispatcher.Object,
            _dateTimeProvider,
            _mockCurrentUser.Object,
            _mockUserIdentityStore.Object,
            _mockOidcProviderRegistry.Object);
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
        result.Value.Should().NotBeNullOrWhiteSpace();
        _mockUserManager.Verify(x => x.CreateAsync(It.Is<ApplicationUser>(u =>
            u.FirstName == "John" &&
            u.LastName == "Doe" &&
            u.Email == "john@example.com" &&
            u.UserName == "john@example.com" &&
            u.LoginProvider == LoginProviders.Wayd &&
            u.IsActive), "Password123!"), Times.Once);
        _mockUserManager.Verify(x => x.AddToRolesAsync(It.IsAny<ApplicationUser>(),
            It.Is<IEnumerable<string>>(r => r.SequenceEqual(new[] { "Contributor" }))), Times.Once);
        _mockEvents.Verify(x => x.PublishAsync(It.IsAny<ApplicationUserCreatedEvent>()), Times.Once);
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

        _mockUserManager
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), "Password123!"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Duplicate username." }));

        var sut = CreateSut();

        // Act
        var result = await sut.CreateAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Duplicate username.");
        _mockUserManager.Verify(x => x.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()), Times.Never);
        _mockEvents.Verify(x => x.PublishAsync(It.IsAny<ApplicationUserCreatedEvent>()), Times.Never);
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

        _mockUserManager
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), "Password123!"))
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
        _mockEvents.Verify(x => x.PublishAsync(It.IsAny<ApplicationUserCreatedEvent>()), Times.Never);
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
        // Entra users created by an admin have no oid/tid yet — the identity row
        // is only written on their first SSO login via the principal flow.
        var command = new CreateUserCommand
        {
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com",
            LoginProvider = LoginProviders.MicrosoftEntraId,
        };

        _mockUserManager
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager
            .Setup(x => x.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        await sut.CreateAsync(command, TestContext.Current.CancellationToken);

        _mockUserIdentityStore.Verify(s => s.Add(It.IsAny<UserIdentity>(), It.IsAny<CancellationToken>()), Times.Never);
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

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_ShouldUpdateUser_WhenUserExists()
    {
        // Arrange
        var user = CreateUser();
        var command = new UpdateUserCommand { Id = "user-1", FirstName = "Updated", LastName = "Name", Email = "updated@example.com", PhoneNumber = "555-1234" };

        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.GetPhoneNumberAsync(user)).ReturnsAsync((string?)null);
        _mockUserManager.Setup(x => x.SetPhoneNumberAsync(user, "555-1234")).ReturnsAsync(IdentityResult.Success);
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
        _mockEvents.Verify(x => x.PublishAsync(It.IsAny<ApplicationUserUpdatedEvent>()), Times.Once);
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
    public async Task UpdateAsync_ShouldNotSetPhone_WhenPhoneNumberUnchanged()
    {
        // Arrange
        var user = CreateUser();
        var command = new UpdateUserCommand { Id = "user-1", FirstName = "Test", LastName = "User", Email = "test@example.com", PhoneNumber = "555-1234" };

        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.GetPhoneNumberAsync(user)).ReturnsAsync("555-1234");
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        await sut.UpdateAsync(command, "user-1");

        // Assert
        _mockUserManager.Verify(x => x.SetPhoneNumberAsync(user, It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrow_WhenUpdateFails()
    {
        // Arrange
        var user = CreateUser();
        var command = new UpdateUserCommand { Id = "user-1", FirstName = "Test", LastName = "User", Email = "test@example.com" };

        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.GetPhoneNumberAsync(user)).ReturnsAsync((string?)null);
        _mockUserManager.Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Update failed." }));

        var sut = CreateSut();

        // Act
        var act = () => sut.UpdateAsync(command, "user-1");

        // Assert
        await act.Should().ThrowAsync<InternalServerException>();
    }

    [Fact]
    public async Task UpdateAsync_ShouldPreserveEmployeeLink_WhenUpdateDoesNotManageIt()
    {
        // Arrange — the self-service profile edit sends no employee id. Applying the command's
        // default null would silently unlink the user from their employee record on every save.
        var employeeId = Guid.NewGuid();
        var user = CreateUser();
        user.EmployeeId = employeeId;
        var command = new UpdateUserCommand { Id = "user-1", FirstName = "Test", LastName = "User", Email = "test@example.com" };

        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.GetPhoneNumberAsync(user)).ReturnsAsync((string?)null);
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
        _mockUserManager.Setup(x => x.GetPhoneNumberAsync(user)).ReturnsAsync((string?)null);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        await sut.UpdateAsync(command, "user-1");

        // Assert
        user.EmployeeId.Should().Be(employeeId);
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
        _mockUserManager.Setup(x => x.GetPhoneNumberAsync(user)).ReturnsAsync((string?)null);
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
        _mockEvents.Verify(x => x.PublishAsync(It.IsAny<ApplicationUserActivatedEvent>()), Times.Once);
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
        _mockEvents.Verify(x => x.PublishAsync(It.IsAny<ApplicationUserDeactivatedEvent>()), Times.Once);
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
        _mockEvents.Verify(x => x.PublishAsync(It.IsAny<ApplicationUserDeactivatedEvent>()), Times.Once);
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
        _mockEvents.Verify(x => x.PublishAsync(It.IsAny<ApplicationUserUpdatedEvent>()), Times.Once);
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
            .WithMessage("*at least 1 Admin*");
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
        var result = await sut.ChangePasswordAsync("user-1", command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockUserManager.Verify(x => x.ChangePasswordAsync(user, "OldPass123!", "NewPass456!"), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldClearMustChangePassword_WhenFlagIsSet()
    {
        // Arrange
        var user = CreateUser(loginProvider: LoginProviders.Wayd);
        user.MustChangePassword = true;
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.ChangePasswordAsync(user, "OldPass123!", "NewPass456!"))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var command = new ChangePasswordCommand("OldPass123!", "NewPass456!");
        var sut = CreateSut();

        // Act
        var result = await sut.ChangePasswordAsync("user-1", command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.MustChangePassword.Should().BeFalse();
        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
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
        var result = await sut.ChangePasswordAsync("user-1", command);

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
        var result = await sut.ChangePasswordAsync("user-1", command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Incorrect password.");
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldThrowNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        _mockUserManager.Setup(x => x.FindByIdAsync("missing")).ReturnsAsync((ApplicationUser?)null);

        var command = new ChangePasswordCommand("OldPass123!", "NewPass456!");
        var sut = CreateSut();

        // Act
        var act = () => sut.ChangePasswordAsync("missing", command);

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
        _mockUserManager.Setup(x => x.ResetPasswordAsync(user, "reset-token", "NewPass456!"))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);

        var command = new ResetPasswordCommand("user-1", "NewPass456!");
        var sut = CreateSut();

        // Act
        var result = await sut.ResetPasswordAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.MustChangePassword.Should().BeTrue();
        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldClearLockout_WhenUserIsLockedOut()
    {
        // Arrange
        var user = CreateUser(loginProvider: LoginProviders.Wayd);
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-token");
        _mockUserManager.Setup(x => x.ResetPasswordAsync(user, "reset-token", "NewPass456!"))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(true);
        _mockUserManager.Setup(x => x.SetLockoutEndDateAsync(user, null)).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.ResetAccessFailedCountAsync(user)).ReturnsAsync(IdentityResult.Success);

        var command = new ResetPasswordCommand("user-1", "NewPass456!");
        var sut = CreateSut();

        // Act
        var result = await sut.ResetPasswordAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockUserManager.Verify(x => x.SetLockoutEndDateAsync(user, null), Times.Once);
        _mockUserManager.Verify(x => x.ResetAccessFailedCountAsync(user), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldNotClearLockout_WhenUserIsNotLockedOut()
    {
        // Arrange
        var user = CreateUser(loginProvider: LoginProviders.Wayd);
        _mockUserManager.Setup(x => x.FindByIdAsync("user-1")).ReturnsAsync(user);
        _mockUserManager.Setup(x => x.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("reset-token");
        _mockUserManager.Setup(x => x.ResetPasswordAsync(user, "reset-token", "NewPass456!"))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);

        var command = new ResetPasswordCommand("user-1", "NewPass456!");
        var sut = CreateSut();

        // Act
        var result = await sut.ResetPasswordAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockUserManager.Verify(x => x.SetLockoutEndDateAsync(It.IsAny<ApplicationUser>(), It.IsAny<DateTimeOffset?>()), Times.Never);
        _mockUserManager.Verify(x => x.ResetAccessFailedCountAsync(It.IsAny<ApplicationUser>()), Times.Never);
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
        var result = await sut.ResetPasswordAsync(command);

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
        var result = await sut.ResetPasswordAsync(command);

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
        var act = () => sut.ResetPasswordAsync(command);

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
        user.PendingMigrationTenantId = Guid.NewGuid().ToString();
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
        _mockEvents.Verify(x => x.PublishAsync(It.IsAny<ApplicationUserUpdatedEvent>()), Times.Once);
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
        _mockEvents.Verify(x => x.PublishAsync(It.IsAny<ApplicationUserUpdatedEvent>()), Times.Never);
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

        var sut = CreateSut();

        var (resolvedId, _) = await sut.GetOrCreateFromPrincipalAsync(
            CreateEntraPrincipal(newObjectId, newTenantId, upn));

        resolvedId.Should().Be(user.Id);
        user.PendingMigrationTenantId.Should().BeNull();
        // StagedAt moves together with the tenant flag — both clear on a completed rebind.
        user.PendingMigrationStagedAt.Should().BeNull();

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
            .WithMessage("*Failed to clear pending migration flag*Concurrency failure*");

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

        var sut = CreateSut();

        // Act
        var (resolvedId, _) = await sut.GetOrCreateFromPrincipalAsync(
            CreateNewUserPrincipal(objectId, tenantId, upn, displayName: upn));

        // Assert — same user Id preserved, LoginProvider flipped, flag cleared, and the
        // new active identity carries this token's tenant/subject.
        resolvedId.Should().Be(user.Id);
        user.LoginProvider.Should().Be(LoginProviders.MicrosoftEntraId);
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
        _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
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
        _mockEvents.Verify(x => x.PublishAsync(It.IsAny<ApplicationUserUpdatedEvent>()), Times.Once);
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

        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        var result = await sut.CancelProviderMigration(user.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        user.PendingMigrationProviderId.Should().BeNull();
        _mockUserManager.Verify(x => x.UpdateAsync(user), Times.Once);
        _mockEvents.Verify(x => x.PublishAsync(It.IsAny<ApplicationUserUpdatedEvent>()), Times.Once);
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
        _mockEvents.Verify(x => x.PublishAsync(It.IsAny<ApplicationUserUpdatedEvent>()), Times.Never);
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

        _mockUserManager.Setup(x => x.Users).Returns(new[] { user }.AsQueryable().BuildMockDbSet().Object);
        _mockUserManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        var result = await sut.TryApplyPendingProviderMigration(targetProvider, null, subject, email);

        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
        user.LoginProvider.Should().Be(targetProvider);
        user.PendingMigrationProviderId.Should().BeNull();

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
            .WithMessage("*Failed to apply pending provider migration*Concurrency failure*");

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

        var sut = CreateSut();

        var result = await sut.ConvertToLocalAccount(
            new ConvertToLocalAccountCommand(user.Id, "NewPass123!"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        user.LoginProvider.Should().Be(LoginProviders.Wayd);
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

        _mockEvents.Verify(x => x.PublishAsync(It.IsAny<ApplicationUserUpdatedEvent>()), Times.Once);
    }

    [Fact]
    public async Task ConvertToLocalAccount_ShouldClearPendingProviderMigration_WhenFlagIsSet()
    {
        var user = CreateUser(loginProvider: LoginProviders.MicrosoftEntraId);
        user.PendingMigrationProviderId = "Acme-Okta";

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
        _mockUserManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(true);
        _mockUserManager.Setup(x => x.SetLockoutEndDateAsync(user, null)).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(x => x.ResetAccessFailedCountAsync(user)).ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        var result = await sut.UnlockUserAsync("user-1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockUserManager.Verify(x => x.SetLockoutEndDateAsync(user, null), Times.Once);
        _mockUserManager.Verify(x => x.ResetAccessFailedCountAsync(user), Times.Once);
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
        _mockUserManager.Verify(x => x.SetLockoutEndDateAsync(It.IsAny<ApplicationUser>(), It.IsAny<DateTimeOffset?>()), Times.Never);
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
