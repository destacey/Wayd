using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Application.Interfaces;
using Wayd.Infrastructure.Auth;

namespace Wayd.Infrastructure.Tests.Sut.Auth;

public sealed class CurrentPrincipalTests
{
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IUserService> _userService = new();

    private CurrentPrincipal CreateSut() =>
        new(_currentUser.Object, _userService.Object);

    private void GivenUserWithPermissions(string userId, params string[] permissions)
    {
        _currentUser.Setup(u => u.Kind).Returns(ActorKind.User);
        _currentUser.Setup(u => u.GetUserId()).Returns(userId);
        _userService
            .Setup(s => s.GetPermissionsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. permissions]);
    }

    [Fact]
    public async Task HasPermission_ForSystemActor_GrantsWithoutStoreLookup()
    {
        // Arrange
        _currentUser.Setup(u => u.Kind).Returns(ActorKind.System);
        var sut = CreateSut();

        // Act
        var result = await sut.HasPermission("Permissions.Projects.View", TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeTrue();
        _userService.Verify(s => s.GetPermissionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HasPermission_ForAnonymousActor_DeniesWithoutStoreLookup()
    {
        // Arrange
        _currentUser.Setup(u => u.Kind).Returns(ActorKind.Anonymous);
        var sut = CreateSut();

        // Act
        var result = await sut.HasPermission("Permissions.Projects.View", TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeFalse();
        _userService.Verify(s => s.GetPermissionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HasPermission_ForUserHoldingPermission_ReturnsTrue()
    {
        // Arrange
        GivenUserWithPermissions("user-1", "Permissions.Projects.View");
        var sut = CreateSut();

        // Act
        var result = await sut.HasPermission("Permissions.Projects.View", TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasPermission_ForUserWithoutPermission_ReturnsFalse()
    {
        // Arrange
        GivenUserWithPermissions("user-1", "Permissions.Projects.View");
        var sut = CreateSut();

        // Act
        var result = await sut.HasPermission("Permissions.Projects.Delete", TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasPermission_CalledTwice_QueriesTheStoreOnce()
    {
        // Arrange
        GivenUserWithPermissions("user-1", "Permissions.Projects.View");
        var sut = CreateSut();

        // Act
        await sut.HasPermission("Permissions.Projects.View", TestContext.Current.CancellationToken);
        await sut.HasPermission("Permissions.Projects.Delete", TestContext.Current.CancellationToken);

        // Assert - the permission set is cached for the scope after the first check.
        _userService.Verify(s => s.GetPermissionsAsync("user-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HasAnyPermission_WhenOneOfSeveralHeld_ReturnsTrue()
    {
        // Arrange
        GivenUserWithPermissions("user-1", "Permissions.Teams.View");
        var sut = CreateSut();

        // Act
        var result = await sut.HasAnyPermission(
            ["Permissions.Projects.View", "Permissions.Teams.View"],
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasAnyPermission_WhenNoneHeld_ReturnsFalse()
    {
        // Arrange
        GivenUserWithPermissions("user-1", "Permissions.Teams.View");
        var sut = CreateSut();

        // Act
        var result = await sut.HasAnyPermission(
            ["Permissions.Projects.View", "Permissions.Projects.Delete"],
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeFalse();
    }
}
