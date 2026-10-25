using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wayd.Common.Application.Identity.Users;
using Wayd.Infrastructure.RateLimiting;

namespace Wayd.Infrastructure.Tests.Sut.RateLimiting;

/// <summary>
/// Asserts the settings checks the rate limits are registered with, which run at startup so a bad configuration stops
/// the host instead of throttling every request, and the middleware that resolves each request's tier before the limiter
/// runs.
/// </summary>
public sealed class ConfigureServicesTests
{
    [Fact]
    public void Settings_AreValid_WithTheDefaults()
    {
        // Arrange
        using var provider = Provider([]);

        // Act
        var settings = provider.GetRequiredService<IOptions<RateLimitSettings>>().Value;

        // Assert — the docs promise a grant or revoke applies within five minutes by default
        settings.TierCacheSeconds.Should().Be(300);
    }

    [Theory]
    [InlineData("PerUserElevated:TokenLimit", "0")]
    [InlineData("PerUser:TokensPerPeriod", "0")]
    public void Settings_AreRefused_WithALimitAtZero(string key, string value)
    {
        // Arrange
        using var provider = Provider(new() { [key] = value });

        // Act
        var read = () => provider.GetRequiredService<IOptions<RateLimitSettings>>().Value;

        // Assert
        read.Should().Throw<OptionsValidationException>().WithMessage("*every number above zero*");
    }

    [Theory]
    [InlineData("PerUserElevated:TokenLimit", "199")]
    [InlineData("PerUserElevated:TokensPerPeriod", "4")]
    [InlineData("PerUserElevated:PeriodSeconds", "10")]
    public void Settings_AreRefused_WhenTheElevatedBucketIsSmallerThanTheStandardOne(string key, string value)
    {
        // Arrange
        using var provider = Provider(new() { [key] = value });

        // Act
        var read = () => provider.GetRequiredService<IOptions<RateLimitSettings>>().Value;

        // Assert
        read.Should().Throw<OptionsValidationException>().WithMessage("*PerUserElevated*at least as large*");
    }

    [Fact]
    public void Settings_AreRefused_WithANegativeTierCacheLifetime()
    {
        // Arrange
        using var provider = Provider(new() { ["TierCacheSeconds"] = "-1" });

        // Act
        var read = () => provider.GetRequiredService<IOptions<RateLimitSettings>>().Value;

        // Assert
        read.Should().Throw<OptionsValidationException>().WithMessage("*TierCacheSeconds*");
    }

    [Fact]
    public void Settings_AreValid_WithATierCacheLifetimeOfZero()
    {
        // Arrange
        using var provider = Provider(new() { ["TierCacheSeconds"] = "0" });

        // Act
        var settings = provider.GetRequiredService<IOptions<RateLimitSettings>>().Value;

        // Assert
        settings.TierCacheSeconds.Should().Be(0);
    }

    [Fact]
    public async Task ResolveTier_StoresTheTier_ForASignedInRequest()
    {
        // Arrange
        var userService = new Mock<IUserService>();
        userService.Setup(s => s.HasPermissionAsync("user-1", It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var context = Request(userService, signedIn: true);

        // Act
        await Wayd.Infrastructure.RateLimiting.ConfigureServices.ResolveTier(context, _ => Task.CompletedTask);

        // Assert
        RateLimitTierResolver.TierOf(context).Should().Be(RateLimitTier.Elevated);
    }

    [Fact]
    public async Task ResolveTier_ReadsNothing_ForAnAnonymousRequest()
    {
        // Arrange
        var userService = new Mock<IUserService>();
        var context = Request(userService, signedIn: false);
        var passedOn = false;

        // Act
        await Wayd.Infrastructure.RateLimiting.ConfigureServices.ResolveTier(context, _ => { passedOn = true; return Task.CompletedTask; });

        // Assert
        passedOn.Should().BeTrue();
        context.Items.Should().BeEmpty();
        userService.Verify(s => s.HasPermissionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveTier_ReadsNothing_ForAnEndpointExemptFromRateLimiting()
    {
        // Arrange — as /mcp is, whose tool calls are counted by their inner requests instead
        var userService = new Mock<IUserService>();
        var context = Request(userService, signedIn: true);
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new DisableRateLimitingAttribute()), "mcp"));
        var passedOn = false;

        // Act
        await Wayd.Infrastructure.RateLimiting.ConfigureServices.ResolveTier(context, _ => { passedOn = true; return Task.CompletedTask; });

        // Assert
        passedOn.Should().BeTrue();
        context.Items.Should().BeEmpty();
        userService.Verify(s => s.HasPermissionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A request whose services resolve the tier through <paramref name="userService"/>.</summary>
    private static DefaultHttpContext Request(Mock<IUserService> userService, bool signedIn)
    {
        var services = new ServiceCollection()
            .AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()))
            .AddSingleton(userService.Object)
            .AddSingleton<ILogger<RateLimitTierResolver>>(NullLogger<RateLimitTierResolver>.Instance)
            .AddSingleton(Mock.Of<IOptionsMonitor<RateLimitSettings>>(m => m.CurrentValue == new RateLimitSettings()))
            .AddScoped<RateLimitTierResolver>()
            .BuildServiceProvider();

        var identity = signedIn
            ? new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], authenticationType: "Test")
            : new ClaimsIdentity();
        return new DefaultHttpContext { RequestServices = services, User = new ClaimsPrincipal(identity) };
    }

    private static ServiceProvider Provider(Dictionary<string, string> values)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>($"{RateLimitSettings.SectionName}:{v.Key}", v.Value)))
            .Build();

        return Wayd.Infrastructure.RateLimiting.ConfigureServices.AddRateLimiting(new ServiceCollection(), config).BuildServiceProvider();
    }
}
