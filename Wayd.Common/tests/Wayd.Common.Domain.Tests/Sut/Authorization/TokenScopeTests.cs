using Wayd.Common.Domain.Authorization;

namespace Wayd.Common.Domain.Tests.Sut.Authorization;

public sealed class TokenScopeTests
{
    [Theory]
    [InlineData("wayd:read", TokenScope.Read)]
    [InlineData("wayd:write", TokenScope.Write)]
    [InlineData("wayd:read wayd:write", TokenScope.Write)]
    [InlineData("wayd:write wayd:read", TokenScope.Write)]
    [InlineData("offline_access wayd:read", TokenScope.Read)]
    [InlineData("  wayd:read  ", TokenScope.Read)]
    public void Parse_AnswersTheWidestScopeNamed(string scopes, TokenScope expected)
    {
        // Act
        var result = TokenScopes.Parse(scopes);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("openid offline_access")]
    [InlineData("WAYD:READ")]
    [InlineData("wayd:admin")]
    public void Parse_AnswersNull_WhenNoScopeWaydDefinesIsNamed(string? scopes)
    {
        // Act
        var result = TokenScopes.Parse(scopes);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(TokenScope.Read, "wayd:read")]
    [InlineData(TokenScope.Write, "wayd:write")]
    public void ToScopeString_RoundTripsThroughParse(TokenScope scope, string expected)
    {
        // Act
        var result = scope.ToScopeString();

        // Assert
        result.Should().Be(expected);
        TokenScopes.Parse(result).Should().Be(scope);
    }

    [Theory]
    [InlineData("Permissions.Projects.View", true)]
    [InlineData("Permissions.Users.Search", true)]
    [InlineData("Permissions.Users.Export", true)]
    [InlineData("Permissions.Projects.Create", false)]
    [InlineData("Permissions.Projects.Update", false)]
    [InlineData("Permissions.Projects.Delete", false)]
    [InlineData("Permissions.ProjectPortfolioManagement.Administer", false)]
    [InlineData("Permissions.Imports.Manage", false)]
    [InlineData("Permissions.Projects.ViewOnly", false)]
    public void Permits_ForRead_AllowsOnlyViewSearchAndExport(string permission, bool expected)
    {
        // Act
        var result = TokenScope.Read.Permits(permission);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Permits_ForWrite_AllowsEveryPermissionInTheCatalogue()
    {
        // Act
        var refused = ApplicationPermissions.All.Where(p => !TokenScope.Write.Permits(p.Name)).ToList();

        // Assert
        refused.Should().BeEmpty();
    }

    [Fact]
    public void Permits_ForRead_AllowsExactlyTheCataloguePermissionsWhoseActionChangesNothing()
    {
        // Arrange — a new action is refused until it is deliberately added to the read set
        string[] readActions = [ApplicationAction.View, ApplicationAction.Search, ApplicationAction.Export];

        // Act
        var allowed = ApplicationPermissions.All.Where(p => TokenScope.Read.Permits(p.Name)).Select(p => p.Name).ToList();

        // Assert
        allowed.Should().BeEquivalentTo(ApplicationPermissions.All.Where(p => readActions.Contains(p.Action)).Select(p => p.Name));
        allowed.Should().NotBeEmpty();
    }
}
