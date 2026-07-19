using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Wayd.Common.Application.Identity;
using Wayd.Common.Application.Interfaces;
using Wayd.Infrastructure.Auth;

namespace Wayd.Infrastructure.Tests.Sut.Auth;

public sealed class CurrentUserTests
{
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();
    private readonly AmbientUserId _ambientUserId = new();

    private CurrentUser CreateSut() =>
        new(_httpContextAccessor.Object, _ambientUserId);

    private void GivenHttpUser(ClaimsPrincipal principal) =>
        _httpContextAccessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext { User = principal });

    private static ClaimsPrincipal AuthenticatedUser(string userId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], authenticationType: "Test"));

    [Fact]
    public void Kind_WithAuthenticatedHttpUser_IsUser()
    {
        // Arrange
        GivenHttpUser(AuthenticatedUser("user-1"));
        var sut = CreateSut();

        // Act
        var kind = sut.Kind;

        // Assert
        kind.Should().Be(ActorKind.User);
    }

    [Fact]
    public void Kind_WithUnauthenticatedHttpRequest_IsAnonymous()
    {
        // Arrange
        GivenHttpUser(new ClaimsPrincipal(new ClaimsIdentity()));
        var sut = CreateSut();

        // Act
        var kind = sut.Kind;

        // Assert
        kind.Should().Be(ActorKind.Anonymous);
    }

    [Fact]
    public void Kind_WithSeededAmbientUserAndNoHttpContext_IsUser()
    {
        // Arrange
        _ambientUserId.Set("job-user-1");
        var sut = CreateSut();

        // Act
        var kind = sut.Kind;

        // Assert
        kind.Should().Be(ActorKind.User);
    }

    [Fact]
    public void Kind_WithSystemSentinelSeeded_IsSystem()
    {
        // Arrange - a run-as-system job (HangfireService.EnqueueSystem) seeds the well-known system id
        // via its UserId job parameter; the scope must resolve to System, not User.
        _ambientUserId.Set(SystemIdentity.UserId);
        var sut = CreateSut();

        // Act
        var kind = sut.Kind;

        // Assert
        kind.Should().Be(ActorKind.System);
    }

    [Fact]
    public void Kind_WithNoHttpContextAndNoSeededUser_IsSystem()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var kind = sut.Kind;

        // Assert
        kind.Should().Be(ActorKind.System);
    }

    [Fact]
    public void GetUserId_WithAuthenticatedHttpUser_ReturnsClaimValue()
    {
        // Arrange
        GivenHttpUser(AuthenticatedUser("user-42"));
        var sut = CreateSut();

        // Act
        var userId = sut.GetUserId();

        // Assert
        userId.Should().Be("user-42");
    }

    [Fact]
    public void GetUserId_WithUnauthenticatedHttpRequest_IsEmpty()
    {
        // Arrange
        GivenHttpUser(new ClaimsPrincipal(new ClaimsIdentity()));
        var sut = CreateSut();

        // Act
        var userId = sut.GetUserId();

        // Assert
        userId.Should().BeEmpty();
    }

    [Fact]
    public void GetUserId_WithSeededAmbientUser_ReturnsSeededId()
    {
        // Arrange
        _ambientUserId.Set("job-user-7");
        var sut = CreateSut();

        // Act
        var userId = sut.GetUserId();

        // Assert
        userId.Should().Be("job-user-7");
    }

    [Fact]
    public void GetUserId_InSystemScope_ReturnsSystemSentinel()
    {
        // Arrange - no HTTP request, no seeded user: the platform is acting.
        var sut = CreateSut();

        // Act
        var userId = sut.GetUserId();

        // Assert - audit columns get an explicit attribution instead of an empty string.
        userId.Should().Be(SystemIdentity.UserId);
    }

    [Fact]
    public void Name_InSystemScope_ReturnsSystemName()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var name = sut.Name;

        // Assert
        name.Should().Be(SystemIdentity.Name);
    }
}
