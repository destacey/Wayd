using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Domain.Authorization;
using Wayd.Infrastructure.Auth.Permissions;
using Wayd.Infrastructure.OpenTelemetry;

namespace Wayd.Infrastructure.Tests.Sut.Auth.Permissions;

public sealed class PermissionAuthorizationHandlerTests
{
    private readonly Mock<IUserService> _userService = new();

    [Fact]
    public async Task HandleRequirementAsync_WhenUserHasPermission_SucceedsAndRecordsSuccess()
    {
        // Arrange
        const string userId = "user-1";
        const string permission = "Permissions.Projects.View";
        _userService
            .Setup(s => s.HasPermissionAsync(userId, permission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var seen = new List<string?>();
        using var listener = Listen(permission, seen);
        var context = Context(userId, new PermissionRequirement(permission));

        // Act
        await new PermissionAuthorizationHandler(_userService.Object).HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
        seen.Should().Equal("authz.check|success");
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenUserLacksPermission_DeniesAndRecordsFailure()
    {
        // Arrange
        const string userId = "user-1";
        const string permission = "Permissions.Projects.Update";
        _userService
            .Setup(s => s.HasPermissionAsync(userId, permission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var seen = new List<string?>();
        using var listener = Listen(permission, seen);
        var context = Context(userId, new PermissionRequirement(permission));

        // Act
        await new PermissionAuthorizationHandler(_userService.Object).HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        seen.Should().Equal("authz.check|failure");
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenUserIdMissing_DeniesWithoutCheckingPermissions()
    {
        // Arrange
        const string permission = "Permissions.Projects.Delete";
        var seen = new List<string?>();
        using var listener = Listen(permission, seen);
        var context = new AuthorizationHandlerContext(
            [new PermissionRequirement(permission)],
            new ClaimsPrincipal(new ClaimsIdentity()),
            null);

        // Act
        await new PermissionAuthorizationHandler(_userService.Object).HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        seen.Should().Equal("authz.check|failure");
        _userService.Verify(
            s => s.HasPermissionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenReadScopedTokenAsksForAWritePermission_DeniesWithoutCheckingPermissions()
    {
        // Arrange
        const string permission = "Permissions.Projects.Update";
        _userService
            .Setup(s => s.HasPermissionAsync("user-1", permission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var context = ScopedContext("user-1", TokenScopes.Read, new PermissionRequirement(permission));

        // Act
        await new PermissionAuthorizationHandler(_userService.Object).HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        _userService.Verify(
            s => s.HasPermissionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(TokenScopes.Read, "Permissions.Projects.View", true)]
    [InlineData(TokenScopes.Read, "Permissions.Projects.View", false)]
    [InlineData(TokenScopes.Write, "Permissions.Projects.Update", true)]
    [InlineData(TokenScopes.Write, "Permissions.Projects.Update", false)]
    public async Task HandleRequirementAsync_WhenTheScopeAllowsThePermission_DecidesByWhetherTheUserHoldsIt(string scope, string permission, bool holds)
    {
        // Arrange
        _userService
            .Setup(s => s.HasPermissionAsync("user-1", permission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(holds);
        var context = ScopedContext("user-1", scope, new PermissionRequirement(permission));

        // Act
        await new PermissionAuthorizationHandler(_userService.Object).HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().Be(holds);
    }

    private static AuthorizationHandlerContext Context(string userId, IAuthorizationRequirement requirement) =>
        new(
            [requirement],
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test")),
            null);

    private static ActivityListener Listen(string policy, List<string?> seen)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AuthTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.GetTagItem("aspnetcore.authorization.policy") is not string seenPolicy || seenPolicy != policy)
                    return;

                seen.Add($"{activity.DisplayName}|{activity.GetTagItem("aspnetcore.authorization.result")}");
            },
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static AuthorizationHandlerContext ScopedContext(string userId, string scope, IAuthorizationRequirement requirement) =>
        new(
            [requirement],
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(TokenScopes.ClaimType, scope)], "Test")),
            null);
}
