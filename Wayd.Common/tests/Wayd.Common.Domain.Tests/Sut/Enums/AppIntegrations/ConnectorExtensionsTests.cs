using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Wayd.Common.Domain.Enums.AppIntegrations;

namespace Wayd.Common.Domain.Tests.Sut.Enums.AppIntegrations;

public sealed class ConnectorExtensionsTests
{
    [Theory]
    [InlineData(Connector.AzureDevOps, ConnectorCapability.WorkItems)]
    [InlineData(Connector.AzureOpenAI, ConnectorCapability.AiProvider)]
    [InlineData(Connector.Entra, ConnectorCapability.People)]
    [InlineData(Connector.Workday, ConnectorCapability.People)]
    public void GetCapabilities_ShouldIncludeExpectedCapability(Connector connector, ConnectorCapability expectedCapability)
    {
        // Act
        var result = connector.GetCapabilities();

        // Assert
        result.Should().Contain(expectedCapability);
        connector.HasCapability(expectedCapability).Should().BeTrue();
    }

    [Fact]
    public void GetCapabilities_ShouldDeclareEveryConnector()
    {
        // Arrange
        var connectors = Enum.GetValues<Connector>();

        foreach (var connector in connectors)
        {
            // Act
            var capabilities = connector.GetCapabilities();

            // Assert
            capabilities.Should().NotBeEmpty(
                $"connector '{connector}' must declare at least one capability — add it to the GetCapabilities switch");
        }
    }

    [Fact]
    public void ConnectorCapability_ShouldDeclareDisplayCategory_ForEveryMember()
    {
        // Arrange
        var capabilities = Enum.GetValues<ConnectorCapability>();

        foreach (var capability in capabilities)
        {
            // Act
            var display = typeof(ConnectorCapability)
                .GetField(capability.ToString())!
                .GetCustomAttribute<DisplayAttribute>();

            // Assert
            display.Should().NotBeNull($"capability '{capability}' must carry a Display attribute");
            display!.GroupName.Should().NotBeNullOrWhiteSpace(
                $"capability '{capability}' must declare its display category via Display(GroupName = ...)");
        }
    }
}
