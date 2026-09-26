using NodaTime;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Integrations.AzureDevOps.Models.Contracts;

namespace Wayd.Integrations.AzureDevOps.Tests.Sut.Models.Contracts;

public sealed class AzdoIterationTests
{
    // A Monday-to-Friday sprint, Sep 28 – Oct 9.
    private static readonly LocalDate Start = new(2026, 9, 28);
    private static readonly LocalDate End = new(2026, 10, 9);

    [Theory]
    [InlineData(2026, 9, 27, 23, 59, IterationState.Future)]
    [InlineData(2026, 9, 28, 0, 0, IterationState.Active)]
    [InlineData(2026, 10, 9, 0, 0, IterationState.Active)]
    [InlineData(2026, 10, 9, 23, 59, IterationState.Active)]
    [InlineData(2026, 10, 10, 0, 0, IterationState.Completed)]
    public void State_IsActiveFromTheFirstDayThroughTheWholeOfTheLastDayInUtc(int year, int month, int day, int hour, int minute, IterationState expected)
    {
        // Arrange
        var now = Instant.FromUtc(year, month, day, hour, minute);

        // Act
        var iteration = Create(Start, End, now);

        // Assert
        iteration.State.Should().Be(expected);
    }

    [Theory]
    [InlineData(2026, 9, 27, IterationState.Future)]
    [InlineData(2026, 9, 28, IterationState.Active)]
    public void State_WithNoEnd_IsActiveFromTheFirstDay(int year, int month, int day, IterationState expected)
    {
        // Arrange
        var now = Instant.FromUtc(year, month, day, 12, 0);

        // Act
        var iteration = Create(Start, null, now);

        // Assert
        iteration.State.Should().Be(expected);
    }

    [Theory]
    [InlineData(2026, 10, 9, IterationState.Active)]
    [InlineData(2026, 10, 10, IterationState.Completed)]
    public void State_WithNoStart_IsActiveThroughTheLastDay(int year, int month, int day, IterationState expected)
    {
        // Arrange
        var now = Instant.FromUtc(year, month, day, 12, 0);

        // Act
        var iteration = Create(null, End, now);

        // Assert
        iteration.State.Should().Be(expected);
    }

    [Fact]
    public void State_WithNoDates_IsUnknown()
    {
        // Arrange
        var now = Instant.FromUtc(2026, 10, 1, 12, 0);

        // Act
        var iteration = Create(null, null, now);

        // Assert
        iteration.State.Should().Be(IterationState.Unknown);
    }

    private static AzdoIteration Create(LocalDate? start, LocalDate? end, Instant now) =>
        new(426, "Sprint 12", IterationType.Sprint, start, end, null,
            new AzdoIterationMetadata { ProjectId = Guid.NewGuid(), Identifier = Guid.NewGuid(), Path = "\\Atlas\\Sprint 12" }, now);
}
