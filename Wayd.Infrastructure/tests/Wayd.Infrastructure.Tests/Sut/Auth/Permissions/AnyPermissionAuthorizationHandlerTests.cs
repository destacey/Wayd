using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Domain.Authorization;
using Wayd.Infrastructure.Auth.Permissions;
using Wayd.Infrastructure.OpenTelemetry;

namespace Wayd.Infrastructure.Tests.Sut.Auth.Permissions;

public sealed class AnyPermissionAuthorizationHandlerTests
{
    private readonly Mock<IUserService> _userService = new();

    [Fact]
    public async Task HandleRequirementAsync_WhenAnyPermissionMatches_SucceedsAndRecordsSuccess()
    {
        // Arrange
        const string userId = "user-1";
        const string first = "Permissions.Projects.View";
        const string second = "Permissions.Projects.Update";
        _userService.Setup(s => s.HasPermissionAsync(userId, first, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _userService.Setup(s => s.HasPermissionAsync(userId, second, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var policy = $"{first},{second}";
        var seen = new List<string?>();
        using var listener = Listen(policy, seen);
        var context = Context(userId, new AnyPermissionRequirement([first, second]));

        // Act
        await new AnyPermissionAuthorizationHandler(_userService.Object).HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
        seen.Should().Equal("authz.check|success");
        _userService.Verify(s => s.HasPermissionAsync(userId, second, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenNoPermissionMatches_DeniesAndRecordsFailure()
    {
        // Arrange
        const string userId = "user-1";
        const string first = "Permissions.Programs.View";
        const string second = "Permissions.Programs.Update";
        _userService.Setup(s => s.HasPermissionAsync(userId, first, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _userService.Setup(s => s.HasPermissionAsync(userId, second, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var policy = $"{first},{second}";
        var seen = new List<string?>();
        using var listener = Listen(policy, seen);
        var context = Context(userId, new AnyPermissionRequirement([first, second]));

        // Act
        await new AnyPermissionAuthorizationHandler(_userService.Object).HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        seen.Should().Equal("authz.check|failure");
    }

    [Fact]
    public async Task HandleRequirementAsync_WhenUserIdMissing_DeniesWithoutCheckingPermissions()
    {
        // Arrange
        const string permission = "Permissions.Programs.Delete";
        var seen = new List<string?>();
        using var listener = Listen(permission, seen);
        var context = new AuthorizationHandlerContext(
            [new AnyPermissionRequirement([permission])],
            new ClaimsPrincipal(new ClaimsIdentity()),
            null);

        // Act
        await new AnyPermissionAuthorizationHandler(_userService.Object).HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
        seen.Should().Equal("authz.check|failure");
        _userService.Verify(
            s => s.HasPermissionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleRequirementAsync_WhenReadScoped_SucceedsOnlyThroughAPermissionTheScopeAllows(bool holdsView)
    {
        // Arrange — the user holds the write permission either way
        const string update = "Permissions.Projects.Update";
        const string view = "Permissions.Projects.View";
        _userService.Setup(s => s.HasPermissionAsync("user-1", update, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _userService.Setup(s => s.HasPermissionAsync("user-1", view, It.IsAny<CancellationToken>())).ReturnsAsync(holdsView);
        var context = ScopedContext("user-1", TokenScopes.Read, new AnyPermissionRequirement([update, view]));

        // Act
        await new AnyPermissionAuthorizationHandler(_userService.Object).HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().Be(holdsView);
        _userService.Verify(s => s.HasPermissionAsync("user-1", update, It.IsAny<CancellationToken>()), Times.Never);
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
