using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Wayd.Infrastructure;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Asserts the security headers on a REAL response from the booted host, against literal header-name
/// strings. Both details are the point of this suite: the middleware shipped for a long time reading a
/// settings shape the config file did not have, so every value bound null and no header was ever sent
/// while the config file still looked complete. A test that asserted on the bound settings object, or
/// that reused the production header-name constants, would have passed the whole time.
/// </summary>
public sealed class SecurityHeadersTests(WaydApiFactory factory) : IClassFixture<WaydApiFactory>
{
    private readonly WaydApiFactory _factory = factory;

    [Theory]
    [InlineData("X-Frame-Options", "DENY")]
    [InlineData("X-Content-Type-Options", "nosniff")]
    [InlineData("Referrer-Policy", "strict-origin-when-cross-origin")]
    [InlineData("Permissions-Policy", "geolocation=(), camera=(), microphone=()")]
    public async Task Response_CarriesConfiguredSecurityHeader(string headerName, string expectedValue)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(ServiceEndpoints.AlivenessEndpointPath, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(response.Headers.TryGetValues(headerName, out var values), $"{headerName} was not present on the response.");
        Assert.Equal(expectedValue, Assert.Single(values!));
    }

    [Theory]
    [InlineData("frame-ancestors 'none'")]
    [InlineData("base-uri 'self'")]
    [InlineData("form-action 'self'")]
    [InlineData("object-src 'none'")]
    public async Task ContentSecurityPolicy_ContainsDirective(string directive)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(ServiceEndpoints.AlivenessEndpointPath, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var values), "Content-Security-Policy was not present on the response.");
        Assert.Contains(directive, Assert.Single(values!));
    }

    [Fact]
    public async Task Response_DoesNotCarryDeprecatedXxssProtection()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(ServiceEndpoints.AlivenessEndpointPath, TestContext.Current.CancellationToken);

        // Assert — the header enables a filter that can introduce cross-site leaks; CSP replaces it.
        Assert.False(response.Headers.Contains("X-XSS-Protection"));
    }

    [Fact]
    public async Task Response_OmitsHstsOverPlainHttp()
    {
        // Arrange — TLS terminates upstream, so the app must not claim HSTS on the plain-HTTP hop.
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(ServiceEndpoints.AlivenessEndpointPath, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Theory]
    [InlineData("X-Frame-Options", "DENY")]
    [InlineData("X-Content-Type-Options", "nosniff")]
    [InlineData("Referrer-Policy", "strict-origin-when-cross-origin")]
    [InlineData("Permissions-Policy", "geolocation=(), camera=(), microphone=()")]
    [InlineData("Content-Security-Policy", "frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'")]
    public async Task EmptyConfigurationStillSendsSecureDefault(string headerName, string expectedValue)
    {
        // Arrange — every configurable key blanked, the shape a mis-nested or renamed config section
        // produces. The headers must fall back to their defaults rather than disappear, which is the
        // failure this middleware originally shipped with.
        using var factory = WithSettings(
            ("ReferrerPolicy", string.Empty),
            ("PermissionsPolicy", string.Empty));
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync(ServiceEndpoints.AlivenessEndpointPath, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(response.Headers.TryGetValues(headerName, out var values), $"{headerName} was not present on the response.");
        Assert.Equal(expectedValue, Assert.Single(values!));
    }

    [Theory]
    [InlineData("X-Frame-Options")]
    [InlineData("X-Content-Type-Options")]
    [InlineData("Referrer-Policy")]
    [InlineData("Permissions-Policy")]
    [InlineData("Content-Security-Policy")]
    public async Task EnableFalse_SendsNoSecurityHeaderAtAll(string headerName)
    {
        // Arrange — the kill switch, for a deployment whose edge owns these headers.
        using var factory = WithSettings(("Enable", "false"));
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync(ServiceEndpoints.AlivenessEndpointPath, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(response.Headers.Contains(headerName));
    }

    [Theory]
    [InlineData("ReferrerPolicy", "no-referrer", "Referrer-Policy")]
    [InlineData("PermissionsPolicy", "geolocation=(self)", "Permissions-Policy")]
    public async Task ConfiguredValue_OverridesTheDefault(string settingKey, string configuredValue, string headerName)
    {
        // Arrange — proves config is actually read. Asserting only the defaults would pass even if the
        // middleware ignored configuration entirely.
        using var factory = WithSettings((settingKey, configuredValue));
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync(ServiceEndpoints.AlivenessEndpointPath, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(response.Headers.TryGetValues(headerName, out var values), $"{headerName} was not present on the response.");
        Assert.Equal(configuredValue, Assert.Single(values!));
    }

    [Theory]
    [InlineData("true", "max-age=31536000; includeSubDomains")]
    [InlineData("false", "max-age=31536000")]
    public async Task EnableHsts_EmitsMaxAgeOverHttps(string includeSubDomains, string expectedValue)
    {
        // Arrange — the test server only speaks https when given an https base address.
        using var factory = WithSettings(
            ("EnableHsts", "true"),
            ("HstsMaxAgeSeconds", "31536000"),
            ("HstsIncludeSubDomains", includeSubDomains));
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });

        // Act
        var response = await client.GetAsync(ServiceEndpoints.AlivenessEndpointPath, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(response.Headers.TryGetValues("Strict-Transport-Security", out var values), "Strict-Transport-Security was not present on the response.");
        Assert.Equal(expectedValue, Assert.Single(values!));
    }

    [Fact]
    public async Task EnableHsts_StillOmitsTheHeaderOverPlainHttp()
    {
        // Arrange — TLS terminates upstream, so an enabled-but-plain-HTTP hop must stay silent rather
        // than latch a max-age the edge cannot honour.
        using var factory = WithSettings(("EnableHsts", "true"), ("HstsMaxAgeSeconds", "31536000"));
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync(ServiceEndpoints.AlivenessEndpointPath, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public async Task EnableHsts_WithZeroMaxAge_OmitsTheHeader()
    {
        // Arrange — a zero max-age would tell browsers to forget the policy; treat it as "off" rather
        // than emitting a header that undoes an edge-set one.
        using var factory = WithSettings(("EnableHsts", "true"), ("HstsMaxAgeSeconds", "0"));
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });

        // Act
        var response = await client.GetAsync(ServiceEndpoints.AlivenessEndpointPath, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    private WebApplicationFactory<Program> WithSettings(params (string Key, string Value)[] settings) =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                settings.Select(s => new KeyValuePair<string, string?>($"SecurityHeaderSettings:{s.Key}", s.Value)))));
}
