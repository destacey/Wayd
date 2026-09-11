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
    public void EnsureDefaultRecurringJobs_SchedulesTheImportStallSweepWhenNothingRunsIt()
    {
        // Arrange
        ScheduledJobsAre(new RecurringJobDto { Id = "people-sync", Action = nameof(IJobManager.RunPeopleSync) });

        // Act
        CreateServices().EnsureDefaultRecurringJobs();

        // Assert
        _jobService.Verify(
            s => s.AddOrUpdate(
                DefaultRecurringJobs.ImportStallRecoveryJobId,
                It.IsAny<Expression<Func<Task>>>(),
                It.IsAny<Func<string>>()),
            Times.Once);
    }

    [Fact]
    public void EnsureDefaultRecurringJobs_LeavesAScheduleAnAdminAlreadyMadeAlone()
    {
        // Arrange — the sweep, scheduled under a name of the admin's own
        ScheduledJobsAre(new RecurringJobDto { Id = "nightly-import-recovery", Action = nameof(IJobManager.RunImportStallRecovery) });

        // Act
        CreateServices().EnsureDefaultRecurringJobs();

        // Assert — not replaced, and not joined by a second
        _jobService.Verify(
            s => s.AddOrUpdate(It.IsAny<string>(), It.IsAny<Expression<Func<Task>>>(), It.IsAny<Func<string>>()),
            Times.Never);
    }
}
