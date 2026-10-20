using System.Net;
using Wayd.Infrastructure.Auth.OAuth;

namespace Wayd.Infrastructure.Tests.Sut.Auth.OAuth;

/// <summary>
/// Holds the outbound guard for client-chosen URLs to the public internet: the addresses a server-side request
/// forgery would aim at are refused, both as a classification and when a connection is actually made.
/// </summary>
public sealed class PublicNetworkTests
{
    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("::ffff:8.8.8.8")]
    public void IsPublic_AcceptsAPubliclyRoutableAddress(string address)
    {
        // Arrange
        var ip = IPAddress.Parse(address);

        // Act
        var result = PublicNetwork.IsPublic(ip);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    [InlineData("ff02::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("64:ff9b::a00:1")]
    [InlineData("2002:a00:1::")]
    [InlineData("::a00:1")]
    [InlineData("fec0::1")]
    [InlineData("3fff::1")]
    public void IsPublic_RefusesAnAddressThatIsNotPubliclyRoutable(string address)
    {
        // Arrange
        var ip = IPAddress.Parse(address);

        // Act
        var result = PublicNetwork.IsPublic(ip);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("https://localhost:1/client.json")]
    [InlineData("https://127.0.0.1:1/client.json")]
    [InlineData("https://[::1]:1/client.json")]
    public async Task CreateHandler_RefusesToConnectToANonPublicAddress(string url)
    {
        // Arrange
        using var client = new HttpClient(PublicNetwork.CreateHandler(TimeSpan.FromSeconds(5)));

        // Act
        var send = () => client.GetAsync(url, TestContext.Current.CancellationToken);

        // Assert
        (await send.Should().ThrowAsync<HttpRequestException>())
            .Which.ToString().Should().Contain("does not resolve to a public address");
    }

    [Theory]
    [InlineData("10.0.0.1")]
    [InlineData("8.8.8.8", "10.0.0.1")]
    [InlineData("8.8.8.8", "::1")]
    public async Task CreateHandler_RefusesAHostName_WhenAnyAddressItResolvesToIsNotPublic(params string[] resolvedTo)
    {
        // Arrange — the socket may try any resolved address, so one private address among public ones is enough
        var addresses = resolvedTo.Select(IPAddress.Parse).ToArray();
        using var client = new HttpClient(PublicNetwork.CreateHandler(TimeSpan.FromSeconds(5), (_, _) => Task.FromResult(addresses)));

        // Act
        var send = () => client.GetAsync("https://client.acme.example/client.json", TestContext.Current.CancellationToken);

        // Assert
        (await send.Should().ThrowAsync<HttpRequestException>())
            .Which.ToString().Should().Contain("'client.acme.example' does not resolve to a public address");
    }
}
