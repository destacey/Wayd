using System.Reflection;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Wayd.AppIntegration.Application.Interfaces;
using Wayd.AppIntegration.Domain.Models;
using Hangfire;
using Moq;
using Moq.AutoMock;
using NodaTime;
using Wayd.Common.Application.Attachments.Commands;
using Wayd.Common.Application.Enums;
using Wayd.Common.Application.Exceptions;
using Wayd.Common.Application.Imports.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Notifications.Commands;
using Wayd.Common.Application.Requests.Planning.Queries;
using Wayd.Common.Application.Requests.ProjectPortfolioManagement;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Domain.Interfaces.Organization;
using Wayd.Common.Domain.Interfaces.Planning.Iterations;
using Wayd.Common.Domain.Interfaces.ProjectPortfolioManagement;
using Wayd.Common.Domain.Interfaces.StrategicManagement;
using Wayd.Common.Domain.Models.Planning.EstimationScales;
using Wayd.DemandManagement.Application.DemandEstimationScales.Commands;
using Wayd.DemandManagement.Application.DemandProjectDeliverables.Commands;
using Wayd.DemandManagement.Application.DemandProjects.Commands;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Planning.Application.PlanningSprints.Commands;
using Wayd.Planning.Application.PlanningTeams.Commands;
using Wayd.ProductManagement.Application.ProductTeams.Commands;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Ranking.Commands;
using Wayd.ProjectPortfolioManagement.Application.Portfolios.Ranking.Queries;
using Wayd.ProjectPortfolioManagement.Application.PpmTeams.Commands;
using Wayd.ProjectPortfolioManagement.Application.Projects.Queries;
using Wayd.StrategicManagement.Application.StrategicThemes.Queries;
using Wayd.Web.Api.Interfaces;
using Wayd.Web.Api.Services;
using Wayd.Work.Application.Iterations.Queries;
using Wayd.Work.Application.WorkProjects.Commands;
using Wayd.Work.Application.WorkTeams.Commands;
using PpmSyncStrategicThemesCommand = Wayd.ProjectPortfolioManagement.Application.StrategicThemes.Commands.SyncStrategicThemesCommand;

namespace Wayd.Web.Api.Tests.Sut.Services;

public sealed class JobManagerTests
{
    private readonly AutoMocker _mocker = new();
    private readonly Mock<IDispatcher> _dispatcher;

    public JobManagerTests()
    {
        _dispatcher = _mocker.GetMock<IDispatcher>();
        _mocker.GetMock<IDateTimeProvider>().SetupGet(p => p.Now).Returns(Instant.FromUtc(2026, 6, 1, 12, 0));
        _dispatcher.Setup(d => d.Send(It.IsAny<GetSimpleTeamsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ISimpleTeam>());
        _dispatcher.Setup(d => d.Send(It.IsAny<GetSimpleIterationsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ISimpleIteration>());
        _dispatcher.Setup(d => d.Send(It.IsAny<GetStrategicThemesDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IStrategicThemeData>());
        _dispatcher.Setup(d => d.Send(It.IsAny<GetEstimationScaleSnapshotsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EstimationScaleSnapshot>());
        _dispatcher.Setup(d => d.Send(It.IsAny<GetSimpleProjectsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ISimpleProjectWithLifecycle>());
        _dispatcher.Setup(d => d.Send(It.IsAny<GetSimpleProjectDeliverablesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ISimpleProjectDeliverable>());
        _dispatcher.Setup(d => d.Send(It.IsAny<ICommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
    }

    [Fact]
    public void EveryJob_SetsItsRetryCountAndDisallowsConcurrentRuns()
    {
        // Arrange
        var jobs = typeof(IJobManager).GetMethods()
            .Select(m => typeof(JobManager).GetMethod(m.Name, [.. m.GetParameters().Select(p => p.ParameterType)])!)
            .ToList();

        // Act
        var missingRetry = jobs.Where(m => m.GetCustomAttribute<AutomaticRetryAttribute>() is null).Select(m => m.Name);
        var missingLock = jobs.Where(m => m.GetCustomAttribute<DisableConcurrentExecutionAttribute>() is null).Select(m => m.Name);

        // Assert
        jobs.Should().NotBeEmpty();
        missingRetry.Should().BeEmpty("Hangfire's default of ten retries would apply");
        missingLock.Should().BeEmpty();
    }

    [Theory]
    [InlineData(nameof(JobManager.RunPeopleSync))]
    [InlineData(nameof(JobManager.RunSyncIterations))]
    [InlineData(nameof(JobManager.RunSyncStrategicThemes))]
    [InlineData(nameof(JobManager.RunSyncProjects))]
    [InlineData(nameof(JobManager.RunSyncEstimationScales))]
    [InlineData(nameof(JobManager.RunSyncTeams))]
    [InlineData(nameof(JobManager.RunImportStallRecovery))]
    [InlineData(nameof(JobManager.RunImportRetentionSweep))]
    [InlineData(nameof(JobManager.RunNotificationRetentionSweep))]
    [InlineData(nameof(JobManager.RunAttachmentSweep))]
    public void Job_IsNotRetried(string jobName)
    {
        // Arrange
        var method = typeof(JobManager).GetMethod(jobName)!;

        // Act
        var retry = method.GetCustomAttribute<AutomaticRetryAttribute>();

        // Assert
        retry.Should().NotBeNull();
        retry!.Attempts.Should().Be(0);
    }

    [Theory]
    [InlineData(nameof(JobManager.RunWorkSync), 30, 60, 120)]
    [InlineData(nameof(JobManager.RunRepointWorkItemAttribution), 30, 60, 120)]
    [InlineData(nameof(JobManager.RunPortfolioRankRebalance), 60, 300, 600)]
    public void Job_IsRetriedThreeTimes(string jobName, params int[] delaysInSeconds)
    {
        // Arrange
        var method = typeof(JobManager).GetMethod(jobName)!;

        // Act
        var retry = method.GetCustomAttribute<AutomaticRetryAttribute>();

        // Assert
        retry.Should().NotBeNull();
        retry!.Attempts.Should().Be(3);
        retry.DelaysInSeconds.Should().Equal(delaysInSeconds);
    }

    [Theory]
    [InlineData(nameof(JobManager.RunSyncIterations))]
    [InlineData(nameof(JobManager.RunSyncStrategicThemes))]
    [InlineData(nameof(JobManager.RunSyncEstimationScales))]
    [InlineData(nameof(JobManager.RunRepointWorkItemAttribution))]
    [InlineData(nameof(JobManager.RunImportStallRecovery))]
    [InlineData(nameof(JobManager.RunImportRetentionSweep))]
    [InlineData(nameof(JobManager.RunNotificationRetentionSweep))]
    [InlineData(nameof(JobManager.RunAttachmentSweep))]
    public async Task SingleStepJob_Fails_WhenTheSyncReturnsAFailure(string jobName)
    {
        // Arrange
        SetupCommand(jobName, fails: true);
        var sut = _mocker.CreateInstance<JobManager>();

        // Act
        var act = () => Run(sut, jobName);

        // Assert
        await act.Should().ThrowAsync<InternalServerException>().WithMessage("*job failed*");
    }

    [Theory]
    [InlineData(nameof(JobManager.RunSyncIterations))]
    [InlineData(nameof(JobManager.RunSyncStrategicThemes))]
    [InlineData(nameof(JobManager.RunSyncEstimationScales))]
    [InlineData(nameof(JobManager.RunRepointWorkItemAttribution))]
    [InlineData(nameof(JobManager.RunImportStallRecovery))]
    [InlineData(nameof(JobManager.RunImportRetentionSweep))]
    [InlineData(nameof(JobManager.RunNotificationRetentionSweep))]
    [InlineData(nameof(JobManager.RunAttachmentSweep))]
    public async Task SingleStepJob_Succeeds_WhenTheSyncSucceeds(string jobName)
    {
        // Arrange
        SetupCommand(jobName, fails: false);
        var sut = _mocker.CreateInstance<JobManager>();

        // Act
        var act = () => Run(sut, jobName);

        // Assert
        await act.Should().NotThrowAsync();
        _dispatcher.Verify();
    }

    [Fact]
    public async Task RunPeopleSync_Fails_WhenTheRunnerReturnsAFailure()
    {
        // Arrange
        _mocker.GetMock<IPeopleSyncRunner>()
            .Setup(r => r.Run(SyncTriggerSource.Manual, SyncType.Full, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("people failed"));
        var sut = _mocker.CreateInstance<JobManager>();

        // Act
        var act = () => sut.RunPeopleSync(SyncType.Full, SyncTriggerSource.Manual, null, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InternalServerException>().WithMessage("*people failed*");
    }

    [Fact]
    public async Task RunWorkSync_Fails_WhenTheRunnerReturnsAFailure()
    {
        // Arrange
        var connectionId = Guid.NewGuid();
        _mocker.GetMock<IWorkSyncRunner>()
            .Setup(r => r.Run(connectionId, SyncType.Differential, SyncTriggerSource.Manual, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("work failed"));
        var sut = _mocker.CreateInstance<JobManager>();

        // Act
        var act = () => sut.RunWorkSync(SyncType.Differential, SyncTriggerSource.Manual, connectionId, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InternalServerException>().WithMessage("*work failed*");
    }

    [Fact]
    public async Task RunSyncTeams_Succeeds_WhenEveryModuleSyncs()
    {
        // Arrange
        var sut = _mocker.CreateInstance<JobManager>();

        // Act
        var act = () => sut.RunSyncTeams(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
        _dispatcher.Verify(d => d.Send(It.IsAny<SyncPlanningTeamsCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        _dispatcher.Verify(d => d.Send(It.IsAny<SyncPpmTeamsCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        _dispatcher.Verify(d => d.Send(It.IsAny<SyncProductTeamsCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        _dispatcher.Verify(d => d.Send(It.IsAny<SyncWorkTeamsCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunSyncTeams_CarriesEveryFailure_WhenSeveralModulesFail()
    {
        // Arrange
        var workFailure = new InvalidOperationException("work failed");
        _dispatcher.Setup(d => d.Send(It.IsAny<SyncPpmTeamsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("PPM failed"));
        _dispatcher.Setup(d => d.Send(It.IsAny<SyncWorkTeamsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(workFailure);
        var sut = _mocker.CreateInstance<JobManager>();

        // Act
        var act = () => sut.RunSyncTeams(TestContext.Current.CancellationToken);

        // Assert
        var thrown = await act.Should().ThrowAsync<AggregateException>();
        thrown.Which.InnerExceptions.Should().HaveCount(2);
        thrown.Which.InnerExceptions[0].Message.Should().Contain("PPM failed");
        thrown.Which.InnerExceptions[1].Should().BeSameAs(workFailure);
    }

    [Fact]
    public async Task RunSyncTeams_FailsAfterSyncingTheOtherModules_WhenOneReturnsAFailure()
    {
        // Arrange
        _dispatcher.Setup(d => d.Send(It.IsAny<SyncPpmTeamsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("PPM failed"));
        var sut = _mocker.CreateInstance<JobManager>();

        // Act
        var act = () => sut.RunSyncTeams(TestContext.Current.CancellationToken);

        // Assert
        var thrown = await act.Should().ThrowAsync<AggregateException>();
        thrown.Which.InnerExceptions.Should().ContainSingle()
            .Which.Should().BeOfType<InternalServerException>()
            .Which.Message.Should().Contain("PPM failed");
        _dispatcher.Verify(d => d.Send(It.IsAny<SyncProductTeamsCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        _dispatcher.Verify(d => d.Send(It.IsAny<SyncWorkTeamsCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunSyncTeams_FailsAfterSyncingTheOtherModules_WhenOneThrows()
    {
        // Arrange
        var planningFailure = new InvalidOperationException("planning failed");
        _dispatcher.Setup(d => d.Send(It.IsAny<SyncPlanningTeamsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(planningFailure);
        var sut = _mocker.CreateInstance<JobManager>();

        // Act
        var act = () => sut.RunSyncTeams(TestContext.Current.CancellationToken);

        // Assert
        var thrown = await act.Should().ThrowAsync<AggregateException>();
        thrown.Which.InnerExceptions.Should().ContainSingle().Which.Should().BeSameAs(planningFailure);
        _dispatcher.Verify(d => d.Send(It.IsAny<SyncPpmTeamsCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        _dispatcher.Verify(d => d.Send(It.IsAny<SyncProductTeamsCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        _dispatcher.Verify(d => d.Send(It.IsAny<SyncWorkTeamsCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunSyncTeams_StopsWhenCancelled()
    {
        // Arrange
        _dispatcher.Setup(d => d.Send(It.IsAny<SyncPlanningTeamsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        var sut = _mocker.CreateInstance<JobManager>();

        // Act
        var act = () => sut.RunSyncTeams(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        _dispatcher.Verify(d => d.Send(It.IsAny<SyncPpmTeamsCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunSyncProjects_Succeeds_WhenEveryCopySyncs()
    {
        // Arrange
        var sut = _mocker.CreateInstance<JobManager>();

        // Act
        var act = () => sut.RunSyncProjects(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
        _dispatcher.Verify(d => d.Send(It.IsAny<SyncDemandProjectDeliverablesCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunSyncProjects_FailsAfterSyncingTheOtherCopies_WhenOneThrows()
    {
        // Arrange
        var workFailure = new InvalidOperationException("work failed");
        _dispatcher.Setup(d => d.Send(It.IsAny<SyncWorkProjectsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(workFailure);
        var sut = _mocker.CreateInstance<JobManager>();

        // Act
        var act = () => sut.RunSyncProjects(TestContext.Current.CancellationToken);

        // Assert
        var thrown = await act.Should().ThrowAsync<AggregateException>();
        thrown.Which.InnerExceptions.Should().ContainSingle().Which.Should().BeSameAs(workFailure);
        _dispatcher.Verify(d => d.Send(It.IsAny<SyncDemandProjectsCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        _dispatcher.Verify(d => d.Send(It.IsAny<SyncDemandProjectDeliverablesCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunSyncProjects_Fails_WhenTheDeliverablesSyncReturnsAFailure()
    {
        // Arrange
        _dispatcher.Setup(d => d.Send(It.IsAny<SyncDemandProjectDeliverablesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("deliverables failed"));
        var sut = _mocker.CreateInstance<JobManager>();

        // Act
        var act = () => sut.RunSyncProjects(TestContext.Current.CancellationToken);

        // Assert
        var thrown = await act.Should().ThrowAsync<AggregateException>();
        thrown.Which.InnerExceptions.Should().ContainSingle()
            .Which.Message.Should().Contain("deliverables failed");
    }

    [Fact]
    public async Task RunPortfolioRankRebalance_FailsAfterRebalancingTheOtherPortfolios_WhenOneFails()
    {
        // Arrange
        Guid failing = Guid.NewGuid(), first = Guid.NewGuid(), last = Guid.NewGuid();
        _dispatcher.Setup(d => d.Send(It.IsAny<GetPortfolioIdsToRebalanceQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { first, failing, last });
        _dispatcher.Setup(d => d.Send(It.Is<RebalancePortfolioRanksCommand>(c => c.PortfolioId == failing), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("rebalance failed"));
        var sut = _mocker.CreateInstance<JobManager>();

        // Act
        var act = () => sut.RunPortfolioRankRebalance(TestContext.Current.CancellationToken);

        // Assert
        var thrown = await act.Should().ThrowAsync<AggregateException>();
        thrown.Which.InnerExceptions.Should().ContainSingle()
            .Which.Message.Should().Contain(failing.ToString()).And.Contain("rebalance failed");
        _dispatcher.Verify(d => d.Send(It.Is<RebalancePortfolioRanksCommand>(c => c.PortfolioId == last), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunPortfolioRankRebalance_Succeeds_WhenEveryPortfolioRebalances()
    {
        // Arrange
        _dispatcher.Setup(d => d.Send(It.IsAny<GetPortfolioIdsToRebalanceQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { Guid.NewGuid(), Guid.NewGuid() });
        var sut = _mocker.CreateInstance<JobManager>();

        // Act
        var act = () => sut.RunPortfolioRankRebalance(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
        _dispatcher.Verify(d => d.Send(It.IsAny<RebalancePortfolioRanksCommand>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    /// <summary>
    /// Sets the result of the one command a single-step job sends, marked verifiable so
    /// <c>_dispatcher.Verify()</c> shows the job sent it.
    /// </summary>
    private void SetupCommand(string jobName, bool fails)
    {
        var result = fails ? Result.Failure("job failed") : Result.Success();
        var count = fails ? Result.Failure<int>("job failed") : Result.Success(0);
        switch (jobName)
        {
            case nameof(JobManager.RunSyncIterations):
                _dispatcher.Setup(d => d.Send(It.IsAny<SyncPlanningSprintsCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(result).Verifiable(Times.Once);
                break;
            case nameof(JobManager.RunSyncStrategicThemes):
                _dispatcher.Setup(d => d.Send(It.IsAny<PpmSyncStrategicThemesCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(result).Verifiable(Times.Once);
                break;
            case nameof(JobManager.RunSyncEstimationScales):
                _dispatcher.Setup(d => d.Send(It.IsAny<SyncDemandEstimationScalesCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(result).Verifiable(Times.Once);
                break;
            case nameof(JobManager.RunRepointWorkItemAttribution):
                _dispatcher.Setup(d => d.Send(It.IsAny<RepointWorkItemAttributionCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(result).Verifiable(Times.Once);
                break;
            case nameof(JobManager.RunImportStallRecovery):
                _dispatcher.Setup(d => d.Send(It.IsAny<RecoverStalledImportsCommand>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(fails ? Result.Failure<StalledImportRecovery>("job failed") : Result.Success(new StalledImportRecovery(0, 0)))
                    .Verifiable(Times.Once);
                break;
            case nameof(JobManager.RunImportRetentionSweep):
                _dispatcher.Setup(d => d.Send(It.IsAny<PurgeExpiredImportPayloadsCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(count).Verifiable(Times.Once);
                break;
            case nameof(JobManager.RunNotificationRetentionSweep):
                _dispatcher.Setup(d => d.Send(It.IsAny<PurgeExpiredNotificationsCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(count).Verifiable(Times.Once);
                break;
            case nameof(JobManager.RunAttachmentSweep):
                _dispatcher.Setup(d => d.Send(It.IsAny<PurgeAbandonedAttachmentsCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(count).Verifiable(Times.Once);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(jobName), jobName, null);
        }
    }

    private static Task Run(JobManager sut, string jobName) => jobName switch
    {
        nameof(JobManager.RunSyncIterations) => sut.RunSyncIterations(TestContext.Current.CancellationToken),
        nameof(JobManager.RunSyncStrategicThemes) => sut.RunSyncStrategicThemes(TestContext.Current.CancellationToken),
        nameof(JobManager.RunSyncEstimationScales) => sut.RunSyncEstimationScales(TestContext.Current.CancellationToken),
        nameof(JobManager.RunRepointWorkItemAttribution) => sut.RunRepointWorkItemAttribution("ext-1", Guid.NewGuid(), TestContext.Current.CancellationToken),
        nameof(JobManager.RunImportStallRecovery) => sut.RunImportStallRecovery(TestContext.Current.CancellationToken),
        nameof(JobManager.RunImportRetentionSweep) => sut.RunImportRetentionSweep(TestContext.Current.CancellationToken),
        nameof(JobManager.RunNotificationRetentionSweep) => sut.RunNotificationRetentionSweep(TestContext.Current.CancellationToken),
        nameof(JobManager.RunAttachmentSweep) => sut.RunAttachmentSweep(TestContext.Current.CancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(jobName), jobName, null),
    };
}
