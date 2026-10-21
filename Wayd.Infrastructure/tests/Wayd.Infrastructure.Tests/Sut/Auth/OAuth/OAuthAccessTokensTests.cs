using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Wayd.Infrastructure.Auth.OAuth;

namespace Wayd.Infrastructure.Tests.Sut.Auth.OAuth;

/// <summary>
/// Holds the scheme choice for <c>/mcp</c> to the token's type: an OAuth access token goes to the scheme that accepts
/// only those, and anything else, including the web app's own JWTs, goes elsewhere.
/// </summary>
public sealed class OAuthAccessTokensTests
{
    [Theory]
    [InlineData("at+jwt", true)]
    [InlineData("AT+JWT", true)]
    [InlineData("JWT", false)]
    public void IsAccessToken_ReadsTheTokensType(string type, bool expected)
    {
        // Arrange
        var header = "Bearer " + Token(type);

        // Act
        var result = OAuthAccessTokens.IsAccessToken(header);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bearer not-a-jwt")]
    [InlineData("Basic dXNlcjpwYXNz")]
    public void IsAccessToken_AnswersFalse_ForAnythingButABearerJwt(string? header)
    {
        // Arrange

        // Act
        var result = OAuthAccessTokens.IsAccessToken(header);

        // Assert
        result.Should().BeFalse();
    }

    private static string Token(string type) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        TokenType = type,
        Issuer = "https://api.wayd.example.com",
        SigningCredentials = new SigningCredentials(
            new SymmetricSecurityKey(new byte[32]), SecurityAlgorithms.HmacSha256),
    });
}
