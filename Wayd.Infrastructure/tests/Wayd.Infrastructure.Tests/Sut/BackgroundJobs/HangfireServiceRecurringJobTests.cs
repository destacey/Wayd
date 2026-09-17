using FluentAssertions;
using Hangfire;
using Hangfire.InMemory;
using Wayd.Infrastructure.BackgroundJobs;

namespace Wayd.Infrastructure.Tests.Sut.BackgroundJobs;

/// <summary>
/// Against real Hangfire storage: the stored invocation is what identifies a recurring job once its method
/// is gone, and only the storage shows that lookup reads the right hash.
/// </summary>
public sealed class HangfireServiceRecurringJobTests
{
    public static Task Retired() => Task.CompletedTask;
    public static Task StillHere() => Task.CompletedTask;

    public HangfireServiceRecurringJobTests()
    {
        JobStorage.Current = new InMemoryStorage();
    }

    [Fact]
    public void RemoveRecurringJobsInvoking_RemovesEveryScheduleForThatMethodAndNoOther()
    {
        // Arrange — two schedules for the retired method under an admin's own ids, one for another method
        RecurringJob.AddOrUpdate("graph-nightly", () => Retired(), Cron.Daily());
        RecurringJob.AddOrUpdate("graph-hourly", () => Retired(), Cron.Hourly());
        RecurringJob.AddOrUpdate("keep", () => StillHere(), Cron.Daily());
        var sut = new HangfireService();

        // Act
        var removed = sut.RemoveRecurringJobsInvoking(nameof(Retired));

        // Assert
        removed.Should().BeEquivalentTo("graph-nightly", "graph-hourly");
        sut.GetRecurringJobs().Select(j => j.Id).Should().Equal("keep");
    }

    [Fact]
    public void RemoveRecurringJobsInvoking_ReturnsNothingWhenNoScheduleNamesTheMethod()
    {
        // Arrange
        RecurringJob.AddOrUpdate("keep", () => StillHere(), Cron.Daily());
        var sut = new HangfireService();

        // Act
        var removed = sut.RemoveRecurringJobsInvoking(nameof(Retired));

        // Assert
        removed.Should().BeEmpty();
        sut.GetRecurringJobs().Should().ContainSingle(j => j.Id == "keep");
    }
}
