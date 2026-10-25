using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Wayd.Common.Domain.Authorization;
using Wayd.Infrastructure.Auth.Permissions;

namespace Wayd.Infrastructure.Tests.Sut.Auth.Permissions;

public sealed class TokenScopeAuthorizationHandlerTests
{
    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task HandleRequirementAsync_ForAReadOnlyTokenOnASafeMethod_Succeeds(string method)
    {
        // Arrange
        var context = Context(TokenScopes.Read, method);

        // Act
        await new TokenScopeAuthorizationHandler().HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task HandleRequirementAsync_ForAReadOnlyTokenWritingToAnEndpointGatedBySignInAlone_Fails(string method)
    {
        // Arrange
        var context = Context(TokenScopes.Read, method, new AuthorizeAttribute());

        // Act
        await new TokenScopeAuthorizationHandler().HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_ForAReadOnlyTokenWritingToAnEndpointGatedByAViewPermission_Fails()
    {
        // Arrange — an endpoint can change a record under its view permission, as an intake request's update does
        var context = Context(TokenScopes.Read, "PUT",
            new AuthorizeAttribute(), new MustHavePermissionAttribute(ApplicationAction.View, ApplicationResource.IntakeRequests));

        // Act
        await new TokenScopeAuthorizationHandler().HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_ForAReadOnlyTokenPostingToAnActionMarkedAsChangingNothing_Succeeds()
    {
        // Arrange
        var context = Context(TokenScopes.Read, "POST",
            new MustHavePermissionAttribute(ApplicationAction.View, ApplicationResource.ScoringModels), new ReadOnlyOperationAttribute());

        // Act
        await new TokenScopeAuthorizationHandler().HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(TokenScopes.Read, false)]
    [InlineData(TokenScopes.Write, true)]
    [InlineData(null, true)]
    public async Task HandleRequirementAsync_ForAHubMethodCall_SucceedsOnlyForATokenThatMayWrite(string? scope, bool expected)
    {
        // Arrange — SignalR authorizes a hub method with the invocation as the resource, not a request
        var context = new AuthorizationHandlerContext([TokenScopeRequirement.Instance], Principal(scope), new object());

        // Act
        await new TokenScopeAuthorizationHandler().HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().Be(expected);
    }

    [Theory]
    [InlineData(TokenScopes.Write)]
    [InlineData(null)]
    public async Task HandleRequirementAsync_ForATokenThatMayWrite_Succeeds(string? scope)
    {
        // Arrange
        var context = Context(scope, "DELETE", new AuthorizeAttribute());

        // Act
        await new TokenScopeAuthorizationHandler().HandleAsync(context);

        // Assert
        context.HasSucceeded.Should().BeTrue();
    }

    private static AuthorizationHandlerContext Context(string? scope, string method, params object[] endpointMetadata)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        httpContext.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(endpointMetadata), "test"));

        return new AuthorizationHandlerContext([TokenScopeRequirement.Instance], Principal(scope), httpContext);
    }

    private static ClaimsPrincipal Principal(string? scope)
    {
        List<Claim> claims = [new(ClaimTypes.NameIdentifier, "user-1")];
        if (scope is not null)
            claims.Add(new(TokenScopes.ClaimType, scope));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
}
