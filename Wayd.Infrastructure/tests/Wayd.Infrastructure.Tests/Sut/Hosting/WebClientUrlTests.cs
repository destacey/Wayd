using Wayd.Infrastructure.Hosting;

namespace Wayd.Infrastructure.Tests.Sut.Hosting;

/// <summary>Holds the configured web app URL to an origin the authorization endpoint can safely send a browser to.</summary>
public sealed class WebClientUrlTests
{
    [Theory]
    [InlineData("https://wayd.example.com/", "https://wayd.example.com")]
    [InlineData("http://localhost:3000", "http://localhost:3000")]
    public void Value_IsTheConfiguredOrigin(string configured, string expected)
    {
        // Arrange

        // Act
        var url = new WebClientUrl(configured);

        // Assert
        url.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void Value_IsNull_WhenNoneIsConfigured(string? configured)
    {
        // Arrange

        // Act
        var url = new WebClientUrl(configured);

        // Assert
        url.Value.Should().BeNull();
    }

    [Theory]
    [InlineData("http://wayd.example.com")]
    [InlineData("https://wayd.example.com/app")]
    [InlineData("wayd.example.com")]
    public void Constructor_RefusesAnythingButAnHttpsOrLoopbackOrigin(string configured)
    {
        // Arrange

        // Act
        var act = () => new WebClientUrl(configured);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage($"*'{WebClientUrl.ConfigurationKey}'*");
    }
}
