using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Infrastructure.Auth.Oidc;

namespace Wayd.Infrastructure.Tests.Sut.Auth.Oidc;

/// <summary>
/// Holds the wiring of the OIDC discovery client: the registered factory reaches only public authorities unless
/// the operator allows private ones, and never cloud metadata either way.
/// </summary>
public sealed class ConfigureServicesTests
{
    [Fact]
    public async Task AddOidcProviderRegistry_RefusesAPrivateAuthority_ByDefault()
    {
        // Arrange
        var factory = BuildFactory(allowPrivateNetworkAuthorities: null);

        // Act
        var act = () => factory.Get("https://10.0.0.5/realms/wayd").GetConfigurationAsync(TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<Exception>())
            .Which.ToString().Should().Contain("does not resolve to a public address");
    }

    [Fact]
    public async Task AddOidcProviderRegistry_StillRefusesCloudMetadata_WhenPrivateAuthoritiesAreAllowed()
    {
        // Arrange
        var factory = BuildFactory(allowPrivateNetworkAuthorities: "true");

        // Act
        var act = () => factory.Get("https://169.254.169.254/metadata").GetConfigurationAsync(TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<Exception>())
            .Which.ToString().Should().Contain("does not resolve to an allowed address");
    }

    private static IOidcConfigurationManagerFactory BuildFactory(string? allowPrivateNetworkAuthorities)
    {
        var settings = new Dictionary<string, string?>();
        if (allowPrivateNetworkAuthorities is not null)
            settings[$"{OidcNetworkOptions.SectionName}:{nameof(OidcNetworkOptions.AllowPrivateNetworkAuthorities)}"] = allowPrivateNetworkAuthorities;

        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build())
            .AddLogging();
        services.AddOidcProviderRegistry();

        return services.BuildServiceProvider().GetRequiredService<IOidcConfigurationManagerFactory>();
    }
}
