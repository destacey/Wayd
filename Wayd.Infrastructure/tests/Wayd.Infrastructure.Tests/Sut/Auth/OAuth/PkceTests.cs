using Wayd.Infrastructure.Auth.OAuth;

namespace Wayd.Infrastructure.Tests.Sut.Auth.OAuth;

/// <summary>Holds PKCE to RFC 7636's S256 method, against the RFC's own worked example (Appendix B).</summary>
public sealed class PkceTests
{
    private const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    private const string Challenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

    [Fact]
    public void Verify_AcceptsTheVerifierTheChallengeWasMadeFrom()
    {
        // Arrange — the values are RFC 7636 Appendix B's

        // Act
        var result = Pkce.Verify(Verifier, Challenge);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXK")]
    [InlineData(Challenge)]
    [InlineData(null)]
    public void Verify_RefusesAnyOtherVerifier(string? verifier)
    {
        // Arrange — a one-character change, the challenge itself (what "plain" would accept), and none

        // Act
        var result = Pkce.Verify(verifier, Challenge);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(42)]
    [InlineData(129)]
    public void Verify_RefusesAVerifierOutsideTheRfcsLength_EvenWhenItHashesToTheChallenge(int length)
    {
        // Arrange
        var verifier = new string('a', length);
        var challenge = System.Buffers.Text.Base64Url.EncodeToString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier)));

        // Act
        var result = Pkce.Verify(verifier, challenge);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(Challenge, true)]
    [InlineData("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-c", false)]
    [InlineData("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM=", false)]
    [InlineData("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw+cM", false)]
    [InlineData(null, false)]
    public void IsChallenge_AcceptsOnlyAnUnpaddedBase64UrlSha256(string? challenge, bool expected)
    {
        // Arrange — too short, padded, and standard rather than URL-safe base64

        // Act
        var result = Pkce.IsChallenge(challenge);

        // Assert
        result.Should().Be(expected);
    }
}
