using System.Security.Claims;
using Wayd.Common.Domain.Authorization;
using Wayd.Infrastructure.Auth;

namespace Wayd.Infrastructure.Tests.Sut.Auth;

public sealed class TokenScopeAccessTests
{
    private const string View = "Permissions.Projects.View";
    private const string Update = "Permissions.Projects.Update";

    [Theory]
    [InlineData(View)]
    [InlineData(Update)]
    public void Permits_WithoutAScopeClaim_AllowsEveryPermission(string permission)
    {
        // Arrange — a web app sign-in carries no scope
        var principal = Principal();

        // Act
        var result = TokenScopeAccess.Permits(principal, permission);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(TokenScopes.Read, View, true)]
    [InlineData(TokenScopes.Read, Update, false)]
    [InlineData(TokenScopes.Write, View, true)]
    [InlineData(TokenScopes.Write, Update, true)]
    public void Permits_WithAScopeClaim_AppliesTheScope(string scope, string permission, bool expected)
    {
        // Arrange
        var principal = Principal(scope);

        // Act
        var result = TokenScopeAccess.Permits(principal, permission);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Permits_WithAScopeClaimNamingNothingWaydDefines_AllowsNothing()
    {
        // Arrange
        var principal = Principal("openid");

        // Act
        var result = TokenScopeAccess.Permits(principal, View);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Permits_WithScopesOnTwoIdentities_AppliesTheNarrowest()
    {
        // Arrange
        var principal = Principal(TokenScopes.Write);
        principal.AddIdentity(new ClaimsIdentity([new Claim(TokenScopes.ClaimType, TokenScopes.Read)], "Other"));

        // Act
        var result = TokenScopeAccess.Permits(principal, Update);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Permits_IgnoresAScopeOnAnUnauthenticatedIdentity()
    {
        // Arrange
        var principal = Principal();
        principal.AddIdentity(new ClaimsIdentity([new Claim(TokenScopes.ClaimType, TokenScopes.Read)]));

        // Act
        var result = TokenScopeAccess.Permits(principal, Update);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(TokenScopes.Read, true)]
    [InlineData(TokenScopes.Write, false)]
    [InlineData("openid", true)]
    public void IsReadOnly_AnswersWhetherTheTokenMayChangeNothing(string? scope, bool expected)
    {
        // Arrange
        var principal = scope is null ? Principal() : Principal(scope);

        // Act
        var result = TokenScopeAccess.IsReadOnly(principal);

        // Assert
        result.Should().Be(expected);
    }

    private static ClaimsPrincipal Principal(params string[] scopes) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-1"), .. scopes.Select(s => new Claim(TokenScopes.ClaimType, s))],
            "Test"));
}
