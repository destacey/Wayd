using FluentAssertions;
using Wayd.ArchitectureTests.Helpers;
using NetArchTest.Rules;

namespace Wayd.ArchitectureTests.Sut;

/// <summary>
/// Architecture tests to enforce Clean Architecture dependency rules.
/// These tests ensure that projects only depend on allowed layers according to Clean Architecture principles.
///
/// Dependency Flow (allowed):
/// Domain (innermost) ← Application ← Infrastructure ← Web (outermost)
///
/// Key Rules Enforced:
///
/// Domain Layer (Wayd.*.Domain):
/// - Must NOT depend on Application, Infrastructure, Web, or Integrations
/// - Can only depend on Wayd.Common and Wayd.Common.Domain
/// - Should contain only entities, value objects, domain events, and business logic
///
/// Application Layer (Wayd.*.Application):
/// - Must NOT depend on Infrastructure, Web, or Integrations
/// - Should ONLY depend on its own Domain layer (not other service Domain layers)
/// - Can depend on Wayd.Common, Wayd.Common.Domain, and Wayd.Common.Application
/// - Should contain commands, queries, DTOs, and application logic
///
/// Common Layers:
/// - Wayd.Common.Domain must NOT depend on Wayd.Common.Application
/// - Wayd.Common and Wayd.Common.Domain must NOT depend on Infrastructure
/// - Wayd.Common.Application must NOT depend on Infrastructure
///
/// Infrastructure Layer (Wayd.*.Infrastructure):
/// - Must NOT depend on Web layer
/// - Can depend on Application, Domain, and Common layers
///
/// Integration Layer (Wayd.Integrations.*):
/// - Must NOT depend on Infrastructure, Web, or any Service layers
/// - Should ONLY depend on Wayd.Common layers (Wayd.Common, Wayd.Common.Domain, Wayd.Common.Application)
/// - Should contain external system integration logic (Azure DevOps, Microsoft Graph, etc.)
///
/// Service Isolation:
/// - Each service (Domain and Application projects) should be completely isolated from other services
/// - Service Domain projects must NOT depend on other service Domain projects
/// - Service Application projects must NOT depend on other service Application projects
/// - Service Application projects must NOT depend on other service Domain projects
/// - The Organization.Domain cross-cutting dependency is a known issue that needs to be resolved
/// - All cross-service dependencies should be moved to Wayd.Common layers
///
/// Web Layer (not tested here, but allowed to depend on all layers)
/// </summary>
public class CrossProjectDependencyTests
{
    #region Domain Layer Tests

    [Fact]
    public void DomainLayer_ShouldNotDependOnApplication()
    {
        // Arrange
        var domainAssemblies = Types.InAssemblies(AssemblyHelper.GetDomainAssemblies());

        // Act
        var result = domainAssemblies
            .ShouldNot()
            .HaveDependencyOn("*.Application")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "Domain layer should not depend on Application layer. Violating types: {0}",
            string.Join(", ", result.FailingTypes ?? []));
    }

    [Fact]
    public void DomainLayer_ShouldNotDependOnInfrastructure()
    {
        // Arrange
        var domainAssemblies = Types.InAssemblies(AssemblyHelper.GetDomainAssemblies());

        // Act
        var result = domainAssemblies
            .ShouldNot()
            .HaveDependencyOn("*.Infrastructure")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "Domain layer should not depend on Infrastructure layer. Violating types: {0}",
            string.Join(", ", result.FailingTypes ?? []));
    }

    [Fact]
    public void DomainLayer_ShouldNotDependOnWeb()
    {
        // Arrange
        var domainAssemblies = Types.InAssemblies(AssemblyHelper.GetDomainAssemblies());

        // Act
        var result = domainAssemblies
            .ShouldNot()
            .HaveDependencyOn("Wayd.Web")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "Domain layer should not depend on Web layer. Violating types: {0}",
            string.Join(", ", result.FailingTypes ?? []));
    }

    [Fact]
    public void DomainLayer_ShouldOnlyDependOnCommonLayers()
    {
        // Arrange
        var domainAssemblies = Types.InAssemblies(AssemblyHelper.GetDomainAssemblies());

        // Act
        var result = domainAssemblies
            .That()
            .ResideInNamespace("Wayd")
            .And()
            .DoNotResideInNamespace("Wayd.Common.Domain")
            .ShouldNot()
            .HaveDependencyOnAll(
                "Wayd.Common.Application",
                "Wayd.Infrastructure",
                "Wayd.Integrations",
                "Wayd.Web")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "Domain projects should only depend on Wayd.Common and Wayd.Common.Domain (not Application/Infrastructure/Integrations/Web). Violating types: {0}",
            string.Join(", ", result.FailingTypes ?? []));
    }

    #endregion

    #region Application Layer Tests

    [Fact]
    public void ApplicationLayer_ShouldNotDependOnInfrastructure()
    {
        // Arrange
        var applicationAssemblies = Types.InAssemblies(AssemblyHelper.GetApplicationAssemblies());

        // Act
        var result = applicationAssemblies
            .ShouldNot()
            .HaveDependencyOn("*.Infrastructure")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "Application layer should not depend on Infrastructure layer. Violating types: {0}",
            string.Join(", ", result.FailingTypes ?? []));
    }

    [Fact]
    public void ApplicationLayer_ShouldNotDependOnWeb()
    {
        // Arrange
        var applicationAssemblies = Types.InAssemblies(AssemblyHelper.GetApplicationAssemblies());

        // Act
        var result = applicationAssemblies
            .ShouldNot()
            .HaveDependencyOn("Wayd.Web")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "Application layer should not depend on Web layer. Violating types: {0}",
            string.Join(", ", result.FailingTypes ?? []));
    }

    [Fact]
    public void ApplicationLayer_ShouldNotDependOnConcreteIntegrations()
    {
        // Arrange: Application layer is allowed to depend on Wayd.Integrations.Abstractions
        // (it defines the contracts concrete integrations implement, e.g. IWorkItemSource) but
        // must NOT depend on any concrete integration package such as Wayd.Integrations.AzureDevOps.
        var applicationAssemblies = Types.InAssemblies(AssemblyHelper.GetApplicationAssemblies());

        // Act
        var result = applicationAssemblies
            .ShouldNot()
            .HaveDependencyOnAny(
                "Wayd.Integrations.AzureDevOps",
                "Wayd.Integrations.AzureOpenAI",
                "Wayd.Integrations.MicrosoftGraph")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "Application layer should not depend on concrete Integration projects. Violating types: {0}",
            string.Join(", ", result.FailingTypes ?? []));
    }


    #endregion

    #region Infrastructure Layer Tests

    [Fact]
    public void InfrastructureLayer_ShouldNotDependOnWeb()
    {
        // Arrange
        var infrastructureAssemblies = AssemblyHelper.GetInfrastructureAssemblies();

        // Act
        var result = Types.InAssemblies(infrastructureAssemblies)
            .ShouldNot()
            .HaveDependencyOn("Wayd.Web")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "Infrastructure layer should not depend on Web layer. Violating types: {0}",
            string.Join(", ", result.FailingTypes ?? []));
    }

    #endregion

    #region Integration Layer Tests

    [Fact]
    public void IntegrationLayer_ShouldOnlyDependOnCommonLayers()
    {
        // Arrange
        var integrationAssemblies = AssemblyHelper.GetIntegrationAssemblies();

        // Act
        var result = Types.InAssemblies(integrationAssemblies)
            .ShouldNot()
            .HaveDependencyOnAll(
                "Wayd.Infrastructure",
                "Wayd.Web")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "Integration projects should only depend on Wayd.Common layers (not Infrastructure, Web, or Service layers). Violating types: {0}",
            string.Join(", ", result.FailingTypes ?? []));
    }

    [Fact]
    public void IntegrationLayer_ShouldNotDependOnServiceDomains()
    {
        // Integration projects should not depend on any service Domain projects
        var integrationAssemblies = AssemblyHelper.GetIntegrationAssemblies();
        var serviceDomains = AssemblyHelper.GetServiceDomainAssemblies();
        var failures = new List<string>();

        foreach (var integrationAssembly in integrationAssemblies)
        {
            var serviceDomainNames = serviceDomains
                .Select(a => a.GetName().Name!)
                .ToArray();

            if (serviceDomainNames.Length == 0) continue;

            var types = Types.InAssembly(integrationAssembly);
            var result = types
                .ShouldNot()
                .HaveDependencyOnAny(serviceDomainNames)
                .GetResult();

            if (!result.IsSuccessful)
            {
                var violatingTypes = string.Join(", ", result.FailingTypes ?? []);
                failures.Add($"{integrationAssembly.GetName().Name} -> {violatingTypes}");
            }
        }

        failures.Should().BeEmpty(
            "Integration projects should not depend on service Domain projects. Violations: {0}",
            string.Join("; ", failures));
    }

    [Fact]
    public void IntegrationLayer_ShouldNotDependOnServiceApplications()
    {
        // Integration projects should not depend on any service Application projects
        var integrationAssemblies = AssemblyHelper.GetIntegrationAssemblies();
        var serviceApplications = AssemblyHelper.GetServiceApplicationAssemblies();
        var failures = new List<string>();

        foreach (var integrationAssembly in integrationAssemblies)
        {
            var serviceApplicationNames = serviceApplications
                .Select(a => a.GetName().Name!)
                .ToArray();

            if (serviceApplicationNames.Length == 0) continue;

            var types = Types.InAssembly(integrationAssembly);
            var result = types
                .ShouldNot()
                .HaveDependencyOnAny(serviceApplicationNames)
                .GetResult();

            if (!result.IsSuccessful)
            {
                var violatingTypes = string.Join(", ", result.FailingTypes ?? []);
                failures.Add($"{integrationAssembly.GetName().Name} -> {violatingTypes}");
            }
        }

        failures.Should().BeEmpty(
            "Integration projects should not depend on service Application projects. Violations: {0}",
            string.Join("; ", failures));
    }

    #endregion

    #region Common Layer Tests

    [Fact]
    public void CommonDomain_ShouldNotDependOnCommonApplication()
    {
        // Arrange
        var assembly = typeof(Wayd.Common.Domain.Events.DomainEvent).Assembly;
        var types = Types.InAssembly(assembly);

        // Act
        var result = types
            .ShouldNot()
            .HaveDependencyOn("Wayd.Common.Application")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "Wayd.Common.Domain should not depend on Wayd.Common.Application. Violating types: {0}",
            string.Join(", ", result.FailingTypes ?? []));
    }

    [Fact]
    public void CommonDomain_ShouldNotDependOnInfrastructure()
    {
        // Arrange
        var assembly = typeof(Wayd.Common.Domain.Events.DomainEvent).Assembly;
        var types = Types.InAssembly(assembly);

        // Act
        var result = types
            .ShouldNot()
            .HaveDependencyOn("*.Infrastructure")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "Wayd.Common.Domain should not depend on Infrastructure. Violating types: {0}",
            string.Join(", ", result.FailingTypes ?? []));
    }

    [Fact]
    public void CommonApplication_ShouldNotDependOnInfrastructure()
    {
        // Arrange
        var assembly = typeof(Wayd.Common.Application.Persistence.IWaydDbContext).Assembly;
        var types = Types.InAssembly(assembly);

        // Act
        var result = types
            .ShouldNot()
            .HaveDependencyOn("*.Infrastructure")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "Wayd.Common.Application should not depend on Infrastructure. Violating types: {0}",
            string.Join(", ", result.FailingTypes ?? []));
    }

    #endregion

    #region Service Isolation Tests

    [Fact]
    public void ServiceDomains_ShouldNotDependOnOtherServiceDomains()
    {
        // Each service's Domain project should be isolated and not depend on other service Domains
        // This test dynamically discovers all service Domain assemblies

        var allServiceDomains = AssemblyHelper.GetServiceDomainAssemblies();
        var failures = new List<string>();

        foreach (var serviceDomain in allServiceDomains)
        {
            // Get all OTHER service domain names (exclude this one)
            var otherServiceDomainNames = allServiceDomains
                .Where(s => s != serviceDomain)
                .Select(a => a.GetName().Name!)
                .ToArray();

            if (otherServiceDomainNames.Length == 0) continue;

            var types = Types.InAssembly(serviceDomain);
            var result = types
                .ShouldNot()
                .HaveDependencyOnAny(otherServiceDomainNames)
                .GetResult();

            if (!result.IsSuccessful)
            {
                var violatingTypes = string.Join(", ", result.FailingTypes ?? []);
                failures.Add($"{serviceDomain.GetName().Name} -> {violatingTypes}");
            }
        }

        failures.Should().BeEmpty(
            "Service Domain projects should not depend on other service Domain projects. Violations: {0}",
            string.Join("; ", failures));
    }

    [Fact]
    public void ServiceApplications_ShouldNotDependOnOtherServiceApplications()
    {
        // Each service's Application project should not depend on other service Application projects
        // This test dynamically discovers all service Application assemblies

        // Known exceptions that require refactoring to resolve:
        var ignoredDependencies = new Dictionary<string, string[]>();

        var allServiceApplications = AssemblyHelper.GetServiceApplicationAssemblies();
        var failures = new List<string>();

        foreach (var serviceApplication in allServiceApplications)
        {
            var serviceApplicationName = serviceApplication.GetName().Name!;

            // Get all OTHER service application names (exclude this one)
            var otherServiceApplicationNames = allServiceApplications
                .Where(s => s != serviceApplication)
                .Select(a => a.GetName().Name!)
                .ToList();

            // Remove ignored dependencies for this service from the check
            if (ignoredDependencies.TryGetValue(serviceApplicationName, out var ignoredApps))
            {
                otherServiceApplicationNames = otherServiceApplicationNames
                    .Where(name => !ignoredApps.Contains(name))
                    .ToList();
            }

            if (otherServiceApplicationNames.Count == 0) continue;

            var types = Types.InAssembly(serviceApplication);
            var result = types
                .ShouldNot()
                .HaveDependencyOnAny(otherServiceApplicationNames.ToArray())
                .GetResult();

            if (!result.IsSuccessful)
            {
                var violatingTypes = string.Join(", ", result.FailingTypes ?? []);
                failures.Add($"{serviceApplicationName} -> {violatingTypes}");
            }
        }

        failures.Should().BeEmpty(
            "Service Application projects should not depend on other service Application projects. Violations: {0}",
            string.Join("; ", failures));
    }

    [Fact]
    public void ServiceApplications_ShouldNotDependOnOtherServiceDomains()
    {
        // Each service's Application project should only depend on its own Domain project
        // This test identifies cross-service Domain dependencies (like the Organization.Domain issue)

        var allServiceApplications = AssemblyHelper.GetServiceApplicationAssemblies();
        var allServiceDomains = AssemblyHelper.GetServiceDomainAssemblies();
        var failures = new List<string>();

        foreach (var serviceApplication in allServiceApplications)
        {
            // Extract the service name (e.g., "Wayd.Work" from "Wayd.Work.Application")
            var applicationName = serviceApplication.GetName().Name!;
            var serviceName = applicationName.Replace(".Application", "");
            var ownDomainName = serviceName + ".Domain";

            // Get all OTHER service domain names (exclude own domain)
            var otherServiceDomainNames = allServiceDomains
                .Select(a => a.GetName().Name!)
                .Where(s => s != ownDomainName)
                .ToArray();

            if (otherServiceDomainNames.Length == 0) continue;

            var types = Types.InAssembly(serviceApplication);
            var result = types
                .ShouldNot()
                .HaveDependencyOnAny(otherServiceDomainNames)
                .GetResult();

            if (!result.IsSuccessful)
            {
                var violatingTypes = string.Join(", ", result.FailingTypes ?? []);
                failures.Add($"{applicationName} depends on other service domains -> {violatingTypes}");
            }
        }

        failures.Should().BeEmpty(
            "Service Application projects should only depend on their own Domain project, not other service Domain projects. This test identifies the Organization.Domain cross-cutting dependency issue that needs to be resolved. Violations: {0}",
            string.Join("; ", failures));
    }

    #endregion
}
