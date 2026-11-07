using FluentAssertions;
using Hangfire.Storage.Monitoring;
using NodaTime;
using Wayd.Common.Application.Models;
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
        // Arrange — the monitoring API returns history newest-first, and a retried job records one entry
        // per attempt.
        var history = new[]
        {
            State("Failed", "final attempt"),
            State("Processing"),
            State("Scheduled"),
            State("Failed", "first attempt"),
            State("Processing"),
            State("Enqueued"),
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
        var history = new[] { State("Failed", "only"), State("Processing"), State("Enqueued") };

        // Act
        var failure = HangfireService.CurrentFailure(history);

        // Assert
        failure!.Data["ExceptionMessage"].Should().Be("only");
    }

    [Fact]
    public void CurrentFailure_WhenJobNeverFailed_ReturnsNull()
    {
        // Arrange
        var history = new[] { State("Succeeded"), State("Processing"), State("Enqueued") };

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
    [InlineData(1, 50, 0)]
    [InlineData(4, 50, 150)]
    [InlineData(-5, 50, 0)]
    public void PageOffset_ForNormalPaging_CountsTheItemsOnEarlierPages(int page, int pageSize, int expected)
    {
        // Arrange & Act
        var offset = HangfireService.PageOffset(new PageRequest(page, pageSize));

        // Assert
        offset.Should().Be(expected);
    }

    [Fact]
    public void PageOffset_ForAPageNumberThatWouldOverflow_Saturates()
    {
        // Arrange — the monitoring API takes int offsets; wrapping would produce a negative one.
        const int pageSize = PageSizes.Max;

        // Act
        var offset = HangfireService.PageOffset(new PageRequest(int.MaxValue, pageSize));

        // Assert
        offset.Should().BePositive();
        offset.Should().Be(int.MaxValue - pageSize);
    }

    [Fact]
    public void SucceededRunTiming_WithARecordedRunTime_StartsThatLongBeforeTheSuccess()
    {
        // Arrange
        var succeededAt = Instant.FromUtc(2026, 11, 7, 12, 0, 0);
        var stateData = new Dictionary<string, string> { ["PerformanceDuration"] = "134250", ["Latency"] = "90000" };

        // Act
        var (startedAt, duration) = HangfireService.SucceededRunTiming(stateData, succeededAt);

        // Assert — the queue wait (Latency) is not part of the run.
        duration.Should().Be(134250);
        startedAt.Should().Be(Instant.FromUtc(2026, 11, 7, 11, 57, 45).Plus(Duration.FromMilliseconds(750)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-number")]
    [InlineData("-5")]
    public void SucceededRunTiming_WithoutAUsableRunTime_ReturnsNothing(string? performanceDuration)
    {
        // Arrange
        var stateData = performanceDuration is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { ["PerformanceDuration"] = performanceDuration };

        // Act
        var (startedAt, duration) = HangfireService.SucceededRunTiming(stateData, Instant.FromUtc(2026, 11, 7, 12, 0));

        // Assert
        startedAt.Should().BeNull();
        duration.Should().BeNull();
    }

    [Fact]
    public void SucceededRunTiming_WithoutStateData_ReturnsNothing()
    {
        // Act
        var (startedAt, duration) = HangfireService.SucceededRunTiming(null, Instant.FromUtc(2026, 11, 7, 12, 0));

        // Assert
        startedAt.Should().BeNull();
        duration.Should().BeNull();
    }
}
