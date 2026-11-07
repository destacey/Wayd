using Wayd.Common.Domain.FeatureManagement;
using Wayd.Tests.Shared.Data;

namespace Wayd.Common.Domain.Tests.Sut.FeatureManagement;

public sealed class FeatureFlagTests
{
    private readonly FeatureFlagFaker _faker = new();

    private static FeatureFlagDefinition Definition(
        string name = "my-feature",
        string displayName = "My Feature",
        string description = "A test feature flag.",
        FeatureFlagModule module = FeatureFlagModule.Planning,
        bool isModuleSwitch = false,
        bool defaultEnabled = false)
        => new(name, displayName, description, module, isModuleSwitch, defaultEnabled);

    #region Create

    [Fact]
    public void Create_ShouldCopyTheDefinition()
    {
        // Arrange
        var definition = Definition(module: FeatureFlagModule.ProductManagement, isModuleSwitch: true, defaultEnabled: true);

        // Act
        var result = FeatureFlag.Create(definition);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var flag = result.Value;
        flag.Name.Should().Be("my-feature");
        flag.DisplayName.Should().Be("My Feature");
        flag.Description.Should().Be("A test feature flag.");
        flag.Module.Should().Be(FeatureFlagModule.ProductManagement);
        flag.IsModuleSwitch.Should().BeTrue();
        flag.IsEnabled.Should().BeTrue();
        flag.IsArchived.Should().BeFalse();
        flag.FiltersJson.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldStartDisabled_WhenTheDefinitionDefaultsOff()
    {
        // Act
        var result = FeatureFlag.Create(Definition(defaultEnabled: false));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldTrimAndLowercaseName()
    {
        // Act
        var result = FeatureFlag.Create(Definition(name: "  My-Feature  "));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("my-feature");
    }

    [Fact]
    public void Create_ShouldTrimDisplayNameAndDescription()
    {
        // Act
        var result = FeatureFlag.Create(Definition(displayName: "  My Feature  ", description: "  Some description  "));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DisplayName.Should().Be("My Feature");
        result.Value.Description.Should().Be("Some description");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_ShouldReturnFailure_WhenNameIsWhitespace(string name)
    {
        // Act
        var result = FeatureFlag.Create(Definition(name: name));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Name is required");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_ShouldReturnFailure_WhenDisplayNameIsWhitespace(string displayName)
    {
        // Act
        var result = FeatureFlag.Create(Definition(displayName: displayName));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Display name is required");
    }

    [Theory]
    [InlineData("Invalid Name")]
    [InlineData("has_underscore")]
    [InlineData("has.dot")]
    [InlineData("-leading-hyphen")]
    [InlineData("trailing-hyphen-")]
    [InlineData("double--hyphen")]
    [InlineData("special@char")]
    public void Create_ShouldReturnFailure_WhenNameIsNotKebabCase(string name)
    {
        // Act
        var result = FeatureFlag.Create(Definition(name: name));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("kebab-case");
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("two-words")]
    [InlineData("three-word-name")]
    [InlineData("with-numbers-123")]
    [InlineData("v2")]
    public void Create_ShouldReturnSuccess_WhenNameIsValidKebabCase(string name)
    {
        // Act
        var result = FeatureFlag.Create(Definition(name: name));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be(name);
    }

    #endregion Create

    #region SyncWith

    [Fact]
    public void SyncWith_ShouldApplyTheDefinition_AndKeepTheEnabledState()
    {
        // Arrange
        var flag = _faker.WithName("my-feature").WithDisplayName("Old Name").WithDescription("Old description")
            .WithModule(FeatureFlagModule.Platform).AsEnabled().Generate();

        // Act
        var changed = flag.SyncWith(Definition(module: FeatureFlagModule.ProductManagement, isModuleSwitch: true, defaultEnabled: false));

        // Assert
        changed.Should().BeTrue();
        flag.DisplayName.Should().Be("My Feature");
        flag.Description.Should().Be("A test feature flag.");
        flag.Module.Should().Be(FeatureFlagModule.ProductManagement);
        flag.IsModuleSwitch.Should().BeTrue();
        flag.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void SyncWith_ShouldReportNoChange_WhenTheFlagAlreadyMatches()
    {
        // Arrange
        var flag = FeatureFlag.Create(Definition()).Value;

        // Act
        var changed = flag.SyncWith(Definition());

        // Assert
        changed.Should().BeFalse();
    }

    [Fact]
    public void SyncWith_ShouldRestoreAnArchivedFlag_AndLeaveItOff()
    {
        // Arrange
        var flag = _faker.WithName("my-feature").AsArchived().Generate();

        // Act
        var changed = flag.SyncWith(Definition(defaultEnabled: true));

        // Assert
        changed.Should().BeTrue();
        flag.IsArchived.Should().BeFalse();
        flag.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void SyncWith_ShouldThrow_WhenTheDefinitionIsForAnotherFlag()
    {
        // Arrange
        var flag = FeatureFlag.Create(Definition(name: "one-flag")).Value;

        // Act
        var act = () => flag.SyncWith(Definition(name: "another-flag"));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion SyncWith

    #region Toggle

    [Fact]
    public void Toggle_ShouldEnableFlag()
    {
        // Arrange
        var flag = FeatureFlag.Create(Definition(defaultEnabled: false)).Value;

        // Act
        flag.Toggle(true);

        // Assert
        flag.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Toggle_ShouldDisableFlag()
    {
        // Arrange
        var flag = FeatureFlag.Create(Definition(defaultEnabled: true)).Value;

        // Act
        flag.Toggle(false);

        // Assert
        flag.IsEnabled.Should().BeFalse();
    }

    #endregion Toggle

    #region Archive

    [Fact]
    public void Archive_ShouldArchiveAndDisableTheFlag()
    {
        // Arrange
        var flag = FeatureFlag.Create(Definition(defaultEnabled: true)).Value;

        // Act
        var result = flag.Archive();

        // Assert
        result.IsSuccess.Should().BeTrue();
        flag.IsArchived.Should().BeTrue();
        flag.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Archive_ShouldReturnFailure_WhenAlreadyArchived()
    {
        // Arrange
        var flag = FeatureFlag.Create(Definition()).Value;
        flag.Archive();

        // Act
        var result = flag.Archive();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already archived");
    }

    #endregion Archive
}
