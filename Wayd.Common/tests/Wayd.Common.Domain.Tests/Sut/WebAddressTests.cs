using Wayd.Common.Domain;

namespace Wayd.Common.Domain.Tests.Sut;

public sealed class WebAddressTests
{
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com/a?b=c#d")]
    [InlineData("  https://dev.azure.com/org/project/_workitems/edit/  ")]
    [InlineData("HTTPS://EXAMPLE.COM")]
    public void IsHttp_AcceptsAnAbsoluteHttpOrHttpsAddress(string url)
    {
        // Act
        var result = WebAddress.IsHttp(url);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.com/file")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("example.com/page")]
    [InlineData("/relative/path")]
    [InlineData("//example.com")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsHttp_RefusesAnythingElse(string? url)
    {
        // Act
        var result = WebAddress.IsHttp(url);

        // Assert
        result.Should().BeFalse();
    }
}
