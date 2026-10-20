using Microsoft.AspNetCore.Http;
using Wayd.Infrastructure.Hosting;

namespace Wayd.Infrastructure.Tests.Sut.Hosting;

/// <summary>
/// Holds the configured public URL to the one form Wayd can publish in a header and as an OAuth issuer, and the
/// request's URL to the fallback when none is configured.
/// </summary>
public sealed class PublicUrlTests
{
    [Theory]
    [InlineData("https://api.wayd.example.com", "https://api.wayd.example.com")]
    [InlineData(" https://api.wayd.example.com/ ", "https://api.wayd.example.com")]
    [InlineData("HTTPS://API.Wayd.Example.COM:443", "https://api.wayd.example.com")]
    [InlineData("https://api.wayd.example.com:8443", "https://api.wayd.example.com:8443")]
    [InlineData("https://bücher.example", "https://xn--bcher-kva.example")]
    [InlineData("http://localhost:5150", "http://localhost:5150")]
    [InlineData("http://127.0.0.1:5150", "http://127.0.0.1:5150")]
    [InlineData("http://[::1]:5150", "http://[::1]:5150")]
    public void For_AnswersTheConfiguredOriginInASCII_WhateverTheRequest(string configured, string expected)
    {
        // Arrange
        var publicUrl = new PublicUrl(configured);

        // Act
        var result = publicUrl.For(Request("http", "internal-host:8080"));

        // Assert
        result.Should().Be(expected);
        publicUrl.IsConfigured.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void For_AnswersTheRequestsUrl_WhenNoneIsConfigured(string? configured)
    {
        // Arrange
        var publicUrl = new PublicUrl(configured);

        // Act
        var result = publicUrl.For(Request("https", "localhost:7021", "/base"));

        // Assert
        result.Should().Be("https://localhost:7021/base");
        publicUrl.IsConfigured.Should().BeFalse();
    }

    [Theory]
    [InlineData("api.wayd.example.com")]
    [InlineData("/relative")]
    [InlineData("http://api.wayd.example.com")]
    [InlineData("ftp://api.wayd.example.com")]
    [InlineData("https://wayd.example.com/api-host")]
    [InlineData("https://wayd.example.com/api-host/")]
    [InlineData("https://api.wayd.example.com?tenant=1")]
    [InlineData("https://api.wayd.example.com#top")]
    [InlineData("https://user:secret@api.wayd.example.com")]
    public void Constructor_RefusesAUrlWaydCannotPublish(string configured)
    {
        // Arrange
        var create = () => new PublicUrl(configured);

        // Act
        var thrown = create.Should().Throw<InvalidOperationException>();

        // Assert
        thrown.Which.Message.Should().Contain(PublicUrl.ConfigurationKey);
    }

    private static HttpRequest Request(string scheme, string host, string pathBase = "")
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString(host);
        context.Request.PathBase = pathBase;
        return context.Request;
    }
}
