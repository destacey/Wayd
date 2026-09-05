using FluentAssertions;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Tests.Infrastructure;

namespace Wayd.Common.Application.Tests.Sut.Imports;

public sealed class ImportDefinitionRegistryTests
{
    private static ImportDefinitionRegistry CreateRegistry(params IImportDefinition[] definitions) => new(definitions);

    [Fact]
    public void Find_ResolvesADefinitionByItsKey()
    {
        // Arrange
        var definition = new TestImportDefinition(new TestSerializerService());
        var registry = CreateRegistry(definition);

        // Act
        var result = registry.Find("test-import");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(definition);
    }

    [Fact]
    public void Find_IgnoresCasing()
    {
        // Arrange — the key arrives from a persisted row, not from a code constant
        var registry = CreateRegistry(new TestImportDefinition(new TestSerializerService()));

        // Act
        var result = registry.Find("TEST-IMPORT");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Find_FailsRatherThanThrowsForAKeyNoLongerRegistered()
    {
        // Arrange — a run persisted under a definition that has since been removed
        var registry = CreateRegistry(new TestImportDefinition(new TestSerializerService()));

        // Act
        var result = registry.Find("retired-import");

        // Assert — the Settings page still has to render that run
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("retired-import");
    }

    [Fact]
    public void All_ExposesEveryRegisteredDefinition()
    {
        // Arrange
        var registry = CreateRegistry(new TestImportDefinition(new TestSerializerService()));

        // Act & Assert
        registry.All.Should().ContainSingle().Which.Key.Should().Be("test-import");
    }
}
