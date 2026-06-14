using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Events;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Identity.Roles;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Identity;
using Wayd.Infrastructure.Identity;
using Wayd.Tests.Shared;
using NotFoundException = Wayd.Common.Application.Exceptions.NotFoundException;

namespace Wayd.Infrastructure.Tests.Sut.Identity;

public class RoleServiceTests
{
    private readonly Mock<RoleManager<ApplicationRole>> _mockRoleManager;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<IOidcProviderDefaultRoleChecker> _mockDefaultRoleChecker;
    private readonly Mock<IEventPublisher> _mockEvents;
    private readonly Mock<ICurrentUser> _mockCurrentUser;
    private readonly Mock<ILogger<RoleService>> _mockLogger;
    private readonly TestingDateTimeProvider _dateTimeProvider;

    public RoleServiceTests()
    {
        var roleStore = new Mock<IRoleStore<ApplicationRole>>();
        _mockRoleManager = new Mock<RoleManager<ApplicationRole>>(
            roleStore.Object, null!, null!, null!, null!);

        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _mockDefaultRoleChecker = new Mock<IOidcProviderDefaultRoleChecker>();
        _mockEvents = new Mock<IEventPublisher>();
        _mockCurrentUser = new Mock<ICurrentUser>();
        _mockLogger = new Mock<ILogger<RoleService>>();
        _dateTimeProvider = new TestingDateTimeProvider(DateTime.UtcNow);
    }

    private RoleService CreateSut()
    {
        return new RoleService(
            _mockRoleManager.Object,
            _mockUserManager.Object,
            null!, // WaydDbContext - not used by Create/Delete methods
            _mockDefaultRoleChecker.Object,
            _mockCurrentUser.Object,
            _mockEvents.Object,
            _dateTimeProvider,
            _mockLogger.Object);
    }

    #region CreateOrUpdate - Create

    [Fact]
    public async Task CreateOrUpdate_ShouldCreateRole_WhenIdIsEmpty()
    {
        // Arrange
        var command = new CreateOrUpdateRoleCommand(null, "NewRole", "A new role");
        _mockRoleManager
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationRole>()))
            .ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        var result = await sut.CreateOrUpdate(command);

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
        _mockRoleManager.Verify(x => x.CreateAsync(It.Is<ApplicationRole>(r =>
            r.Name == "NewRole" && r.Description == "A new role")), Times.Once);
        _mockEvents.Verify(x => x.PublishAsync(It.IsAny<ApplicationRoleCreatedEvent>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrUpdate_ShouldThrow_WhenCreateFails()
    {
        // Arrange
        var command = new CreateOrUpdateRoleCommand(null, "NewRole", null);
        _mockRoleManager
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationRole>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Failed" }));

        var sut = CreateSut();

        // Act
        var act = () => sut.CreateOrUpdate(command);

        // Assert
        await act.Should().ThrowAsync<InternalServerException>();
    }

    [Fact]
    public async Task CreateOrUpdate_ShouldThrowValidation_WhenDuplicateRoleName()
    {
        // Arrange
        var command = new CreateOrUpdateRoleCommand(null, "Admin", null);
        _mockRoleManager
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationRole>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError
            {
                Code = "DuplicateRoleName",
                Description = "Role name 'Admin' is already taken."
            }));

        var sut = CreateSut();

        // Act
        var act = () => sut.CreateOrUpdate(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    #endregion

    #region CreateOrUpdate - Update

    [Fact]
    public async Task CreateOrUpdate_ShouldUpdateRole_WhenIdIsProvided()
    {
        // Arrange
        var existingRole = new ApplicationRole("OldName", "Old description");
        var command = new CreateOrUpdateRoleCommand("role-1", "UpdatedName", "Updated description");

        _mockRoleManager.Setup(x => x.FindByIdAsync("role-1")).ReturnsAsync(existingRole);
        _mockRoleManager.Setup(x => x.UpdateAsync(existingRole)).ReturnsAsync(IdentityResult.Success);

        var sut = CreateSut();

        // Act
        var result = await sut.CreateOrUpdate(command);

        // Assert
        result.Should().NotBeNullOrWhiteSpace();
        existingRole.Name.Should().Be("UpdatedName");
        existingRole.Description.Should().Be("Updated description");
        _mockRoleManager.Verify(x => x.UpdateAsync(existingRole), Times.Once);
        _mockEvents.Verify(x => x.PublishAsync(It.IsAny<ApplicationRoleUpdatedEvent>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrUpdate_ShouldThrowNotFound_WhenRoleDoesNotExist()
    {
        // Arrange
        _mockRoleManager.Setup(x => x.FindByIdAsync("missing")).ReturnsAsync((ApplicationRole?)null);
        var command = new CreateOrUpdateRoleCommand("missing", "Name", null);

        var sut = CreateSut();

        // Act
        var act = () => sut.CreateOrUpdate(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateOrUpdate_ShouldThrowConflict_WhenUpdatingDefaultRole()
    {
        // Arrange
        var adminRole = new ApplicationRole(ApplicationRoles.Admin);
        _mockRoleManager.Setup(x => x.FindByIdAsync("admin-id")).ReturnsAsync(adminRole);

        var command = new CreateOrUpdateRoleCommand("admin-id", "RenamedAdmin", null);
        var sut = CreateSut();

        // Act
        var act = () => sut.CreateOrUpdate(command);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Not allowed to modify*Admin*");
    }

    #endregion

    #region Delete

    [Fact]
    public async Task Delete_ShouldDeleteRole_WhenRoleExistsAndIsNotDefault()
    {
        // Arrange
        var role = new ApplicationRole("CustomRole", "A custom role") { Id = "role-1" };
        _mockRoleManager.Setup(x => x.FindByIdAsync("role-1")).ReturnsAsync(role);
        _mockUserManager.Setup(x => x.GetUsersInRoleAsync("CustomRole")).ReturnsAsync([]);
        _mockRoleManager.Setup(x => x.DeleteAsync(role)).ReturnsAsync(IdentityResult.Success);
        // No provider references this role as its default.
        _mockDefaultRoleChecker.Setup(x => x.CountProvidersUsingRole("role-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var sut = CreateSut();

        // Act
        var result = await sut.Delete("role-1");

        // Assert
        result.Should().Contain("CustomRole").And.Contain("Deleted");
        _mockRoleManager.Verify(x => x.DeleteAsync(role), Times.Once);
        _mockEvents.Verify(x => x.PublishAsync(It.IsAny<ApplicationRoleDeletedEvent>()), Times.Once);
    }

    [Fact]
    public async Task Delete_ShouldThrowConflict_WhenRoleIsAProviderDefault()
    {
        // Arrange — a provider pins this role as its auto-registration default.
        var role = new ApplicationRole("ProjectManager") { Id = "role-pm" };
        _mockRoleManager.Setup(x => x.FindByIdAsync("role-pm")).ReturnsAsync(role);
        _mockUserManager.Setup(x => x.GetUsersInRoleAsync("ProjectManager")).ReturnsAsync([]);
        _mockDefaultRoleChecker.Setup(x => x.CountProvidersUsingRole("role-pm", It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var sut = CreateSut();

        // Act
        var act = () => sut.Delete("role-pm");

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*default registration role*identity provider*");
        _mockRoleManager.Verify(x => x.DeleteAsync(It.IsAny<ApplicationRole>()), Times.Never);
    }

    [Fact]
    public async Task Delete_ShouldThrowNotFound_WhenRoleDoesNotExist()
    {
        // Arrange
        _mockRoleManager.Setup(x => x.FindByIdAsync("missing")).ReturnsAsync((ApplicationRole?)null);

        var sut = CreateSut();

        // Act
        var act = () => sut.Delete("missing");

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Delete_ShouldThrowConflict_WhenDeletingDefaultRole()
    {
        // Arrange
        var adminRole = new ApplicationRole(ApplicationRoles.Admin);
        _mockRoleManager.Setup(x => x.FindByIdAsync("admin-id")).ReturnsAsync(adminRole);

        var sut = CreateSut();

        // Act
        var act = () => sut.Delete("admin-id");

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Not allowed to delete*Admin*");
    }

    [Fact]
    public async Task Delete_ShouldThrowConflict_WhenRoleHasUsers()
    {
        // Arrange
        var role = new ApplicationRole("CustomRole");
        _mockRoleManager.Setup(x => x.FindByIdAsync("role-1")).ReturnsAsync(role);
        _mockUserManager.Setup(x => x.GetUsersInRoleAsync("CustomRole"))
            .ReturnsAsync([new() { Id = "user-1" }]);

        var sut = CreateSut();

        // Act
        var act = () => sut.Delete("role-1");

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*currently assigned to users*");
    }

    #endregion
}
