using Wayd.Infrastructure.Auth.OAuth;

namespace Wayd.Infrastructure.Tests.Sut.Auth.OAuth;

/// <summary>
/// A redirect URI is where Wayd sends an authorization code, so anything looser than an exact match is a way to steal
/// one. The single exception is the port of a loopback redirect, which a desktop client cannot know in advance.
/// </summary>
public sealed class RedirectUriMatchTests
{
    [Theory]
    [InlineData("http://127.0.0.1/callback", "http://127.0.0.1:53817/callback")]
    [InlineData("http://localhost/callback", "http://localhost:53817/callback")]
    [InlineData("http://[::1]/callback", "http://[::1]:53817/callback")]
    [InlineData("http://127.0.0.1:8080/callback", "http://127.0.0.1:53817/callback")]
    [InlineData("http://127.0.0.1/callback?x=1", "http://127.0.0.1:65535/callback?x=1")]
    [InlineData("http://127.0.0.1", "http://127.0.0.1:1")]
    [InlineData("http://127.0.0.1/callback", "http://127.0.0.1:00080/callback")]
    public void Matches_ALoopbackRedirectOnAnyPort(string registered, string requested)
    {
        // Arrange

        // Act
        var result = RedirectUriMatch.Matches(registered, requested);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://client.acme.example/callback")]
    [InlineData("http://127.0.0.1/callback")]
    [InlineData("com.acme.agent:/callback")]
    public void Matches_AnExactCopy(string uri)
    {
        // Arrange

        // Act
        var result = RedirectUriMatch.Matches(uri, uri);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://client.acme.example/callback", "https://client.acme.example:8443/callback", "a web redirect's port is exact")]
    [InlineData("http://127.0.0.1/callback", "https://127.0.0.1:53817/callback", "the scheme is exact")]
    [InlineData("http://127.0.0.1/callback", "http://localhost:53817/callback", "the loopback host is exact")]
    [InlineData("http://localhost/callback", "http://LOCALHOST:53817/callback", "the host's case is exact")]
    [InlineData("http://127.0.0.1/callback", "http://127.0.0.1:53817/other", "the path is exact")]
    [InlineData("http://127.0.0.1/callback", "http://127.0.0.1:53817/Callback", "the path's case is exact")]
    [InlineData("http://127.0.0.1/callback", "http://127.0.0.1:53817/callback?x=1", "the query is exact")]
    [InlineData("http://127.0.0.1/callback", "http://127.0.0.1:53817/./callback", "dot segments are not resolved")]
    [InlineData("http://127.0.0.1/callback", "http://127.0.0.1:0/callback", "port 0 is not a port")]
    [InlineData("http://127.0.0.1/callback", "http://127.0.0.1:65536/callback", "the port is at most 65535")]
    [InlineData("http://127.0.0.1/callback", "http://127.0.0.1:/callback", "an empty port is not a port")]
    [InlineData("http://127.0.0.1/callback", "http://127.0.0.1:+80/callback", "a port is plain digits")]
    [InlineData("http://127.0.0.1/callback", "http://127.0.0.1:80@evil.example/callback", "credentials are not a port")]
    [InlineData("http://127.0.0.1/callback", "http://127.0.0.1.evil.example:80/callback", "a lookalike host is not loopback")]
    [InlineData("http://127.0.0.2/callback", "http://127.0.0.2:53817/callback", "only the listed loopback hosts")]
    [InlineData("http://127.0.0.1:8080/callback", "http://127.0.0.1/callback", "a portless request matches only exactly")]
    [InlineData("http://127.0.0.1/callback", "HTTP://127.0.0.1:80/callback", "the scheme's case is exact")]
    [InlineData("http://127.0.0.1/callback", "http://127.0.0.1:80\\@evil.example/callback", "a backslash is not a port")]
    [InlineData("http://127.0.0.1/callback", "http://127.0.0.1:%38%30/callback", "a percent-encoded port is not a port")]
    [InlineData("http://127.0.0.1/callback", "http://127.0.0.1:８０/callback", "full-width digits are not a port")]
    [InlineData("http://[::1]/callback", "http://[::1:80/callback", "an unclosed IPv6 literal is not loopback")]
    [InlineData("http://[::1]/callback", "http://[0:0:0:0:0:0:0:1]:80/callback", "only the short IPv6 loopback form")]
    [InlineData("http://[::1]/callback", "http://[::1]:80:90/callback", "a second colon is not a port")]
    [InlineData("http://localhost/callback", "http://localhost.:80/callback", "a trailing dot is another host")]
    public void Refuses(string registered, string requested, string because)
    {
        // Arrange

        // Act
        var result = RedirectUriMatch.Matches(registered, requested);

        // Assert
        result.Should().BeFalse(because);
    }

    [Fact]
    public void IsRegistered_FindsTheMatchAmongSeveral()
    {
        // Arrange
        string[] registered = ["http://localhost/callback", "http://127.0.0.1/callback"];

        // Act
        var result = RedirectUriMatch.IsRegistered(registered, "http://127.0.0.1:53817/callback");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRegistered_RefusesWhenNoneMatch()
    {
        // Arrange
        string[] registered = ["https://client.acme.example/callback", "http://localhost/callback"];

        // Act
        var result = RedirectUriMatch.IsRegistered(registered, "http://127.0.0.1:53817/callback");

        // Assert
        result.Should().BeFalse();
    }
}
