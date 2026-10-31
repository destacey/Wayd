using Microsoft.Extensions.Options;
using Wayd.Common.Application.Models;

namespace Wayd.Integrations.AzureDevOps.Tests.Sut;

public class AzureDevOpsUrlResolverTests
{
    [Theory]
    [InlineData("https://dev.azure.com", "contoso", "https://dev.azure.com/contoso")]
    [InlineData("https://dev.azure.com/", "contoso", "https://dev.azure.com/contoso")]
    [InlineData("https://devops.example.com//", " contoso ", "https://devops.example.com/contoso")]
    public void GetOrganizationUrl_JoinsTheBaseUrlAndOrganizationWithOneSlash(string baseUrl, string organization, string expected)
    {
        // Arrange
        var sut = new AzureDevOpsUrlResolver(Options.Create(new AzureDevOpsOptions { BaseUrl = baseUrl }));

        // Act
        var url = sut.GetOrganizationUrl(organization);

        // Assert
        url.Should().Be(expected);
    }

    [Fact]
    public void GetOrganizationUrl_DefaultsToAzureDevOpsServices()
    {
        // Arrange
        var sut = new AzureDevOpsUrlResolver(Options.Create(new AzureDevOpsOptions()));

        // Act
        var url = sut.GetOrganizationUrl("contoso");

        // Assert
        url.Should().Be("https://dev.azure.com/contoso");
    }
}
