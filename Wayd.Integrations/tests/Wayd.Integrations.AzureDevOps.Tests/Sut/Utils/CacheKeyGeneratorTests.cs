using Wayd.Integrations.AzureDevOps.Utils;

namespace Wayd.Integrations.AzureDevOps.Tests.Sut.Utils;

/// <summary>
/// Tests for CacheKeyGenerator, focusing on cache key optimization and consistency.
/// </summary>
public class CacheKeyGeneratorTests
{
    [Fact]
    public void GetCacheKey_WithSameInputs_ReturnsSameCachedKey()
    {
        // Arrange
        var resourceType = "azdo-iterations";
        var organizationUrl = "https://dev.azure.com/myorg";
        var projectName = "MyProject";
        var teamSettings = new Dictionary<Guid, Guid?>
        {
            { Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222") },
            { Guid.Parse("33333333-3333-3333-3333-333333333333"), Guid.Parse("44444444-4444-4444-4444-444444444444") }
        };

        // Act - Call GetCacheKey multiple times with the same inputs
        var key1 = InvokeGetCacheKey(resourceType, organizationUrl, projectName, teamSettings);
        var key2 = InvokeGetCacheKey(resourceType, organizationUrl, projectName, teamSettings);
        var key3 = InvokeGetCacheKey(resourceType, organizationUrl, projectName, teamSettings);

        // Assert
        key1.Should().Be(key2);
        key2.Should().Be(key3);
        key1.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetCacheKey_WithDifferentTeamSettings_ReturnsDifferentKeys()
    {
        // Arrange
        var resourceType = "azdo-iterations";
        var organizationUrl = "https://dev.azure.com/myorg";
        var projectName = "MyProject";

        var teamSettings1 = new Dictionary<Guid, Guid?>
        {
            { Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222") }
        };

        var teamSettings2 = new Dictionary<Guid, Guid?>
        {
            { Guid.Parse("33333333-3333-3333-3333-333333333333"), Guid.Parse("44444444-4444-4444-4444-444444444444") }
        };

        // Act
        var key1 = InvokeGetCacheKey(resourceType, organizationUrl, projectName, teamSettings1);
        var key2 = InvokeGetCacheKey(resourceType, organizationUrl, projectName, teamSettings2);

        // Assert
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GetCacheKey_WithDifferentProjects_ReturnsDifferentKeys()
    {
        // Arrange
        var resourceType = "azdo-iterations";
        var organizationUrl = "https://dev.azure.com/myorg";
        var teamSettings = new Dictionary<Guid, Guid?>
        {
            { Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222") }
        };

        // Act
        var key1 = InvokeGetCacheKey(resourceType, organizationUrl, "Project1", teamSettings);
        var key2 = InvokeGetCacheKey(resourceType, organizationUrl, "Project2", teamSettings);

        // Assert
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GetCacheKey_WithNullTeamSettings_ReturnsConsistentKey()
    {
        // Arrange
        var resourceType = "azdo-iterations";
        var organizationUrl = "https://dev.azure.com/myorg";
        var projectName = "MyProject";

        // Act
        var key1 = InvokeGetCacheKey(resourceType, organizationUrl, projectName, null);
        var key2 = InvokeGetCacheKey(resourceType, organizationUrl, projectName, null);

        // Assert
        key1.Should().Be(key2);
        key1.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetCacheKey_WithEmptyTeamSettings_ReturnsConsistentKey()
    {
        // Arrange
        var resourceType = "azdo-iterations";
        var organizationUrl = "https://dev.azure.com/myorg";
        var projectName = "MyProject";
        var emptyTeamSettings = new Dictionary<Guid, Guid?>();

        // Act
        var key1 = InvokeGetCacheKey(resourceType, organizationUrl, projectName, emptyTeamSettings);
        var key2 = InvokeGetCacheKey(resourceType, organizationUrl, projectName, null);

        // Assert - Empty and null team settings should produce the same key
        key1.Should().Be(key2);
    }

    [Fact]
    public void GetCacheKey_WithDifferentOrganizationHosts_ReturnsDifferentKeys()
    {
        // Arrange - Use different hosts, not just different paths
        var resourceType = "azdo-iterations";
        var projectName = "MyProject";
        var teamSettings = new Dictionary<Guid, Guid?>
        {
            { Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222") }
        };

        // Act
        var key1 = InvokeGetCacheKey(resourceType, "https://org1.visualstudio.com", projectName, teamSettings);
        var key2 = InvokeGetCacheKey(resourceType, "https://org2.visualstudio.com", projectName, teamSettings);

        // Assert
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GetCacheKey_WithDifferentOrganizationsOnSameHost_ReturnsDifferentKeys()
    {
        // Arrange - dev.azure.com puts the organization in the URL path, not the host
        var resourceType = "azdo-iterations";
        var projectName = "MyProject";
        var teamSettings = new Dictionary<Guid, Guid?>
        {
            { Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222") }
        };

        // Act
        var key1 = InvokeGetCacheKey(resourceType, "https://dev.azure.com/org-one", projectName, teamSettings);
        var key2 = InvokeGetCacheKey(resourceType, "https://dev.azure.com/org-two", projectName, teamSettings);

        // Assert
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GetCacheKey_WithTrailingSlashVariant_ReturnsSameKey()
    {
        // Arrange
        var resourceType = "azdo-iterations";
        var projectName = "MyProject";
        var teamSettings = new Dictionary<Guid, Guid?>
        {
            { Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222") }
        };

        // Act
        var key1 = InvokeGetCacheKey(resourceType, "https://dev.azure.com/org-one", projectName, teamSettings);
        var key2 = InvokeGetCacheKey(resourceType, "https://dev.azure.com/org-one/", projectName, teamSettings);

        // Assert - URL variants of the same organization should share a key
        key1.Should().Be(key2);
    }

    [Fact]
    public void GetCacheKey_WithTeamSettingOrderChanged_ReturnsSameKey()
    {
        // Arrange - Team settings should be order-independent due to OrderBy in implementation
        var resourceType = "azdo-iterations";
        var organizationUrl = "https://dev.azure.com/myorg";
        var projectName = "MyProject";

        var teamSettings1 = new Dictionary<Guid, Guid?>
        {
            { Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222") },
            { Guid.Parse("33333333-3333-3333-3333-333333333333"), Guid.Parse("44444444-4444-4444-4444-444444444444") }
        };

        var teamSettings2 = new Dictionary<Guid, Guid?>
        {
            { Guid.Parse("33333333-3333-3333-3333-333333333333"), Guid.Parse("44444444-4444-4444-4444-444444444444") },
            { Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222") }
        };

        // Act
        var key1 = InvokeGetCacheKey(resourceType, organizationUrl, projectName, teamSettings1);
        var key2 = InvokeGetCacheKey(resourceType, organizationUrl, projectName, teamSettings2);

        // Assert - Should be the same because dictionary order shouldn't matter
        key1.Should().Be(key2);
    }

    /// <summary>
    /// Helper method to invoke CacheKeyGenerator.GetCacheKey.
    /// </summary>
    private static string InvokeGetCacheKey(string resourceType, string organizationUrl, string projectIdOrName, Dictionary<Guid, Guid?>? teamSettings, string? extra = null)
    {
        return CacheKeyGenerator.GetCacheKey(resourceType, organizationUrl, projectIdOrName, teamSettings, extra);
    }
}
