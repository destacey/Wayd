using FluentAssertions;
using Hangfire.Storage.Monitoring;
using Wayd.Infrastructure.BackgroundJobs;

namespace Wayd.Infrastructure.Tests.Sut.BackgroundJobs;

public sealed class HangfireServiceTests
{
    private static StateHistoryDto State(string name, string? exceptionMessage = null) => new()
    {
        StateName = name,
        Data = exceptionMessage is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { ["ExceptionMessage"] = exceptionMessage },
    };

    [Fact]
    public void CurrentFailure_ForRetriedJob_ReturnsTheMostRecentFailure()
    {
        // Arrange — history is oldest-first, and a retried job records one entry per attempt.
        var history = new[]
        {
            State("Enqueued"),
            State("Processing"),
            State("Failed", "first attempt"),
            State("Scheduled"),
            State("Processing"),
            State("Failed", "final attempt"),
        };

        // Act
        var failure = HangfireService.CurrentFailure(history);

        // Assert — reporting the first failure would show a stale stack trace for the current state.
        failure!.Data["ExceptionMessage"].Should().Be("final attempt");
    }

    [Fact]
    public void CurrentFailure_ForSingleFailure_ReturnsIt()
    {
        // Arrange
        var history = new[] { State("Enqueued"), State("Processing"), State("Failed", "only") };

        // Act
        var failure = HangfireService.CurrentFailure(history);

        // Assert
        failure!.Data["ExceptionMessage"].Should().Be("only");
    }

    [Fact]
    public void CurrentFailure_WhenJobNeverFailed_ReturnsNull()
    {
        // Arrange
        var history = new[] { State("Enqueued"), State("Processing"), State("Succeeded") };

        // Act
        var failure = HangfireService.CurrentFailure(history);

        // Assert
        failure.Should().BeNull();
    }

    [Fact]
    public void CurrentFailure_WithNoHistory_ReturnsNull()
    {
        // Arrange & Act
        var failure = HangfireService.CurrentFailure(null);

        // Assert
        failure.Should().BeNull();
    }

    [Theory]
    [InlineData(0, 50, 0)]
    [InlineData(3, 50, 150)]
    [InlineData(-5, 50, 0)]
    public void PageOffset_ForNormalPaging_IsPageTimesSize(int pageNumber, int pageSize, int expected)
    {
        // Arrange & Act
        var offset = HangfireService.PageOffset(pageNumber, pageSize);

        // Assert
        offset.Should().Be(expected);
    }

    [Fact]
    public void PageOffset_ForAPageNumberThatWouldOverflow_Saturates()
    {
        // Arrange — the monitoring API takes int offsets; wrapping would produce a negative one.
        const int pageSize = HangfireService.MaxPageSize;

        // Act
        var offset = HangfireService.PageOffset(int.MaxValue, pageSize);

        // Assert
        offset.Should().BePositive();
        offset.Should().Be(int.MaxValue - pageSize);
    }
}
