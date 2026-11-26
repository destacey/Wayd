using Wayd.Infrastructure.Networking;

namespace Wayd.Infrastructure.Tests.Sut.Networking;

/// <summary>Holds which outbound proxy addresses startup accepts.</summary>
public sealed class OutboundProxyOptionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("http://proxy.corp.example:3128")]
    [InlineData("https://proxy.corp.example")]
    [InlineData("http://10.0.0.1:8080/")]
    public void IsValid_AcceptsNoProxyOrAnHttpProxyUrl(string? address)
    {
        // Arrange
        var options = new OutboundProxyOptions { Address = address is null ? null : new Uri(address) };

        // Act
        var result = options.IsValid();

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("socks5://proxy.corp.example:1080")]
    [InlineData("ftp://proxy.corp.example")]
    [InlineData("http://user:secret@proxy.corp.example:3128")]
    [InlineData("proxy.corp.example:3128")]
    public void IsValid_RefusesAnotherSchemeOrEmbeddedCredentials(string address)
    {
        // Arrange
        var options = new OutboundProxyOptions { Address = new Uri(address, UriKind.RelativeOrAbsolute) };

        // Act
        var result = options.IsValid();

        // Assert
        result.Should().BeFalse();
    }
}
