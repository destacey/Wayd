using Wayd.Common.Domain.FeatureManagement;

namespace Wayd.Common.Domain.Tests.Sut.FeatureManagement;

public sealed class FeatureFlagsTests
{
    [Fact]
    public void All_ShouldHaveUniqueNames()
    {
        // Arrange
        var names = FeatureFlags.All.Select(d => d.Name.ToLowerInvariant());

        // Act
        var duplicates = names.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key);

        // Assert
        duplicates.Should().BeEmpty();
    }

    [Fact]
    public void All_ShouldHaveAtMostOneModuleSwitchPerModule()
    {
        // Arrange
        var switches = FeatureFlags.All.Where(d => d.IsModuleSwitch);

        // Act
        var modulesWithSeveral = switches.GroupBy(d => d.Module).Where(g => g.Count() > 1).Select(g => g.Key);

        // Assert
        modulesWithSeveral.Should().BeEmpty();
    }

    [Fact]
    public void All_ShouldDescribeEveryFlag()
    {
        // Act
        var undescribed = FeatureFlags.All.Where(d => string.IsNullOrWhiteSpace(d.Description)).Select(d => d.Name);

        // Assert
        undescribed.Should().BeEmpty();
    }

    [Fact]
    public void All_ShouldEachDefineAFlagTheSeederCanStore()
    {
        // Act
        var invalid = FeatureFlags.All.Where(d =>
                FeatureFlag.Create(d).IsFailure
                || d.Name != d.Name.Trim().ToLowerInvariant()
                || d.DisplayName.Length > FeatureFlag.DisplayNameMaxLength
                || d.Description.Length > FeatureFlag.DescriptionMaxLength)
            .Select(d => d.Name);

        // Assert
        invalid.Should().BeEmpty();
    }
}
