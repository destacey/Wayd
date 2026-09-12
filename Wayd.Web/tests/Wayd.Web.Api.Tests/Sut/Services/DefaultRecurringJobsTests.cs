using System.Linq.Expressions;
using FluentAssertions;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Wayd.Common.Application.BackgroundJobs;
using Wayd.Web.Api.Interfaces;
using Wayd.Web.Api.Services;

namespace Wayd.Web.Api.Tests.Sut.Services;

public sealed class DefaultRecurringJobsTests
{
    private readonly Mock<IJobService> _jobService = new();
    private readonly List<(string JobId, string Cron)> _added = [];

    public DefaultRecurringJobsTests()
    {
        _jobService
            .Setup(s => s.AddOrUpdate(It.IsAny<string>(), It.IsAny<Expression<Func<Task>>>(), It.IsAny<Func<string>>()))
            .Callback((string jobId, Expression<Func<Task>> _, Func<string> cron) => _added.Add((jobId, cron())));
    }

    private IServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new Mock<JobStorage>().Object);
        services.AddSingleton(_jobService.Object);
        services.AddSingleton(new Mock<IJobManager>().Object);
        return services.BuildServiceProvider();
    }

    private void ScheduledJobsAre(params RecurringJobDto[] jobs) =>
        _jobService.Setup(s => s.GetRecurringJobs()).Returns(jobs);

    [Fact]
    public void EnsureDefaultRecurringJobs_SchedulesBothImportSweepsWhenNothingRunsThem()
    {
        // Arrange
        ScheduledJobsAre(new RecurringJobDto { Id = "people-sync", Action = nameof(IJobManager.RunPeopleSync) });

        // Act
        CreateServices().EnsureDefaultRecurringJobs();

        // Assert
        _added.Select(a => a.JobId).Should().BeEquivalentTo(
            DefaultRecurringJobs.ImportStallRecoveryJobId,
            DefaultRecurringJobs.ImportRetentionSweepJobId);
    }

    [Fact]
    public void EnsureDefaultRecurringJobs_RunsTheRetentionSweepOnceADay()
    {
        // Arrange
        ScheduledJobsAre();

        // Act
        CreateServices().EnsureDefaultRecurringJobs();

        // Assert — a 30-day window does not need checking more often than that
        _added.Single(a => a.JobId == DefaultRecurringJobs.ImportRetentionSweepJobId).Cron.Should().Be("0 3 * * *");
    }

    [Fact]
    public void EnsureDefaultRecurringJobs_LeavesAScheduleAnAdminAlreadyMadeAlone()
    {
        // Arrange — both sweeps, scheduled under names of the admin's own
        ScheduledJobsAre(
            new RecurringJobDto { Id = "nightly-import-recovery", Action = nameof(IJobManager.RunImportStallRecovery) },
            new RecurringJobDto { Id = "weekly-import-purge", Action = nameof(IJobManager.RunImportRetentionSweep) });

        // Act
        CreateServices().EnsureDefaultRecurringJobs();

        // Assert — not replaced, and not joined by a second
        _added.Should().BeEmpty();
    }

    [Fact]
    public void EnsureDefaultRecurringJobs_AddsOnlyTheSweepThatIsMissing()
    {
        // Arrange — a deployment from before the retention sweep had a default
        ScheduledJobsAre(new RecurringJobDto { Id = DefaultRecurringJobs.ImportStallRecoveryJobId, Action = nameof(IJobManager.RunImportStallRecovery) });

        // Act
        CreateServices().EnsureDefaultRecurringJobs();

        // Assert
        _added.Select(a => a.JobId).Should().Equal(DefaultRecurringJobs.ImportRetentionSweepJobId);
    }
}
