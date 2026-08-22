using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Asserts which /api/auth endpoints admit an anonymous caller, against the booted host rather than
/// the attributes. AuthController mixes both: a controller-level AllowAnonymous cannot be narrowed by
/// an action-level Authorize (ASP0026), so logout is only actually protected because AllowAnonymous is
/// applied per action. Nothing in a unit test or a plain build catches a regression to the
/// controller-level form — the endpoint would simply start answering anonymous callers.
///
/// Uses the SQL Server factory, not WaydApiFactory: the latter's UseInternalServiceProvider makes every
/// Identity-backed endpoint throw before its auth logic runs (see that factory's comment).
/// </summary>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class AuthEndpointAuthorizationTests(WaydSqlServerApiFactory factory)
{
    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task Logout_ReturnsUnauthorized_WhenCallerIsAnonymous()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/auth/logout", content: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_ReturnsUnauthorized_WhenBearerTokenIsNotValid()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "not.a.valid.token");

        // Act
        var response = await client.PostAsync("/api/auth/logout", content: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Providers_RemainsAnonymous()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/auth/providers", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_RemainsAnonymous()
    {
        // Arrange — refresh runs precisely when the access token is expired, so an anonymous caller
        // must reach the handler rather than be turned away by the auth pipeline.
        var client = _factory.CreateClient();
        var command = new
        {
            token = ForeignlySignedJwt(),
            refreshToken = "not-a-refresh-token",
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/refresh-token", command, TestContext.Current.CancellationToken);

        // Assert — asserts only that the pipeline let the call through. The status is deliberately not
        // pinned: TokenService.GetPrincipalFromExpiredToken lets SecurityTokenException escape, so an
        // untrusted token currently yields 500 (a pre-existing instance of the exception-middleware
        // finding, F9). Pinning 401 here would fail today and silently start passing if that is fixed.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(
            response.Headers.WwwAuthenticate.Any(),
            "a challenge header would mean the auth pipeline rejected the call rather than the handler.");
    }

    private static string ForeignlySignedJwt()
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("a-key-this-deployment-does-not-trust-0123456789"));

        var token = new JwtSecurityToken(
            issuer: "https://not.wayd.example",
            audience: "https://not.wayd.example",
            claims: [new Claim(ClaimTypes.NameIdentifier, "00000000-0000-0000-0000-000000000001")],
            expires: DateTime.UtcNow.AddMinutes(-5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
