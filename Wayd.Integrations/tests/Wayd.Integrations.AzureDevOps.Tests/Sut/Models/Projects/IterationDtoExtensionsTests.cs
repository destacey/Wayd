using NodaTime;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Integrations.AzureDevOps.Models.Projects;

namespace Wayd.Integrations.AzureDevOps.Tests.Sut.Models.Projects;

public sealed class IterationDtoExtensionsTests
{
    [Fact]
    public void ToAzdoIteration_TakesTheCalendarDateOfEachMidnightUtcDate()
    {
        // Arrange
        var dto = new IterationDto
        {
            Id = 426,
            Identifier = Guid.NewGuid(),
            Name = "Sprint 12",
            Path = "\\Atlas\\Sprint 12",
            StartDate = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc),
        };

        // Act
        var iteration = dto.ToAzdoIteration(Instant.FromUtc(2026, 10, 1, 12, 0), Guid.NewGuid());

        // Assert
        iteration.Start.Should().Be(new LocalDate(2026, 9, 28));
        iteration.End.Should().Be(new LocalDate(2026, 10, 9));
        iteration.Type.Should().Be(IterationType.Sprint);
        iteration.State.Should().Be(IterationState.Active);
    }

    [Fact]
    public void ToAzdoIteration_WithoutDates_IsAnIterationWithNoDates()
    {
        // Arrange
        var dto = new IterationDto
        {
            Id = 421,
            Identifier = Guid.NewGuid(),
            Name = "Release 3",
            Path = "\\Atlas\\Release 3",
        };

        // Act
        var iteration = dto.ToAzdoIteration(Instant.FromUtc(2026, 10, 1, 12, 0), Guid.NewGuid());

        // Assert
        iteration.Start.Should().BeNull();
        iteration.End.Should().BeNull();
        iteration.Type.Should().Be(IterationType.Iteration);
    }
}
