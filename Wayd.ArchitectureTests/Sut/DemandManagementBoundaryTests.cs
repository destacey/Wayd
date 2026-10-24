using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Wayd.ArchitectureTests.Helpers;
using Wayd.Common.Application.Persistence;
using Wayd.DemandManagement.Application;

namespace Wayd.ArchitectureTests.Sut;

/// <summary>
/// Holds Demand Management to its own data: it reaches the database only through
/// <see cref="IDemandManagementDbContext"/>, and never through another module's types.
/// </summary>
/// <remarks>
/// The older modules' contexts inherit <see cref="IWaydDbContext"/>, so their boundary is a convention. This
/// module's is enforced, and these tests are what keep it so: another module's data reaches it as a same-Id
/// copy fed by that module's events, never by referencing the module.
/// </remarks>
public class DemandManagementBoundaryTests
{
    private static readonly Assembly DomainAssembly = AssemblyHelper.GetServiceDomainAssemblies()
        .Single(a => a.GetName().Name == "Wayd.DemandManagement.Domain");
    private static readonly Assembly ApplicationAssembly = typeof(IDemandManagementDbContext).Assembly;

    [Fact]
    public void DbContext_ShouldNotInheritTheSharedContext()
    {
        // Arrange
        var context = typeof(IDemandManagementDbContext);

        // Act
        var inheritsShared = typeof(IWaydDbContext).IsAssignableFrom(context);

        // Assert
        inheritsShared.Should().BeFalse(
            "IWaydDbContext exposes the database facade, the change tracker and the shared identity tables; " +
            "the module's context exposes only what the module owns");
    }

    [Fact]
    public void Module_ShouldNotDependOnTheSharedContext()
    {
        // Arrange
        var types = Types.InAssemblies([DomainAssembly, ApplicationAssembly]);

        // Act
        var result = types
            .ShouldNot()
            .HaveDependencyOn(typeof(IWaydDbContext).FullName)
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "Demand Management reaches the database only through IDemandManagementDbContext. Violating types: {0}",
            string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? []));
    }

    [Fact]
    public void Module_ShouldNotDependOnAnotherModule()
    {
        // Arrange
        var otherModules = AssemblyHelper.GetServiceDomainAssemblies()
            .Concat(AssemblyHelper.GetServiceApplicationAssemblies())
            .Select(a => a.GetName().Name!)
            .Where(name => !name.StartsWith("Wayd.DemandManagement."))
            .Append("Wayd.Links")
            .Distinct()
            .ToArray();
        var types = Types.InAssemblies([DomainAssembly, ApplicationAssembly]);

        // Act
        var result = types
            .ShouldNot()
            .HaveDependencyOnAny(otherModules)
            .GetResult();

        // Assert
        otherModules.Should().Contain("Wayd.ProjectPortfolioManagement.Domain", "the scan must see the other modules to mean anything");
        result.IsSuccessful.Should().BeTrue(
            "Demand Management reads another module's data through a same-Id copy fed by its events, never by " +
            "referencing the module. Violating types: {0}",
            string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? []));
    }
}
