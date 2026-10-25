using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NodaTime;
using Wayd.Common.Domain.Authorization;
using Wayd.Infrastructure.Auth.OAuth;

namespace Wayd.Infrastructure.Tests.Sut.Auth.OAuth;

/// <summary>
/// The token a tool call carries through the API in place of the caller's OAuth access token: it must name the user
/// and the acting client in RFC 8693's terms, be typed and addressed so only the delegated scheme accepts it, and live
/// only long enough for one request.
/// </summary>
public sealed class DelegatedAccessTokensTests
{
    private const string Secret = "a-test-secret-that-is-long-enough-for-hmac-sha256";
    private const string Audience = "https://api.wayd.example";
    private const string Issuer = "https://api.wayd.example.com";
    private const string ClientId = "https://agent.acme.example/client-metadata.json";

    private static readonly Instant _now = Instant.FromUtc(2026, 10, 20, 9, 0);

    private static readonly IConfiguration _config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SecuritySettings:LocalJwt:Secret"] = Secret,
            ["SecuritySettings:LocalJwt:Audience"] = Audience,
        })
        .Build();

    [Fact]
    public void Issue_NamesTheUserAsSubjectAndTheClientAsActor()
    {
        // Arrange

        // Act
        var token = new JsonWebToken(DelegatedAccessTokens.Issue(_config, Issuer, "user-1", ClientId, TokenScope.Write, _now));

        // Assert
        token.Subject.Should().Be("user-1");
        var actor = token.GetPayloadValue<JsonElement>(DelegatedAccessTokens.ActorClaim);
        actor.GetProperty("client_id").GetString().Should().Be(ClientId);
        token.TryGetPayloadValue<string>(OAuthAccessTokens.ClientIdClaim, out _).Should().BeFalse(
            "the token was requested by Wayd itself, so the client belongs in act rather than client_id");
    }

    [Fact]
    public void Issue_IsAnAccessTokenFromTheIssuerForTheApi()
    {
        // Arrange

        // Act
        var token = new JsonWebToken(DelegatedAccessTokens.Issue(_config, Issuer, "user-1", ClientId, TokenScope.Write, _now));

        // Assert
        token.Typ.Should().Be(OAuthAccessTokens.TokenType);
        token.Issuer.Should().Be(Issuer);
        token.Audiences.Should().Equal(Audience);
    }

    [Fact]
    public void Issue_LivesSixtySecondsFromNow()
    {
        // Arrange

        // Act
        var token = new JsonWebToken(DelegatedAccessTokens.Issue(_config, Issuer, "user-1", ClientId, TokenScope.Write, _now));

        // Assert
        token.IssuedAt.Should().Be(_now.ToDateTimeUtc());
        token.ValidFrom.Should().Be(_now.ToDateTimeUtc());
        token.ValidTo.Should().Be(_now.Plus(Duration.FromSeconds(60)).ToDateTimeUtc());
    }

    [Fact]
    public void ActingClientId_ReadsTheClientFromAnIssuedToken()
    {
        // Arrange
        var token = new JsonWebToken(DelegatedAccessTokens.Issue(_config, Issuer, "user-1", ClientId, TokenScope.Write, _now));

        // Act
        var clientId = DelegatedAccessTokens.ActingClientId(token);

        // Assert
        clientId.Should().Be(ClientId);
    }

    public static TheoryData<string, object?> TokensNamingNoClient => new()
    {
        { "no act claim", null },
        { "act is a string", ClientId },
        { "act has no client_id", new Dictionary<string, object> { ["sub"] = "someone" } },
        { "client_id is blank", new Dictionary<string, object> { [OAuthAccessTokens.ClientIdClaim] = " " } },
        { "client_id is not a string", new Dictionary<string, object> { [OAuthAccessTokens.ClientIdClaim] = 7 } },
    };

    [Theory]
    [MemberData(nameof(TokensNamingNoClient))]
    public void ActingClientId_IsNull_ForATokenNamingNoClient(string because, object? actor)
    {
        // Arrange
        var claims = new Dictionary<string, object>();
        if (actor is not null)
            claims[DelegatedAccessTokens.ActorClaim] = actor;
        var token = new JsonWebToken(new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim("sub", "user-1")]),
            Claims = claims,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256),
        }));

        // Act
        var clientId = DelegatedAccessTokens.ActingClientId(token);

        // Assert
        clientId.Should().BeNull(because);
    }

    [Fact]
    public async Task Issue_IsSignedWithTheWebAppsKey()
    {
        // Arrange
        var issued = DelegatedAccessTokens.Issue(_config, Issuer, "user-1", ClientId, TokenScope.Write, _now);

        // Act
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(issued, new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(Secret)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        });

        // Assert
        result.IsValid.Should().BeTrue(result.Exception?.Message);
    }
}
