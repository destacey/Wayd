using Wayd.Common.Domain.FeatureManagement;
using Wayd.Tests.Shared.Data;

namespace Wayd.Common.Domain.Tests.Sut.FeatureManagement;

public sealed class FeatureFlagTests
{
    private readonly FeatureFlagFaker _faker = new();

    #region Create

    [Fact]
    public void Create_ShouldReturnSuccess_WhenValidData()
    {
        // Arrange
        var name = "my-feature";
        var displayName = "My Feature";
        var description = "A test feature flag.";

        // Act
        var result = FeatureFlag.Create(name, displayName, description, true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var flag = result.Value;
        flag.Name.Should().Be(name);
        flag.DisplayName.Should().Be(displayName);
        flag.Description.Should().Be(description);
        flag.IsEnabled.Should().BeTrue();
        flag.IsArchived.Should().BeFalse();
        flag.IsSystem.Should().BeFalse();
        flag.FiltersJson.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldSetIsSystemTrue_WhenSpecified()
    {
        // Act
        var result = FeatureFlag.Create("system-flag", "System Flag", null, false, isSystem: true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsSystem.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldDefaultIsSystemToFalse()
    {
        // Act
        var result = FeatureFlag.Create("user-flag", "User Flag", null, false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsSystem.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldReturnSuccess_WhenDisabled()
    {
        // Act
        var result = FeatureFlag.Create("disabled-flag", "Disabled Flag", null, false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldReturnSuccess_WhenDescriptionIsNull()
    {
        // Act
        var result = FeatureFlag.Create("no-desc", "No Description", null, true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldTrimAndLowercaseName()
    {
        // Act
        var result = FeatureFlag.Create("  My-Feature  ", "My Feature", null, true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("my-feature");
    }

    [Fact]
    public void Create_ShouldTrimDisplayNameAndDescription()
    {
        // Act
        var result = FeatureFlag.Create("test-flag", "  My Feature  ", "  Some description  ", true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DisplayName.Should().Be("My Feature");
        result.Value.Description.Should().Be("Some description");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_ShouldReturnFailure_WhenNameIsNullOrWhitespace(string? name)
    {
        // Act
        var result = FeatureFlag.Create(name!, "Display Name", null, true);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Name is required");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_ShouldReturnFailure_WhenDisplayNameIsNullOrWhitespace(string? displayName)
    {
        // Act
        var result = FeatureFlag.Create("valid-name", displayName!, null, true);

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
        var result = FeatureFlag.Create(name, "Display Name", null, true);

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
        var result = FeatureFlag.Create(name, "Display Name", null, true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be(name);
    }

    #endregion Create

    #region Update

    [Fact]
    public void Update_ShouldReturnSuccess_WhenValidData()
    {
        // Arrange
        var flag = FeatureFlag.Create("test-flag", "Original Name", "Original description", true).Value;

        // Act
        var result = flag.Update("Updated Name", "Updated description");

        // Assert
        result.IsSuccess.Should().BeTrue();
        flag.DisplayName.Should().Be("Updated Name");
        flag.Description.Should().Be("Updated description");
    }

    [Fact]
    public void Update_ShouldTrimValues()
    {
        // Arrange
        var flag = FeatureFlag.Create("test-flag", "Original", null, true).Value;

        // Act
        var result = flag.Update("  Updated Name  ", "  Updated description  ");

        // Assert
        result.IsSuccess.Should().BeTrue();
        flag.DisplayName.Should().Be("Updated Name");
        flag.Description.Should().Be("Updated description");
    }

    [Fact]
    public void Update_ShouldAllowNullDescription()
    {
        // Arrange
        var flag = FeatureFlag.Create("test-flag", "Original", "Has description", true).Value;

        // Act
        var result = flag.Update("Updated Name", null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        flag.Description.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Update_ShouldReturnFailure_WhenDisplayNameIsNullOrWhitespace(string? displayName)
    {
        // Arrange
        var flag = FeatureFlag.Create("test-flag", "Original", null, true).Value;

        // Act
        var result = flag.Update(displayName!, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Display name is required");
    }

    #endregion Update

    #region Toggle

    [Fact]
    public void Toggle_ShouldEnableFlag()
    {
        // Arrange
        var flag = FeatureFlag.Create("test-flag", "Test", null, false).Value;

        // Act
        flag.Toggle(true);

        // Assert
        flag.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Toggle_ShouldDisableFlag()
    {
        // Arrange
        var flag = FeatureFlag.Create("test-flag", "Test", null, true).Value;

        // Act
        flag.Toggle(false);

        // Assert
        flag.IsEnabled.Should().BeFalse();
    }

    #endregion Toggle

    #region Archive

    [Fact]
    public void Archive_ShouldReturnSuccess_WhenNotArchived()
    {
        // Arrange
        var flag = FeatureFlag.Create("test-flag", "Test", null, true).Value;

        // Act
        var result = flag.Archive();

        // Assert
        result.IsSuccess.Should().BeTrue();
        flag.IsArchived.Should().BeTrue();
        flag.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Archive_ShouldDisableEnabledFlag()
    {
        // Arrange
        var flag = FeatureFlag.Create("test-flag", "Test", null, true).Value;
        flag.IsEnabled.Should().BeTrue();

        // Act
        var result = flag.Archive();

        // Assert
        result.IsSuccess.Should().BeTrue();
        flag.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Archive_ShouldReturnFailure_WhenAlreadyArchived()
    {
        // Arrange
        var flag = FeatureFlag.Create("test-flag", "Test", null, true).Value;
        flag.Archive();

        // Act
        var result = flag.Archive();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already archived");
    }

    [Fact]
    public void Archive_ShouldReturnFailure_WhenSystemFlag()
    {
        // Arrange
        var flag = FeatureFlag.Create("system-flag", "System Flag", null, true, isSystem: true).Value;

        // Act
        var result = flag.Archive();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("System feature flags cannot be archived");
    }

    #endregion Archive
}
