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
    // Hangfire only schedules public methods, and a public non-test method on the test class trips xUnit1013.
    public static class Jobs
    {
        public static Task Retired() => Task.CompletedTask;
        public static Task StillHere() => Task.CompletedTask;
    }

    private readonly InMemoryStorage _storage = new();
    private readonly RecurringJobManager _recurringJobs;

    public HangfireServiceRecurringJobTests()
    {
        _recurringJobs = new RecurringJobManager(_storage);
    }

    private HangfireService CreateSut() => new(_storage, new BackgroundJobClient(_storage), _recurringJobs);

    [Fact]
    public void RemoveRecurringJobsInvoking_RemovesEveryScheduleForThatMethodAndNoOther()
    {
        // Arrange — two schedules for the retired method under an admin's own ids, one for another method
        _recurringJobs.AddOrUpdate("graph-nightly", () => Jobs.Retired(), Cron.Daily());
        _recurringJobs.AddOrUpdate("graph-hourly", () => Jobs.Retired(), Cron.Hourly());
        _recurringJobs.AddOrUpdate("keep", () => Jobs.StillHere(), Cron.Daily());
        var sut = CreateSut();

        // Act
        var removed = sut.RemoveRecurringJobsInvoking(nameof(Jobs.Retired));

        // Assert
        removed.Should().BeEquivalentTo("graph-nightly", "graph-hourly");
        sut.GetRecurringJobs().Select(j => j.Id).Should().Equal("keep");
    }

    [Fact]
    public void RemoveRecurringJobsInvoking_ReturnsNothingWhenNoScheduleNamesTheMethod()
    {
        // Arrange
        _recurringJobs.AddOrUpdate("keep", () => Jobs.StillHere(), Cron.Daily());
        var sut = CreateSut();

        // Act
        var removed = sut.RemoveRecurringJobsInvoking(nameof(Jobs.Retired));

        // Assert
        removed.Should().BeEmpty();
        sut.GetRecurringJobs().Should().ContainSingle(j => j.Id == "keep");
    }
}
