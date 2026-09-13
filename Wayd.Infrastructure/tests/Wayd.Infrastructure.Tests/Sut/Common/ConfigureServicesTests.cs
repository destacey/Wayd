using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Interfaces;
using Wayd.Infrastructure.Common;
using Wayd.Tests.Shared;
using ServiceRegistration = Wayd.Infrastructure.Common.ConfigureServices;

namespace Wayd.Infrastructure.Tests.Sut.Common;

public sealed class ConfigureServicesTests
{
    [Fact]
    public void AddServices_ShouldNotRegisterServicesFromALoadedTestLibrary()
    {
        // Arrange — this test assembly references Wayd.Tests.Shared, whose TestingDateTimeProvider implements
        // IDateTimeProvider; touching the type guarantees the library is loaded when the scan runs.
        var testDouble = typeof(TestingDateTimeProvider);
        var services = new ServiceCollection();

        // Act
        services.AddServices(typeof(IScopedService), ServiceLifetime.Scoped);

        // Assert
        services.Should().NotContain(d => d.ImplementationType == testDouble);
        services.Should().Contain(d => d.ServiceType == typeof(IDateTimeProvider));
    }

    [Theory]
    [InlineData("Wayd.Infrastructure", true)]
    [InlineData("Wayd.Work.Application", true)]
    [InlineData("Wayd.Integrations.AzureDevOps", true)]
    [InlineData("Wayd.Tests.Shared", false)]
    [InlineData("Wayd.TestData.Core", false)]
    [InlineData("Wayd.Organization.TestData", false)]
    [InlineData("Wayd.Work.Application.Tests", false)]
    [InlineData("Wayd.Web.Api.IntegrationTests", false)]
    [InlineData("Wayd.ArchitectureTests", false)]
    [InlineData("Microsoft.Extensions.Hosting", false)]
    public void IsApplicationAssembly_ShouldRecogniseTestAssembliesByName(string name, bool expected)
    {
        // Arrange
        var assembly = new AssemblyName(name);

        // Act
        var result = ServiceRegistration.IsApplicationAssembly(assembly);

        // Assert
        result.Should().Be(expected);
    }
}
