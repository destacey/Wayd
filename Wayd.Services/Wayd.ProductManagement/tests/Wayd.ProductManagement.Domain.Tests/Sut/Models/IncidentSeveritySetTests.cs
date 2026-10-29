using FluentAssertions;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Domain.Tests.Sut.Models;

public sealed class IncidentSeveritySetTests
{
    [Fact]
    public void CreateCustom_WithTwoLevels_Succeeds()
    {
        // Act
        var result = IncidentSeveritySet.CreateCustom("Custom", null, ["A", "B"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Levels.Should().HaveCount(2);
        result.Value.Levels[0].SortOrder.Should().Be(0);
    }

    [Fact]
    public void UpdateDetails_OnSystemSet_Fails()
    {
        // Arrange
        var set = IncidentSeveritySet.CreateSystem("SEV", null, ["SEV-1", "SEV-2"]).Value;

        // Act
        var result = set.UpdateDetails("Other", null);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ReplaceLevels_WhenAddingLevel_KeepsTheNewLevel()
    {
        // Arrange
        var set = IncidentSeveritySet.CreateCustom("Custom", null, ["A", "B"]).Value;

        // Act
        var result = set.ReplaceLevels(
            [
                (set.Levels[0].Id, "A"),
                (set.Levels[1].Id, "B"),
                (null, "C"),
            ],
            new HashSet<Guid>());

        // Assert
        result.IsSuccess.Should().BeTrue();
        set.Levels.Should().HaveCount(3);
        set.Levels.Select(l => l.Name).Should().Equal("A", "B", "C");
    }

    [Fact]
    public void ReplaceLevels_WhenLevelInUse_Fails()
    {
        // Arrange
        var set = IncidentSeveritySet.CreateCustom("Custom", null, ["A", "B"]).Value;
        var inUse = set.Levels[0].Id;

        // Act
        var result = set.ReplaceLevels([(set.Levels[1].Id, "B")], new HashSet<Guid> { inUse });

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_WhenSelected_Fails()
    {
        // Arrange
        var set = IncidentSeveritySet.CreateCustom("Custom", null, ["A", "B"]).Value;

        // Act
        var result = set.Deactivate(isSelected: true);

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}
