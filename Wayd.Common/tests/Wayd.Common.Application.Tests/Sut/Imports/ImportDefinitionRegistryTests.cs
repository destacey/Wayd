using FluentAssertions;
using Wayd.Common.Application.Imports;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Enums.Imports;

namespace Wayd.Common.Application.Tests.Sut.Imports;

public sealed class ImportDefinitionRegistryTests
{
    private static ImportDefinitionRegistry CreateRegistry(params IImportDefinition[] definitions) => new(definitions);

    [Fact]
    public void Find_ResolvesADefinitionByItsKey()
    {
        // Arrange
        var definition = new TestImportDefinition(new ImportPayloadSerializer());
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
        var registry = CreateRegistry(new TestImportDefinition(new ImportPayloadSerializer()));

        // Act
        var result = registry.Find("TEST-IMPORT");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Find_FailsRatherThanThrowsForAKeyNoLongerRegistered()
    {
        // Arrange — a run persisted under a definition that has since been removed
        var registry = CreateRegistry(new TestImportDefinition(new ImportPayloadSerializer()));

        // Act
        var result = registry.Find("retired-import");

        // Assert — the Settings page still has to render that run
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("retired-import");
    }

    [Fact]
    public void Constructor_AcceptsASinglePassPerGroupDefinition()
    {
        // Act
        var act = () => CreateRegistry(new TestGroupedImportDefinition(new ImportPayloadSerializer()));

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_RefusesAPerGroupDefinitionWithMoreThanOnePass()
    {
        // Arrange — each pass saves before the next, so a group rejected in the second would be half applied
        var definition = new TestImportDefinition(new ImportPayloadSerializer())
        {
            AtomicityOverride = ImportAtomicity.PerGroup,
        };

        // Act
        var act = () => CreateRegistry(definition);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*single pass*");
    }

    [Fact]
    public void All_ExposesEveryRegisteredDefinition()
    {
        // Arrange
        var registry = CreateRegistry(new TestImportDefinition(new ImportPayloadSerializer()));

        // Act & Assert
        registry.All.Should().ContainSingle().Which.Key.Should().Be("test-import");
    }
}
