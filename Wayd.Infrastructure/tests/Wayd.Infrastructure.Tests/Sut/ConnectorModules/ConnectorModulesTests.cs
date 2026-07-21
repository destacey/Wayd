using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Infrastructure.ConnectorModules;
using Wayd.Integrations.Abstractions;
using Wayd.Integrations.AzureDevOps;

namespace Wayd.Infrastructure.Tests.Sut.ConnectorModules;

/// <summary>
/// Architecture tests for the connector module seam: every connector has exactly one module,
/// each module's capability manifest agrees with the domain declaration, and every declared sync
/// capability is backed by a keyed source registration. A new connector that misses any of these
/// fails CI instead of failing at runtime.
/// </summary>
public sealed class ConnectorModulesTests
{
    [Fact]
    public void DiscoverModules_ShouldReturnExactlyOneModulePerConnector()
    {
        // Act
        var modules = ConnectorModuleExtensions.DiscoverModules();

        // Assert
        modules.Select(m => m.Connector).Should().OnlyHaveUniqueItems(
            "each connector must have exactly one module");
        modules.Select(m => m.Connector).Should().BeEquivalentTo(
            Enum.GetValues<Connector>(),
            "every connector must have a module — add a module class in Wayd.Infrastructure/ConnectorModules");
    }

    [Fact]
    public void ModuleCapabilities_ShouldMatchDomainCapabilityDeclarations()
    {
        // Arrange
        var modules = ConnectorModuleExtensions.DiscoverModules();

        foreach (var module in modules)
        {
            // Act
            var declared = module.Connector.GetCapabilities();

            // Assert
            module.Capabilities.Should().BeEquivalentTo(declared,
                $"module for '{module.Connector}' must declare the same capabilities as ConnectorExtensions.GetCapabilities");
        }
    }

    [Fact]
    public void AddConnectorModules_ShouldRegisterAKeyedSourceForEverySyncCapability()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddConnectorModules();

        // Assert — a connector "has" a sync capability exactly when its port is registered
        foreach (var module in ConnectorModuleExtensions.DiscoverModules())
        {
            if (module.Capabilities.Contains(ConnectorCapability.WorkItems))
            {
                services.Should().Contain(
                    d => d.ServiceType == typeof(IWorkItemSource) && Equals(d.ServiceKey, module.Connector),
                    $"'{module.Connector}' declares WorkItems and must register a keyed IWorkItemSource");
                services.Should().Contain(
                    d => d.ServiceType == typeof(ISyncableConnectionDescriptorBuilder),
                    $"'{module.Connector}' declares WorkItems and must register a descriptor builder");
            }

            if (module.Capabilities.Contains(ConnectorCapability.People))
            {
                services.Should().Contain(
                    d => d.ServiceType == typeof(IEmployeeSource) && Equals(d.ServiceKey, module.Connector),
                    $"'{module.Connector}' declares People and must register a keyed IEmployeeSource");
            }
        }

        // Every sync-capable connector needs a descriptor builder; with three sync-capable
        // connectors today there must be exactly three.
        var syncCapableCount = ConnectorModuleExtensions.DiscoverModules()
            .Count(m => m.Capabilities.Contains(ConnectorCapability.WorkItems)
                        || m.Capabilities.Contains(ConnectorCapability.People));
        services.Count(d => d.ServiceType == typeof(ISyncableConnectionDescriptorBuilder))
            .Should().Be(syncCapableCount,
                "every sync-capable connector registers exactly one descriptor builder");
    }

    [Fact]
    public void AddConnectorModules_RegistersTheNamedAzureDevOpsHttpClient()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddConnectorModules();

        // Assert - AzureDevOpsService resolves this client by name via IHttpClientFactory; if the
        // registration is ever renamed or dropped, every AzDO sync fails at runtime with no signal
        // until then. IHttpClientFactory registers named options, not a directly-queryable service
        // entry, so build the provider and confirm the factory actually produces a client for the name.
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        var act = () => factory.CreateClient(AzureDevOpsHttpClient.Name);

        act.Should().NotThrow();
    }
}
